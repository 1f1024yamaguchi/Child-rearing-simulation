using UnityEngine;

/// 毎日1回、子どものステータスを成長式に従って更新するシステム。
/// child-status.md 3章「勉強⇔遊びバランスの数値」の計算式を実装。
/// すべての数値は Inspector から変更できる（たたき台）。
public class ChildGrowthSystem : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("成長を適用する子どものステータス（Household.Child から自動取得）")]
    [SerializeField] private ChildStatus child;

    // ─── 成績の係数（月あたり → ÷30 して毎日適用） ──────────────
    [Header("成績 Lerp係数（1ヶ月あたり・たたき台）")]
    [Tooltip("勉強寄り(b=0)のときの成績変化")]
    [SerializeField] private float gradeAtStudy = 6f;
    [Tooltip("遊び寄り(b=1)のときの成績変化")]
    [SerializeField] private float gradeAtPlay = 0f;
    [Tooltip("ライフ寄り(w=0)の親の影響（成績）")]
    [SerializeField] private float gradeParentLife = 2f;
    [Tooltip("ワーク寄り(w=1)の親の影響（成績）")]
    [SerializeField] private float gradeParentWork = -2f;

    // ─── 趣味経験値の係数 ──────────────────────────────────────
    [Header("趣味経験値（1ヶ月あたり・たたき台）")]
    [Tooltip("勉強寄り(b=0)のときの経験値変化")]
    [SerializeField] private float hobbyExpAtStudy = 0f;
    [Tooltip("遊び寄り(b=1)のときの経験値変化")]
    [SerializeField] private float hobbyExpAtPlay = 30f;
    [Tooltip("ライフ寄り(w=0)の親の影響（趣味）")]
    [SerializeField] private float hobbyExpParentLife = 2f;
    [Tooltip("ワーク寄り(w=1)の親の影響（趣味）")]
    [SerializeField] private float hobbyExpParentWork = -2f;

    // ─── 子ストレスの係数 ──────────────────────────────────────
    [Header("ストレス（1ヶ月あたり・たたき台）")]
    [Tooltip("勉強寄り(b=0)のときの子ストレス変化")]
    [SerializeField] private float stressAtStudy = 6f;
    [Tooltip("遊び寄り(b=1)のときの子ストレス変化")]
    [SerializeField] private float stressAtPlay = -5f;
    [Tooltip("ライフ寄り(w=0)の親の影響（子ストレス）")]
    [SerializeField] private float stressParentLife = -3f;
    [Tooltip("ワーク寄り(w=1)の親の影響（子ストレス）")]
    [SerializeField] private float stressParentWork = 6f;
    [Tooltip("親の平均ストレスがこの値を超えたとき、子ストレスへの追加分")]
    [SerializeField] private float stressParentOverloadBonus = 3f;
    [Tooltip("この値を超えた親の平均ストレスを「過負荷」と見なす閾値")]
    [SerializeField] private float parentStressOverloadThreshold = 70f;

    // ─── 体調の係数 ──────────────────────────────────────────
    [Header("体調（1ヶ月あたり・たたき台）")]
    [Tooltip("毎日の自然回復量")]
    [SerializeField] private float healthRecovery = 2f;
    [Tooltip("子ストレスがこの値を超えたとき、体調への追加分（マイナス）")]
    [SerializeField] private float healthStressPenalty = -4f;
    [Tooltip("体調に影響する子ストレスの閾値")]
    [SerializeField] private float childStressHealthThreshold = 70f;

    private const float DaysPerMonth = 30f;

    private void Start()
    {
        // child が Inspector 未設定なら Household から自動取得
        if (child == null && Household.I != null)
            child = Household.I.Child;

        if (DayTicker.I == null)
        {
            Debug.LogError("ChildGrowthSystem: DayTicker が見つかりません");
            return;
        }
        DayTicker.I.OnDayPassed += ApplyOneDay;
    }

    private void OnDestroy()
    {
        if (DayTicker.I != null) DayTicker.I.OnDayPassed -= ApplyOneDay;
    }

    private void ApplyOneDay(int day)
    {
        if (child == null)
        {
            // 実行中に遅れて設定された場合の再取得
            if (Household.I != null) child = Household.I.Child;
            if (child == null) return;
        }

        float b = child.StudyPlayBalance;           // 0=勉強 / 1=遊び
        float w = AverageParentWorkLifeBalance();   // 0=ライフ / 1=ワーク
        float avgParentStress = AverageParentStress();

        // ─── 成績 ───────────────────────────────────────────
        // Lerp(+6, 0, b) + Lerp(+2, -2, w) × 体調/100（プラスのときだけ）
        float gradeDelta = (Mathf.Lerp(gradeAtStudy, gradeAtPlay, b)
                          + Mathf.Lerp(gradeParentLife, gradeParentWork, w))
                          / DaysPerMonth;
        if (gradeDelta > 0f)
            gradeDelta *= child.Health / 100f;
        child.AddGrade(gradeDelta);

        // ─── 趣味経験値 ─────────────────────────────────────
        // Lerp(0, +30, b) + Lerp(+2, -2, w)  マイナスにはしない
        float hobbyDelta = (Mathf.Lerp(hobbyExpAtStudy, hobbyExpAtPlay, b)
                           + Mathf.Lerp(hobbyExpParentLife, hobbyExpParentWork, w))
                           / DaysPerMonth;
        hobbyDelta = Mathf.Max(0f, hobbyDelta);
        child.AddHobbyExp(hobbyDelta);

        // ─── 子ストレス ──────────────────────────────────────
        // Lerp(+6, -5, b) + Lerp(-3, +6, w)  親ストレス>70 なら +3
        float stressDelta = (Mathf.Lerp(stressAtStudy, stressAtPlay, b)
                            + Mathf.Lerp(stressParentLife, stressParentWork, w))
                            / DaysPerMonth;
        if (avgParentStress > parentStressOverloadThreshold)
            stressDelta += stressParentOverloadBonus / DaysPerMonth;
        child.AddStress(stressDelta);

        // ─── 体調 ────────────────────────────────────────────
        // 自然回復 +2  子ストレス>70 なら -4
        float healthDelta = healthRecovery / DaysPerMonth;
        if (child.Stress > childStressHealthThreshold)
            healthDelta += healthStressPenalty / DaysPerMonth;
        child.AddHealth(healthDelta);
    }

    /// Household から親2人のライフ⇔ワーク平均を求める。
    private float AverageParentWorkLifeBalance()
    {
        if (Household.I == null) return 0.5f;
        return Household.I.AverageParentWorkLifeBalance;
    }

    /// Household から親2人の平均ストレスを求める。
    private float AverageParentStress()
    {
        if (Household.I == null) return 0f;
        return Household.I.AverageParentStress;
    }
}
