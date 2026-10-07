using UnityEngine;

public class HouseholdIncome : MonoBehaviour
{
    // earners は削除。家族の情報は Household に聞く。

    private void Start()
    {
        if (MonthTicker.I == null)
        {
            Debug.LogError("HouseholdIncome: MonthTicker が見つかりません");
            return;
        }
        MonthTicker.I.OnMonthChanged += Payday;
    }

    private void OnDestroy()
    {
        if (MonthTicker.I != null)
        {
            MonthTicker.I.OnMonthChanged -= Payday;
        }
    }

    private void Payday(int year, int month)
    {
        if (Household.I == null) return;

        int mother = Household.I.Mother != null ? Household.I.Mother.CollectWage() : 0;
        int father = Household.I.Father != null ? Household.I.Father.CollectWage() : 0;
        int income = mother + father;
        GameState.I.AddMoney(income);
        IncomeHistory.I.Record(income);   // 年収の判定用（直近12か月）
        Debug.Log($"{year}歳{month}月 給料日： 母親{mother:N0} + 父親 {father:N0}= +{income:N0}円 → 所持金 {GameState.I.Money:N0}円");
    }
}