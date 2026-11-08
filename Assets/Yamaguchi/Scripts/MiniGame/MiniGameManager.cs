using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// 毎年 6月1日・12月1日（最初は0歳6月1日、以降6か月ごと）に30秒ミニゲームを起動し、枠を表示する。
/// 流れ：説明画面（あそびかた・まめちしき）→ スタート → 3・2・1 → 30秒 → 結果。
/// 説明画面が出た時点から結果を閉じるまで GameClock を止める（PushPause / PopPause）。
/// シーンに置かなくても、再生が始まると自動で1つ作られる（UI はコードで組み立てる）。
/// Inspector で数値を変えたいときは、このコンポーネントをシーンに置けば、置いたほうが使われる。
/// 結果は家族のステータスの書き込み窓口（AddStress / AddHealth）を通して反映する。
/// デバッグ：再生中に F8＝ミルクの温度合わせ、F9＝寝かしつけ、F10＝ミルクを飲ませる。
/// </summary>
public class MiniGameManager : MonoBehaviour
{
    private enum Phase { None, Intro, Countdown, Playing, Finished }

    public static MiniGameManager I { get; private set; }

    [Header("起動のタイミング")]
    [Tooltip("最初にミニゲームを出す月。既定は6月（0歳6月1日が1回目）")]
    [Range(1, 12)][SerializeField] private int firstMonth = 6;
    [Tooltip("何か月ごとに出すか。既定は6か月（6月・12月）")]
    [Range(1, 12)][SerializeField] private int intervalMonths = 6;
    [Tooltip("その月の何日に出すか")]
    [Range(1, 28)][SerializeField] private int triggerDay = 1;
    [Tooltip("出番が来てから、ほかの判断画面が開いていない状態がこの秒数（実時間）続いたら出す。食事の選択画面は1フレーム遅れて開くので、それを先に出すための待ち")]
    [SerializeField] private float settleSeconds = 0.3f;
    [Tooltip("どの年齢でどのミニゲームを出すか。半年ごとに、いまの年齢に合うものの中からランダムに1つ選ぶ。合うものが無い年齢では出ない")]
    [SerializeField] private List<ScheduleEntry> schedule = new List<ScheduleEntry>
    {
        new ScheduleEntry(MiniGameKind.ミルクの温度合わせ, 0f, 3f),
        new ScheduleEntry(MiniGameKind.寝かしつけ, 0f, 3f),
        new ScheduleEntry(MiniGameKind.ミルクを飲ませる, 0f, 3f),
    };

    /// 出番表の1行。minAge 歳以上、maxAge 歳未満のあいだだけ出る。
    [Serializable]
    public class ScheduleEntry
    {
        public MiniGameKind kind;
        [Tooltip("この年齢（歳）以上で出る")] public float minAge;
        [Tooltip("この年齢（歳）未満で出る")] public float maxAge = 3f;

        public ScheduleEntry() { }
        public ScheduleEntry(MiniGameKind kind, float minAge, float maxAge)
        {
            this.kind = kind;
            this.minAge = minAge;
            this.maxAge = maxAge;
        }
    }

    [Header("表示")]
    [Tooltip("3・2・1 の1つぶんの秒数")]
    [SerializeField] private float countdownStep = 0.7f;
    [Tooltip("結果が決まってから結果画面を出すまでの待ち（秒）")]
    [SerializeField] private float resultDelay = 0.8f;
    [Tooltip("再生中に F8 / F9 で直接ミニゲームを始められるようにする")]
    [SerializeField] private bool debugKeys = true;

    [Header("絵（空なら Resources/MiniGameArt を使う）")]
    [SerializeField] private MiniGameArt art;

    [Header("ミニゲームの設定")]
    [SerializeField] private MilkTemperatureGame.Settings milk = new MilkTemperatureGame.Settings();
    [SerializeField] private SleepRhythmGame.Settings sleep = new SleepRhythmGame.Settings();
    [SerializeField] private MilkFeedingGame.Settings feeding = new MilkFeedingGame.Settings();

    private static readonly Color Dark = new Color(0.25f, 0.2f, 0.18f);
    private static readonly Color Cream = new Color(0.99f, 0.96f, 0.88f);

    private DayTicker ticker;
    private bool subscribed;

    private MiniGameKind? lastKind;                                  // 同じものが2回続かないように
    private readonly List<MiniGameKind> candidates = new List<MiniGameKind>();
    private int lastFiredSlot = int.MinValue;   // 最後に予約した出番（通算の月番号 = 歳×12 + 月 − 1）
    private int lastSeenDay = -1;               // 最後に受け取った日（ロードなどで日付が飛んだかの判定用）
    private bool pending;                       // 出番が来たが、ほかの判断画面が開いていてまだ出せていない
    private float settleTimer;                  // ほかの判断画面が開いていない状態が続いている秒数
    private MiniGame current;
    private Phase phase = Phase.None;
    private bool paused;
    private float countdownLeft;
    private float startFlash;
    private float resultTimer;

    // UI
    private GameObject canvasObject;
    private TMP_Text titleText;
    private TMP_Text instructionText;
    private TMP_Text timerText;
    private RectTransform timerFill;
    private Image timerFillImage;
    private RectTransform playArea;
    private TMP_Text countdownText;

    private GameObject introPanel;
    private RectTransform introArtArea;
    private TMP_Text introStepsText;
    private TMP_Text introTipText;

    private GameObject resultPanel;
    private TMP_Text rankText;
    private TMP_Text messageText;
    private TMP_Text effectText;

    public bool IsPlaying => current != null;

    // ------------------------------------------------------------
    // 自動生成・ライフサイクル
    // ------------------------------------------------------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<MiniGameManager>() != null) return;
        new GameObject("MiniGameManager").AddComponent<MiniGameManager>();
    }

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }
        I = this;
        if (transform.parent == null) DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        Unsubscribe();
        ReleasePause();
        if (I == this) I = null;
    }

    private void TrySubscribe()
    {
        // DayTicker はシーンごとに作り直されることがあるので、毎回つなぎ直す
        if (subscribed && ticker == null) subscribed = false;
        if (subscribed || DayTicker.I == null) return;
        ticker = DayTicker.I;
        ticker.OnDayPassed += OnDayPassed;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (subscribed && ticker != null) ticker.OnDayPassed -= OnDayPassed;
        subscribed = false;
    }

    private void HoldPause()
    {
        if (paused || GameClock.I == null) return;
        GameClock.I.PushPause();
        paused = true;
    }

    private void ReleasePause()
    {
        if (!paused) return;
        paused = false;
        if (GameClock.I != null) GameClock.I.PopPause();
    }

    // ------------------------------------------------------------
    // 起動
    // ------------------------------------------------------------

    private void OnDayPassed(int totalDays)
    {
        if (intervalMonths <= 0) return;
        int slot = CurrentSlot(totalDays);

        // 初回・ロードで日付が大きく飛んだ・始め直して日付が戻った → 今の出番は「済んだ」扱いにする
        // （ロードした瞬間に過去の分が出ないように。ただし出番の日ちょうどなら出す）
        if (lastSeenDay < 0 || totalDays <= lastSeenDay || totalDays - lastSeenDay > 31)
        {
            lastFiredSlot = IsSlotDay(totalDays) ? slot - intervalMonths : slot;
            if (pending) { pending = false; ReleasePause(); }
        }
        lastSeenDay = totalDays;

        // 出番の月（6月・12月）の triggerDay 日を過ぎていて、まだ予約していなければ予約する。
        // 前の購読者のエラーなどでその日の通知を取りこぼしても、次の通知で予約される。
        // 予約した瞬間に時間を止め、実際に出すのは次のフレーム以降の Update。
        // （同じフレームで月の変わり目の食事の選択画面が開くので、それを先に済ませてもらうため）
        if (slot != int.MinValue && slot > lastFiredSlot)
        {
            lastFiredSlot = slot;
            var gs = GameState.I;
            if (gs != null && PickForAge(gs.AgeYears).HasValue)   // この年齢向けが無ければ予約しない
            {
                pending = true;
                settleTimer = 0f;
                HoldPause();
            }
        }
    }

    /// 予約したミニゲームを出せるなら出す（Update から毎フレーム呼ぶ）。
    /// 食事の選択などほかの判断画面が開いているあいだは待ち、閉じたらすぐ出す。
    /// 予約した時点で時間を止めているので、待っている間も日付は進まず、出番の日のうちに出る。
    private void TryStartPending()
    {
        if (!pending || current != null) return;
        var gs = GameState.I;
        if (gs == null) return;
        if (gs.IsGameOver) { pending = false; ReleasePause(); return; }
        // 自分の予約分（1つ）より多く止められていたら、ほかの判断画面が開いている
        if (GameClock.I != null && GameClock.I.PauseRequests > (paused ? 1 : 0)) { settleTimer = 0f; return; }
        // 開いていない状態が少し続くまで待つ（あとから開く画面を先に出す）。時間は止めてあるので日付は進まない
        settleTimer += Time.unscaledDeltaTime;
        if (settleTimer < settleSeconds) return;

        pending = false;
        var kind = PickForAge(gs.AgeYears);
        if (!kind.HasValue) { ReleasePause(); return; }   // この年齢向けのミニゲームが無い
        Debug.Log($"[MiniGame] {GameDate.Format(gs.AgeDays)} に「{kind.Value}」を開始");
        StartGame(kind.Value);
    }

    /// totalDays の時点でいちばん新しい出番（通算の月番号）。まだ一度も来ていなければ int.MinValue。
    private int CurrentSlot(int totalDays)
    {
        int year, month, day;
        GameDate.FromAgeDays(totalDays, out year, out month, out day);
        int monthIndex = year * 12 + (month - 1);
        int first = firstMonth - 1;
        int offset = ((monthIndex - first) % intervalMonths + intervalMonths) % intervalMonths;
        int slot = monthIndex - offset;
        if (offset == 0 && day < triggerDay) slot -= intervalMonths;
        return slot < first ? int.MinValue : slot;
    }

    /// totalDays が出番の日ちょうど（6月1日など）か。
    private bool IsSlotDay(int totalDays)
    {
        int year, month, day;
        GameDate.FromAgeDays(totalDays, out year, out month, out day);
        int monthIndex = year * 12 + (month - 1);
        int offset = ((monthIndex - (firstMonth - 1)) % intervalMonths + intervalMonths) % intervalMonths;
        return offset == 0 && day == triggerDay;
    }

    /// 出番表から、age 歳で出せるミニゲームをランダムに1つ選ぶ。前回と同じものは、ほかに候補があれば避ける。
    private MiniGameKind? PickForAge(float age)
    {
        candidates.Clear();
        foreach (var e in schedule)
        {
            if (e != null && age >= e.minAge && age < e.maxAge && !candidates.Contains(e.kind))
                candidates.Add(e.kind);
        }
        if (candidates.Count == 0) return null;
        if (candidates.Count > 1 && lastKind.HasValue) candidates.Remove(lastKind.Value);
        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    /// 指定のミニゲームの説明画面を開く。すでに遊んでいる最中は開かない。
    public bool StartGame(MiniGameKind kind)
    {
        if (current != null) return false;
        EnsureUI();

        var a = art != null ? art : MiniGameArt.Load();
        MiniGame game;
        if (kind == MiniGameKind.ミルクの温度合わせ) game = new MilkTemperatureGame(milk, a);
        else if (kind == MiniGameKind.寝かしつけ)   game = new SleepRhythmGame(sleep, a);
        else                                          game = new MilkFeedingGame(feeding, a);

        current = game;
        lastKind = kind;
        game.OnFinished += HandleFinished;
        OpenIntro();
        return true;
    }

    private void OpenIntro()
    {
        phase = Phase.Intro;
        MiniGameUIKit.ClearChildren(playArea);
        MiniGameUIKit.ClearChildren(introArtArea);

        titleText.text = current.Title;
        introStepsText.text = string.Join("\n", current.HowToSteps);
        introTipText.text = current.Tip;
        current.BuildIntroArt(introArtArea);

        SetPlayUIVisible(false);
        resultPanel.SetActive(false);
        introPanel.SetActive(true);
        canvasObject.SetActive(true);
        HoldPause();
    }

    private void BeginCountdown()
    {
        if (phase != Phase.Intro || current == null) return;
        introPanel.SetActive(false);
        SetPlayUIVisible(true);
        instructionText.text = current.Instruction;
        current.Begin(playArea);
        UpdateTimerUI();

        phase = Phase.Countdown;
        countdownLeft = countdownStep * 3f;
        countdownText.gameObject.SetActive(true);
        countdownText.transform.SetAsLastSibling();
        countdownText.color = Color.white;
        countdownText.text = "3";
    }

    private void SetPlayUIVisible(bool visible)
    {
        instructionText.gameObject.SetActive(visible);
        timerText.gameObject.SetActive(visible);
        timerFill.parent.gameObject.SetActive(visible);
        playArea.gameObject.SetActive(visible);
        countdownText.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------
    // 毎フレーム
    // ------------------------------------------------------------

    private void Update()
    {
        TrySubscribe();
        TryStartPending();
        HandleDebugKeys();
        if (current == null) return;

        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        switch (phase)
        {
            case Phase.Countdown:
                countdownLeft -= dt;
                if (countdownLeft > 0f)
                {
                    countdownText.text = Mathf.CeilToInt(countdownLeft / countdownStep).ToString();
                }
                else
                {
                    phase = Phase.Playing;
                    startFlash = 0.6f;
                    countdownText.text = "スタート！";
                }
                break;

            case Phase.Playing:
                current.Tick(dt);
                UpdateTimerUI();
                if (startFlash > 0f)
                {
                    startFlash -= dt;
                    var c = Color.white;
                    c.a = Mathf.Clamp01(startFlash / 0.6f);
                    countdownText.color = c;
                    if (startFlash <= 0f) countdownText.gameObject.SetActive(false);
                }
                break;

            case Phase.Finished:
                if (resultTimer > 0f)
                {
                    resultTimer -= dt;
                    if (resultTimer <= 0f) ShowResult();
                }
                break;
        }
    }

    private void HandleDebugKeys()
    {
#if ENABLE_INPUT_SYSTEM
        if (!debugKeys || !Application.isPlaying) return;
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.f8Key.wasPressedThisFrame) StartGame(MiniGameKind.ミルクの温度合わせ);
        if (kb.f9Key.wasPressedThisFrame) StartGame(MiniGameKind.寝かしつけ);
        if (kb.f10Key.wasPressedThisFrame) StartGame(MiniGameKind.ミルクを飲ませる);
#endif
    }

    [ContextMenu("テスト：ミルクの温度合わせを始める")]
    private void Test_Milk() { if (Application.isPlaying) StartGame(MiniGameKind.ミルクの温度合わせ); }

    [ContextMenu("テスト：寝かしつけを始める")]
    private void Test_Sleep() { if (Application.isPlaying) StartGame(MiniGameKind.寝かしつけ); }

    [ContextMenu("テスト：ミルクを飲ませるを始める")]
    private void Test_Feeding() { if (Application.isPlaying) StartGame(MiniGameKind.ミルクを飲ませる); }

    // ------------------------------------------------------------
    // 結果
    // ------------------------------------------------------------

    private void HandleFinished(MiniGameRank rank)
    {
        if (current == null) return;
        ApplyEffect(current.EffectFor(rank));
        phase = Phase.Finished;
        resultTimer = Mathf.Max(0.01f, resultDelay);
        countdownText.gameObject.SetActive(false);
    }

    private void ShowResult()
    {
        var rank = current.Rank;
        rankText.text = rank.ToString();
        switch (rank)
        {
            case MiniGameRank.大成功: rankText.color = new Color(0.95f, 0.65f, 0.05f); break;
            case MiniGameRank.成功:   rankText.color = new Color(0.2f, 0.65f, 0.35f); break;
            default:                   rankText.color = new Color(0.45f, 0.5f, 0.65f); break;
        }
        messageText.text = current.ResultMessage(rank);
        effectText.text = current.EffectFor(rank).Describe();
        resultPanel.SetActive(true);
    }

    private void ApplyEffect(MiniGameEffect e)
    {
        var hh = Household.I;
        if (hh == null) return;
        var child = hh.Child;
        if (child != null)
        {
            if (!Mathf.Approximately(e.childStress, 0f)) child.AddStress(e.childStress);
            if (!Mathf.Approximately(e.childHealth, 0f)) child.AddHealth(e.childHealth);
        }
        if (!Mathf.Approximately(e.parentStress, 0f))
        {
            foreach (var p in hh.Parents) p.AddStress(e.parentStress);
        }
    }

    private void CloseGame()
    {
        current = null;
        phase = Phase.None;
        MiniGameUIKit.ClearChildren(playArea);
        MiniGameUIKit.ClearChildren(introArtArea);
        if (resultPanel != null) resultPanel.SetActive(false);
        if (canvasObject != null) canvasObject.SetActive(false);
        ReleasePause();
    }

    // ------------------------------------------------------------
    // 枠の UI
    // ------------------------------------------------------------

    private void UpdateTimerUI()
    {
        if (current == null) return;
        float ratio = current.TimeLimit > 0f ? current.Remaining / current.TimeLimit : 0f;
        MiniGameUIKit.SetBar(timerFill, ratio);
        timerText.text = $"あと {Mathf.CeilToInt(current.Remaining)} 秒";
        timerFillImage.color = current.Remaining <= 8f ? new Color(0.9f, 0.3f, 0.25f) : new Color(0.3f, 0.7f, 0.4f);
    }

    private void EnsureUI()
    {
        if (canvasObject != null) return;

        canvasObject = new GameObject("MiniGameCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;   // 他の画面より手前
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        // 暗幕（後ろの画面を触れなくする）
        MiniGameUIKit.MakeImage("Dim", canvasObject.transform, Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.65f), true);

        // ウィンドウ
        var window = MiniGameUIKit.MakeImage("Window", canvasObject.transform, new Vector2(0.1f, 0.08f), new Vector2(0.9f, 0.92f), Cream);
        var w = window.transform;
        titleText = MiniGameUIKit.MakeLabel("Title", w, "", 42, Dark, new Vector2(0f, 0.90f), new Vector2(1f, 1f));
        instructionText = MiniGameUIKit.MakeLabel("Instruction", w, "", 26, Dark, new Vector2(0.03f, 0.83f), new Vector2(0.97f, 0.90f));
        timerText = MiniGameUIKit.MakeLabel("TimerText", w, "", 26, Dark, new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.83f));
        timerFill = MiniGameUIKit.MakeBar("TimerBar", w, new Vector2(0.05f, 0.745f), new Vector2(0.95f, 0.78f),
            new Color(0.85f, 0.85f, 0.88f), new Color(0.3f, 0.7f, 0.4f));
        timerFillImage = timerFill.GetComponent<Image>();
        playArea = MiniGameUIKit.MakeRect("PlayArea", w, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.73f));
        playArea.gameObject.AddComponent<RectMask2D>();   // 背景のはみ出しを切る
        countdownText = MiniGameUIKit.MakeLabel("Countdown", w, "", 120, Dark, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.73f));
        // どの背景の上でも読めるよう、白文字に濃い縁取り
        countdownText.color = Color.white;
        countdownText.outlineWidth = 0.25f;
        countdownText.outlineColor = new Color32(43, 34, 51, 255);

        BuildIntroPanel(w);
        BuildResultPanel(w);

        canvasObject.SetActive(false);
    }

    private void BuildIntroPanel(Transform w)
    {
        var ip = MiniGameUIKit.MakeImage("Intro", w, new Vector2(0f, 0f), new Vector2(1f, 0.89f), Cream, true);
        introPanel = ip.gameObject;
        var t = ip.transform;

        // 左：このゲームの絵
        var artBack = MiniGameUIKit.MakeImage("ArtBack", t, new Vector2(0.03f, 0.18f), new Vector2(0.45f, 0.97f), new Color(0.93f, 0.89f, 0.8f));
        introArtArea = MiniGameUIKit.MakeRect("ArtArea", artBack.transform, Vector2.zero, Vector2.one);
        introArtArea.gameObject.AddComponent<RectMask2D>();

        // 右上：あそびかた
        MiniGameUIKit.MakeLabel("HowToHeader", t, "あそびかた", 30, new Color(0.85f, 0.45f, 0.15f),
            new Vector2(0.48f, 0.88f), new Vector2(0.97f, 0.97f), TextAlignmentOptions.Left);
        introStepsText = MiniGameUIKit.MakeLabel("HowTo", t, "", 23, Dark,
            new Vector2(0.48f, 0.53f), new Vector2(0.97f, 0.88f), TextAlignmentOptions.TopLeft);
        introStepsText.lineSpacing = 12f;
        // 文章が長くても枠からあふれないよう自動で縮める
        introStepsText.enableAutoSizing = true;
        introStepsText.fontSizeMin = 16f;
        introStepsText.fontSizeMax = 23f;

        // 右下：まめちしき
        var tip = MiniGameUIKit.MakeImage("TipBox", t, new Vector2(0.48f, 0.18f), new Vector2(0.97f, 0.51f), new Color(0.87f, 0.95f, 0.86f));
        MiniGameUIKit.MakeLabel("TipHeader", tip.transform, "まめちしき", 24, new Color(0.15f, 0.5f, 0.2f),
            new Vector2(0.04f, 0.80f), new Vector2(0.96f, 0.97f), TextAlignmentOptions.Left);
        introTipText = MiniGameUIKit.MakeLabel("Tip", tip.transform, "", 19, Dark,
            new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.80f), TextAlignmentOptions.TopLeft);
        introTipText.enableAutoSizing = true;
        introTipText.fontSizeMin = 14f;
        introTipText.fontSizeMax = 19f;

        // 下：スタート
        MiniGameUIKit.MakeLabel("Note", t, "※ 失敗してもゲームオーバーにはなりません", 18, new Color(0.45f, 0.4f, 0.38f),
            new Vector2(0.03f, 0.03f), new Vector2(0.36f, 0.15f), TextAlignmentOptions.Left);
        MiniGameUIKit.MakeButton("StartButton", t, "スタート", 40, new Color(0.3f, 0.65f, 0.4f),
            new Vector2(0.37f, 0.03f), new Vector2(0.63f, 0.15f), BeginCountdown);

        introPanel.SetActive(false);
    }

    private void BuildResultPanel(Transform w)
    {
        var rp = MiniGameUIKit.MakeImage("Result", w, Vector2.zero, Vector2.one, new Color(0.99f, 0.96f, 0.88f, 0.97f), true);
        resultPanel = rp.gameObject;
        rankText = MiniGameUIKit.MakeLabel("Rank", rp.transform, "", 100, Dark, new Vector2(0.05f, 0.62f), new Vector2(0.95f, 0.92f));
        messageText = MiniGameUIKit.MakeLabel("Message", rp.transform, "", 36, Dark, new Vector2(0.05f, 0.42f), new Vector2(0.95f, 0.62f));
        effectText = MiniGameUIKit.MakeLabel("Effect", rp.transform, "", 30, new Color(0.35f, 0.3f, 0.28f),
            new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.42f));
        MiniGameUIKit.MakeButton("OkButton", rp.transform, "OK", 40, new Color(0.3f, 0.65f, 0.4f),
            new Vector2(0.38f, 0.05f), new Vector2(0.62f, 0.17f), CloseGame);
        resultPanel.SetActive(false);
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }
}
