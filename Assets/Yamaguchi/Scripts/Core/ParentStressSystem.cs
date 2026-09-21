using UnityEngine;

/// 1日ごとに、各親のワークライフバランスに応じてストレスを増減させる。
public class ParentStressSystem : MonoBehaviour
{
    [Header("1日あたりのストレス変化")]
    [Tooltip("完全ライフ寄り(0)のとき")]
    [SerializeField] private float stressPerDayWhenLife = -5f;
    [Tooltip("完全ワーク寄り(1)のとき")]
    [SerializeField] private float stressPerDayWhenWork = 5f;

    private void Start()
    {
        if (DayTicker.I == null) { Debug.LogError("ParentStressSystem: DayTicker が見つかりません"); return; }
        DayTicker.I.OnDayPassed += ApplyOneDay;
    }

    private void OnDestroy()
    {
        if (DayTicker.I != null) DayTicker.I.OnDayPassed -= ApplyOneDay;
    }

    private void ApplyOneDay(int day)
    {
        if (Household.I == null) return;
        Apply(Household.I.Mother);
        Apply(Household.I.Father);
    }

    private void Apply(ParentStatus p)
    {
        if (p == null) return;
        // 0(ライフ) → life値 ／ 1(ワーク) → work値 を直線で補間
        float delta = Mathf.Lerp(stressPerDayWhenLife, stressPerDayWhenWork, p.WorkLifeBalance);
        p.AddStress(delta);
    }
}