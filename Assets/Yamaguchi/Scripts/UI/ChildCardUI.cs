using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 子どもカードの UI スクリプト。
/// ParentCardUI と同じ作り：Start で Household から取得 → 購読 → 表示するだけ。
/// 計算は ChildGrowthSystem が担当するので、ここでは一切しない。
/// child-status.md 6章。
public class ChildCardUI : MonoBehaviour
{
    [Header("参照：子どものステータス")]
    [Tooltip("勉強⇔遊びスライダー（右=勉強 / 左=遊び。値が変わったら反転して ChildStatus.SetStudyPlayBalance を呼ぶ）")]
    [SerializeField] private Slider studyPlaySlider;

    [Tooltip("プロフィール 1行（例：「8歳・スポーツが好き」）")]
    [SerializeField] private TextMeshProUGUI profileLineText;

    [Header("参照：3本のメーター")]
    [Tooltip("成績メーター")]
    [SerializeField] private StatMeterUI gradeMeter;
    [Tooltip("ストレスメーター")]
    [SerializeField] private StatMeterUI stressMeter;
    [Tooltip("体調メーター")]
    [SerializeField] private StatMeterUI healthMeter;

    [Header("参照：特性行")]
    [Tooltip("特性・状態の行を管理する PersonTraitRowsUI")]
    [SerializeField] private PersonTraitRowsUI traitRows;

    [Header("参照：ステータス欄")]
    [Tooltip("趣味・合格ライン・家出カウントなどを出す StatusListUI")]
    [SerializeField] private StatusListUI statusList;

    [Header("参照：家出ウォッチャー（任意）")]
    [Tooltip("設定すると「家出まで○日」の行を出す。省略可")]
    [SerializeField] private RunawayWatcher runawayWatcher;

    // ─── 内部 ─────────────────────────────────────────────
    private ChildStatus child;
    private bool isSubscribed = false;
    private bool? gradeShown = null;   // 成績欄を出しているか（変わったときだけ切り替える）

    private const string KeyHobby    = "hobby";
    private const string KeyPassLine = "passLine";
    private const string KeyRunaway  = "runaway";
    private const string KeyGradeNotice = "gradeNotice";

    // ─── 初期化 ───────────────────────────────────────────
    private void Start()
    {
        child = (Household.I != null) ? Household.I.Child : null;
        if (child == null)
        {
            Debug.LogWarning("ChildCardUI: Household に Child が設定されていません。", this);
            return;
        }

        // スライダーの初期値（画面上は右[1]が勉強、左[0]が遊び）
        if (studyPlaySlider != null)
            studyPlaySlider.SetValueWithoutNotify(1f - child.StudyPlayBalance);

        // 特性行のバインド
        traitRows?.Bind(child);

        // 体調メーターは「体調」（高い＝元気）で表示する。
        // 「体調不良」だとラベルが折り返すため。バーは左が赤・右が緑（低いところから改善していく見た目）。
        if (healthMeter != null)
        {
            healthMeter.SetLabel("体調");
            healthMeter.SetInvertDisplay(false);
            healthMeter.UseHighIsGoodGradient();
        }

        // 初期表示
        ShowProfile();
        ShowGrade(child.Grade);
        ShowStress(child.Stress);
        ShowHealth(child.Health);
        ShowHobby(child.Hobby, child.HobbyLevel);
        RefreshGradeVisibility();

        // 購読開始
        Subscribe();
    }

    private void OnEnable()
    {
        if (child != null) Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    // ─── 購読 ─────────────────────────────────────────────
    private void Subscribe()
    {
        if (isSubscribed) return;

        if (child != null)
        {
            if (studyPlaySlider != null)
                studyPlaySlider.onValueChanged.AddListener(OnSliderValueChanged);

            child.OnGradeChanged            += ShowGrade;
            child.OnStressChanged           += ShowStress;
            child.OnHealthChanged           += ShowHealth;
            child.OnHobbyChanged            += ShowHobby;
            child.OnStudyPlayBalanceChanged += ShowBalance;
        }

        // 年齢の購読（プロフィール行の更新に必要）
        if (GameState.I != null)
            GameState.I.OnAgeChanged += HandleAgeChanged;

        // 家出ウォッチャー
        if (runawayWatcher != null)
            runawayWatcher.OnDangerDaysChanged += ShowRunaway;

        // ストーリーフラグの変更（趣味の呼び名等）
        if (StoryState.I != null)
        {
            StoryState.I.OnFlagChanged += HandleFlagChanged;
            StoryState.I.OnRestored    += HandleStoryRestored;
        }

        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed) return;

        if (child != null)
        {
            if (studyPlaySlider != null)
                studyPlaySlider.onValueChanged.RemoveListener(OnSliderValueChanged);

            child.OnGradeChanged            -= ShowGrade;
            child.OnStressChanged           -= ShowStress;
            child.OnHealthChanged           -= ShowHealth;
            child.OnHobbyChanged            -= ShowHobby;
            child.OnStudyPlayBalanceChanged -= ShowBalance;
        }

        if (GameState.I != null)
            GameState.I.OnAgeChanged -= HandleAgeChanged;

        if (runawayWatcher != null)
            runawayWatcher.OnDangerDaysChanged -= ShowRunaway;

        if (StoryState.I != null)
        {
            StoryState.I.OnFlagChanged -= HandleFlagChanged;
            StoryState.I.OnRestored    -= HandleStoryRestored;
        }

        isSubscribed = false;
    }

    /// <summary>ロードでフラグがまとめて入れ替わったとき（趣味の呼び名を描き直す）</summary>
    private void HandleStoryRestored() => HandleFlagChanged(null, true);

    private void HandleFlagChanged(StoryFlag flag, bool set)
    {
        ShowProfile();
        if (child != null)
        {
            ShowHobby(child.Hobby, child.HobbyLevel);
        }
    }

    private void HandleAgeChanged(float _)
    {
        ShowProfile();
        RefreshGradeVisibility();
    }

    /// 赤ちゃんの間は成績メーターと合格ラインを隠し、代わりに「成績　2歳から」の説明行を出す。
    /// 成績を扱う年齢になったら、説明行を消してメーターと合格ラインを出す。
    private void RefreshGradeVisibility()
    {
        if (child == null) return;
        bool active = child.IsGradeActive;
        if (gradeShown == active) return;
        gradeShown = active;

        if (gradeMeter != null) gradeMeter.gameObject.SetActive(active);
        if (active)
        {
            statusList?.RemoveStatus(KeyGradeNotice);
            ShowPassLine();
        }
        else
        {
            statusList?.RemoveStatus(KeyPassLine);
            int startAge = Mathf.CeilToInt(child.GradeStartAgeYears);
            statusList?.SetStatus(KeyGradeNotice, "成績", $"{startAge}歳から");
        }
    }

    // ─── 表示メソッド ─────────────────────────────────────
    private void ShowProfile()
    {
        if (profileLineText != null && child != null)
            profileLineText.text = child.ProfileLine;
    }

    private void ShowGrade(int grade)
    {
        gradeMeter?.SetValue(grade);
        // 合格ラインの「不足」表示も成績が変わるたびに更新する
        ShowPassLine();
    }

    private void ShowStress(int stress)
    {
        stressMeter?.SetValue(stress);
    }

    private void ShowHealth(int health)
    {
        healthMeter?.SetValue(health);
    }

    private void OnSliderValueChanged(float sliderValue)
    {
        if (child != null)
            child.SetStudyPlayBalance(1f - sliderValue);
    }

    private void ShowBalance(float balance)
    {
        // 外部（イベント等）から値が変わった場合にスライダー位置を同期（右[1]=勉強、左[0]=遊び）
        if (studyPlaySlider != null)
            studyPlaySlider.SetValueWithoutNotify(1f - balance);
    }

    private void ShowHobby(HobbyKind hobby, int level)
    {
        if (statusList == null) return;
        if (hobby == HobbyKind.なし)
            statusList.RemoveStatus(KeyHobby);
        else
        {
            string hobbyName = HobbyDisplayName.Get(hobby);
            statusList.SetStatus(KeyHobby, "趣味", $"{hobbyName} Lv{level}");
        }
    }

    private void ShowPassLine()
    {
        if (statusList == null || child == null) return;
        if (!child.IsGradeActive) return;   // 赤ちゃんの間は合格ラインを出さない
        string suffix = child.IsAbovePassLine ? "" : " 不足";
        statusList.SetStatus(KeyPassLine, "合格ライン", $"{Mathf.RoundToInt(child.PassLine)}%{suffix}");
    }

    /// 家出カウントが変わったときに呼ばれる。
    /// カウントが 1 以上なら「家出まで○日」の行を出し、0 になったら行ごと消す。
    private void ShowRunaway(int dangerDays)
    {
        if (statusList == null) return;
        if (dangerDays <= 0)
        {
            statusList.RemoveStatus(KeyRunaway);
        }
        else
        {
            int remaining = (runawayWatcher != null) ? runawayWatcher.RemainingDays : 0;
            statusList.SetStatus(KeyRunaway, "家出まで", $"{remaining}日");
        }
    }
}
