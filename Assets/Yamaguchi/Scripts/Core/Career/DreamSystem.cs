using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 将来の夢を決めて持つ（ending.md 5.3）。セーブキー "dream"。
///
/// - 中1の4月1日（たたき台。Inspector で変えられる）になったら1回だけ、全部の職業で CareerJudge を回し、
///   点数が一番高い職業を夢にする。ランダムにはしない。
/// - 夢が決まるまでプレイヤーには見せない（2026-11-18 決定）。夢のイベントは、条件「将来の夢」
///   （DreamCondition）を付けたイベントとして作る。文章の {dream} が夢の職業名になる。
/// - 本編シーンでは StoryBootstrap が "Story" オブジェクトに自動で付ける。
/// - 職業のアセットは Resources/Careers/ から全部読む（アセット名の順。同点は先の方）。
/// </summary>
public class DreamSystem : MonoBehaviour, ISaveParticipant
{
    public const string CareersResourcePath = "Careers";

    private static DreamSystem instance;

    /// <summary>本編シーンにある DreamSystem。無ければ null。</summary>
    public static DreamSystem I => instance;

    [Header("夢が決まる日（たたき台：中1の4月1日）")]
    [SerializeField] private int decideAgeYears = 12;
    [SerializeField] private int decideMonth = 4;
    [SerializeField] private int decideDay = 1;

    [Header("確認用（Inspector で見るだけ）")]
    [SerializeField] private string dreamCareerId = "";
    [SerializeField] private int decidedAgeDay = -1;

    private readonly List<CareerDefinition> careers = new List<CareerDefinition>();
    private CareerDefinition fallback;
    private CareerDefinition dream;
    private bool loaded;
    private bool subscribed;

    /// <summary>夢が決まったとき（決め直しでも出る）。</summary>
    public event Action<CareerDefinition> OnDreamDecided;

    /// <summary>夢の職業。まだ決まっていなければ null。</summary>
    public CareerDefinition Dream => dream;
    public bool HasDream => dream != null;

    /// <summary>夢が決まった日（通算日数）。まだなら -1。</summary>
    public int DecidedAgeDay => decidedAgeDay;

    /// <summary>夢が決まる日（通算日数）。</summary>
    public int DecideAgeDays => GameDate.ToAgeDays(decideAgeYears, decideMonth, decideDay);

    /// <summary>全部の職業（Resources/Careers）。</summary>
    public IReadOnlyList<CareerDefinition> AllCareers { get { LoadCareers(); return careers; } }

    /// <summary>既定の職業（isFallback の付いたもの。会社員）。</summary>
    public CareerDefinition Fallback { get { LoadCareers(); return fallback; } }

    public string SaveKey => "dream";

    /// <summary>家族（10）のあと。職業のアセットを引くだけなので早くてよい。</summary>
    public int RestoreOrder => 70;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[DreamSystem] DreamSystem が2つあります。後から来たほうを外します。", this);
            Destroy(this);
            return;
        }
        instance = this;
    }

    private void OnEnable()
    {
        SaveSystem.Register(this);
        Subscribe();
    }

    private void Start() => Subscribe();

    private void OnDisable()
    {
        SaveSystem.Unregister(this);
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (instance == this) instance = null;
    }

    private void Subscribe()
    {
        if (subscribed || DayTicker.I == null) return;
        DayTicker.I.OnDayPassed += HandleDayPassed;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (DayTicker.I != null) DayTicker.I.OnDayPassed -= HandleDayPassed;
        subscribed = false;
    }

    private void HandleDayPassed(int ageDay)
    {
        // 「== その日」にしない：その日より後のセーブ（夢の仕組みができる前のもの）でも1回は決まるように
        if (dream == null && ageDay >= DecideAgeDays) DecideDreamNow();
    }

    /// <summary>
    /// いまのステータスで夢を決める（決まっていても決め直す）。決まった職業を返す。
    /// 夢を決め直すイベントを作るときはここを呼ぶ。
    /// </summary>
    public CareerDefinition DecideDreamNow()
    {
        LoadCareers();
        var child = Household.I != null ? Household.I.Child : null;
        var input = CareerJudge.Input.From(child, GameContext.Current());
        var picked = CareerJudge.Judge(careers, fallback, input);
        if (picked == null)
        {
            Debug.LogWarning($"[DreamSystem] 職業のアセットが Resources/{CareersResourcePath} にありません。夢を決められませんでした。");
            return null;
        }

        dream = picked;
        dreamCareerId = picked.id;
        decidedAgeDay = GameState.I != null ? Mathf.FloorToInt(GameState.I.AgeDays) : 0;
        Debug.Log($"[DreamSystem] 将来の夢が「{picked.DisplayNameOrName}」に決まりました。\n{CareerJudge.Explain(careers, input)}");
        OnDreamDecided?.Invoke(picked);
        return picked;
    }

    /// <summary>
    /// 夢を指定の職業にする（ストーリーウィンドウの再生中タブ・確認用）。null で「まだ決まっていない」に戻す。
    /// 本番の決め方は DecideDreamNow（点数が一番高い職業）。
    /// </summary>
    public void SetDream(CareerDefinition career)
    {
        dream = career;
        dreamCareerId = career != null ? career.id : "";
        decidedAgeDay = career != null && GameState.I != null ? Mathf.FloorToInt(GameState.I.AgeDays) : -1;
        if (career != null) OnDreamDecided?.Invoke(career);
    }

    /// <summary>id から職業を引く（セーブの復元・条件の判定用）。</summary>
    public CareerDefinition FindCareer(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        LoadCareers();
        return careers.Find(c => c != null && c.id == id);
    }

    private void LoadCareers()
    {
        if (loaded) return;
        loaded = true;
        careers.Clear();
        careers.AddRange(Resources.LoadAll<CareerDefinition>(CareersResourcePath));
        careers.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        fallback = null;
        foreach (var c in careers)
        {
            if (c == null || !c.isFallback) continue;
            if (fallback == null) fallback = c;
            else Debug.LogWarning($"[DreamSystem] 既定の職業が2つ以上あります（{fallback.name} と {c.name}）。{fallback.name} を使います。");
        }
        if (careers.Count > 0 && fallback == null)
            Debug.LogWarning("[DreamSystem] 既定の職業（isFallback）がありません。条件を満たす職業が無いと夢が決まりません。");
    }

    // ─── セーブ ─────────────────────────────────

    [Serializable]
    private class SaveData
    {
        public string careerId = "";
        public int decidedAgeDay = -1;
    }

    public string Capture()
    {
        return JsonUtility.ToJson(new SaveData { careerId = dream != null ? dream.id : "", decidedAgeDay = decidedAgeDay });
    }

    public void Restore(string json)
    {
        dream = null;
        dreamCareerId = "";
        decidedAgeDay = -1;
        if (string.IsNullOrEmpty(json)) return;

        var d = JsonUtility.FromJson<SaveData>(json);
        if (d == null || string.IsNullOrEmpty(d.careerId)) return;

        dream = FindCareer(d.careerId);
        if (dream == null)
        {
            // 職業のアセットを消した・id を変えた。次の日にもう一度決め直される
            Debug.LogWarning($"[DreamSystem] セーブの夢「{d.careerId}」の職業が見つかりません。");
            return;
        }
        dreamCareerId = dream.id;
        decidedAgeDay = d.decidedAgeDay;
    }

    // ─── 確認用 ─────────────────────────────────

    [ContextMenu("Debug/今のステータスで職業の点数をログ")]
    private void DebugExplain()
    {
        LoadCareers();
        var child = Household.I != null ? Household.I.Child : null;
        Debug.Log("[DreamSystem] 職業の点数\n" + CareerJudge.Explain(careers, CareerJudge.Input.From(child, GameContext.Current())));
    }

    [ContextMenu("Debug/夢を今すぐ決める")]
    private void DebugDecide() => DecideDreamNow();
}
