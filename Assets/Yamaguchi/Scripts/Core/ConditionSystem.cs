using UnityEngine;

/// 毎日1回、全員の状態（Condition）の残り日数を減らして期限切れを外す。
/// Trait は期限が無いのでなめない。traits.md 4章。
public class ConditionSystem : MonoBehaviour
{
    private void Start()
    {
        if (DayTicker.I == null)
        {
            Debug.LogError("ConditionSystem: DayTicker が見つかりません");
            return;
        }
        DayTicker.I.OnDayPassed += TickAll;
    }

    private void OnDestroy()
    {
        if (DayTicker.I != null) DayTicker.I.OnDayPassed -= TickAll;
    }

    private void TickAll(int day)
    {
        if (Household.I == null) return;
        foreach (var member in Household.I.Members)
        {
            member.Conditions.TickDay();
        }
    }
}
