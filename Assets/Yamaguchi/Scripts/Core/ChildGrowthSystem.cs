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
    [Tooltip("遊び寄り(b=1)のときの成績変化。マイナスにして、遊びに振ると成績が下がっていくようにする（0 だと遊び側で成績が全く動かなかった）")]
    [SerializeField] private float gradePerMonthAtPlay = -3f;
    [Tooltip("体調が 0 のときの成績の伸びの倍率（体調 100 で 1.0。伸びが止まりきらないよう下限を持たせる）")]
    [Range(0f, 1f)][SerializeField] private float minHealthGradeFactor = 0.5f;
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

    // ─── 子ストレスの係数（1日あたり） ────────────────────────
    [Header("ストレス Lerp係数（1日あたり・たたき台）")]
    [Tooltip("勉強寄り(b=0)のときの子ストレス変化（1日あたり）")]
    [SerializeField] private float stressPerDayAtStudy = 5f;
    [Tooltip("遊び寄り(b=1)のときの子ストレス変化（1日あたり）")]
    [SerializeField] private float stressPerDayAtPlay = -5f;
    [Tooltip("ライフ寄り(w=0)の親の影響（子ストレス、1日あたり）")]
    [SerializeField] private float stressPerDayParentLife = -1f;
    [Tooltip("ワーク寄り(w=1)の親の影響（子ストレス、1日あたり）")]
    [SerializeField] private float stressPerDayParentWork = 2f;
    [Tooltip("親の平均ストレスがこの値を超えたとき、子ストレスへの追加分（1日あたり）")]
    [SerializeField] private float stressPerDayParentOverloadBonus = 1f;
    [Tooltip("この値を超えた親の平均ストレスを「過負荷」と見なす閾値")]
    [SerializeField] private float parentStressOverloadThreshold = 70f;

    // ─── 体調の係数 ──────────────────────────────────────────
    [Header("体調（1ヶ月あたり・たたき台）")]
    [Tooltip("ゲーム開始時（0歳0日）の体調。赤ちゃんは低めから始まる")]
    [Range(0f, 100f)][SerializeField] private float initialHealth = 40f;
    [Tooltip("0歳のときの体調の目標値。体調はこの値まで自然に回復する（これより高くは引き下げない）")]
    [Range(0f, 100f)][SerializeField] private float healthTargetAtBirth = 45f;
    [Tooltip("安定したあとの体調の目標値")]
    [Range(0f, 100f)][SerializeField] private float healthTargetStable = 85f;
    [Tooltip("体調の目標値が安定値に届く歳。0歳→この歳にかけて目標値が直線で上がる（体調が安定していく）")]
    [Min(0.1f)][SerializeField] private float healthStableAgeYears = 4f;
    [Tooltip("体調が目標値より低いときの自然回復量（1ヶ月あたり）")]
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

        // 新しいゲーム（0歳0日）の始まりは、赤ちゃんらしく体調を低めにする
        if (child != null && GameState.I != null && GameState.I.AgeDays < 1f)
            child.SetHealth(initialHealth);

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

        float b = child.StudyPlayBalance;           // 1=勉強 / 0=遊び
        float w = AverageParentWorkLifeBalance();   // 0=ライフ / 1=ワーク
        float avgParentStress = AverageParentStress();

        // ─── 成績 ───────────────────────────────────────────
        // Lerp(+6, -3, b) + Lerp(+2, -2, w) × 体調の係数（プラスのときだけ）
        // 赤ちゃんの間（成績を扱う年齢になるまで）は成績を動かさない。
        if (child.IsGradeActive)
        {
            float gradeDelta = (Mathf.Lerp(gradeAtStudy, gradePerMonthAtPlay, b)
                              + Mathf.Lerp(gradeParentLife, gradeParentWork, w))
                              / DaysPerMonth;
            if (gradeDelta > 0f)
                gradeDelta *= Mathf.Lerp(minHealthGradeFactor, 1f, child.Health / 100f);
            child.AddGrade(gradeDelta);
        }

        // ─── 趣味経験値 ─────────────────────────────────────
        // Lerp(0, +30, b) + Lerp(+2, -2, w)  マイナスにはしない
        float hobbyDelta = (Mathf.Lerp(hobbyExpAtStudy, hobbyExpAtPlay, b)
                           + Mathf.Lerp(hobbyExpParentLife, hobbyExpParentWork, w))
                           / DaysPerMonth;
        hobbyDelta = Mathf.Max(0f, hobbyDelta);
        child.AddHobbyExp(hobbyDelta);

        // ─── 子ストレス（1日あたり） ──────────────────────────
        // Lerp(+5, -5, b) + Lerp(-1, +2, w)  親ストレス>70 なら +1
        float stressDelta = Mathf.Lerp(stressPerDayAtStudy, stressPerDayAtPlay, b)
                          + Mathf.Lerp(stressPerDayParentLife, stressPerDayParentWork, w);
        if (avgParentStress > parentStressOverloadThreshold)
            stressDelta += stressPerDayParentOverloadBonus;
        child.AddStress(stressDelta);

        // ─── 体調 ────────────────────────────────────────────
        // 年齢で上がる目標値まで自然回復（目標より高いときは引き下げない）。子ストレス>70 なら -4
        // → 赤ちゃんの間は低めで、育つにつれて安定していく。
        float target = Mathf.Lerp(healthTargetAtBirth, healthTargetStable,
                                  Mathf.Clamp01(child.AgeYears / healthStableAgeYears));
        float healthDelta = (child.Health < target) ? healthRecovery / DaysPerMonth : 0f;
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
