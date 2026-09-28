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
    [Tooltip("勉強⇔遊びスライダー（値が変わったら ChildStatus.SetStudyPlayBalance を呼ぶ）")]
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

    [Header("参照：バッジ・特性行")]
    [Tooltip("ストレス段階バッジ")]
    [SerializeField] private StressBadgeUI stressBadge;
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

    private const string KeyHobby    = "hobby";
    private const string KeyPassLine = "passLine";
    private const string KeyRunaway  = "runaway";

    // ─── 初期化 ───────────────────────────────────────────
    private void Start()
    {
        child = (Household.I != null) ? Household.I.Child : null;
        if (child == null)
        {
            Debug.LogWarning("ChildCardUI: Household に Child が設定されていません。", this);
            return;
        }

        // スライダーの初期値
        if (studyPlaySlider != null)
            studyPlaySlider.SetValueWithoutNotify(child.StudyPlayBalance);

        // バッジ・特性行のバインド
        stressBadge?.Bind(child);
        traitRows?.Bind(child);

        // 初期表示
        ShowProfile();
        ShowGrade(child.Grade);
        ShowStress(child.Stress);
        ShowHealth(child.Health);
        ShowHobby(child.Hobby, child.HobbyLevel);
        ShowPassLine();

        // 購読開始
        Subscribe();

        // 年齢の購読（プロフィール行の更新に必要）
        if (GameState.I != null)
            GameState.I.OnAgeChanged += _ => ShowProfile();

        // 家出ウォッチャー
        if (runawayWatcher != null)
            runawayWatcher.OnDangerDaysChanged += ShowRunaway;
    }

    private void OnEnable()
    {
        if (child != null) Subscribe();
    }

    private void OnDisable()
    {
        if (child != null) Unsubscribe();
    }

    // ─── 購読 ─────────────────────────────────────────────
    private void Subscribe()
    {
        if (studyPlaySlider != null)
            studyPlaySlider.onValueChanged.AddListener(child.SetStudyPlayBalance);

        child.OnGradeChanged            += ShowGrade;
        child.OnStressChanged           += ShowStress;
        child.OnHealthChanged           += ShowHealth;
        child.OnHobbyChanged            += ShowHobby;
        child.OnStudyPlayBalanceChanged += ShowBalance;
    }

    private void Unsubscribe()
    {
        if (studyPlaySlider != null)
            studyPlaySlider.onValueChanged.RemoveListener(child.SetStudyPlayBalance);

        child.OnGradeChanged            -= ShowGrade;
        child.OnStressChanged           -= ShowStress;
        child.OnHealthChanged           -= ShowHealth;
        child.OnHobbyChanged            -= ShowHobby;
        child.OnStudyPlayBalanceChanged -= ShowBalance;
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

    private void ShowBalance(float _)
    {
        // スライダーは onValueChanged から SetStudyPlayBalance を呼ぶので
        // ここでは通知の受け取りのみ（必要なら他の表示に使う）
    }

    private void ShowHobby(HobbyKind hobby, int level)
    {
        if (statusList == null) return;
        if (hobby == HobbyKind.なし)
            statusList.RemoveStatus(KeyHobby);
        else
            statusList.SetStatus(KeyHobby, "趣味", $"{hobby} Lv{level}");
    }

    private void ShowPassLine()
    {
        if (statusList == null || child == null) return;
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
