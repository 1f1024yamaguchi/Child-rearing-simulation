using UnityEngine;

/// 毎日、各親の日給を未払い分に積み立てる。支払いは HouseholdIncome が給料日にまとめて行う。
public class WageAccrual : MonoBehaviour
{
    private void Start()
    {
        if (DayTicker.I == null) { Debug.LogError("WageAccrual: DayTicker が見つかりません"); return; }
        DayTicker.I.OnDayPassed += Accrue;
    }

    private void OnDestroy()
    {
        if (DayTicker.I != null) DayTicker.I.OnDayPassed -= Accrue;
    }

    private void Accrue(int day)
    {
        if (Household.I == null) return;
        if (Household.I.Mother != null) Household.I.Mother.AccrueDailyWage();
        if (Household.I.Father != null) Household.I.Father.AccrueDailyWage();
    }
}