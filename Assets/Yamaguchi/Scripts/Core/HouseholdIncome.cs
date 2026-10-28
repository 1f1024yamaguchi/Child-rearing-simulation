using UnityEngine;

public class HouseholdIncome : MonoBehaviour
{
    // earners は削除。家族の情報は Household に聞く。

    [Header("手取り（expenses.md 5.5）")]
    [Tooltip("額面に対する手取りの割合。現実の目安は年収や家族構成で 0.75〜0.85。")]
    [Range(0.5f, 1f)] [SerializeField] private float takeHomeRate = 0.8f;

    private static readonly LedgerSource MotherSalary = new LedgerSource("給料（母）", ExpenseCategory.給料);
    private static readonly LedgerSource FatherSalary = new LedgerSource("給料（父）", ExpenseCategory.給料);
    private static readonly LedgerSource TaxSource = new LedgerSource("税金・社会保険", ExpenseCategory.税金社会保険);

    private SupportToast toast;

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
        int gross = mother + father;                                   // 額面
        int net = Mathf.RoundToInt(gross * takeHomeRate);              // 手取り
        GameState.I.AddMoney(net);
        IncomeHistory.I.Record(gross);   // 年収の判定用（直近12か月）。額面のまま

        // 家計簿：額面と、引かれた税金・社会保険（0 円の人は書かない）
        if (mother != 0) Ledger.I.Record(MotherSalary, mother);
        if (father != 0) Ledger.I.Record(FatherSalary, father);
        if (gross - net != 0) Ledger.I.Record(TaxSource, -(gross - net));

        string message = $"給料が入りました（手取り {net:+#,0;-#,0;+0}円）";
        Debug.Log($"{year}歳{month}月 給料日： 母親{mother:N0} + 父親 {father:N0}= 額面 {gross:N0}円 → 手取り +{net:N0}円 → 所持金 {GameState.I.Money:N0}円");

        if (toast == null) toast = FindFirstObjectByType<SupportToast>();
        if (toast != null) toast.Show(message);
        else Debug.Log(message);   // SupportToast がシーンに無いときはログだけ
    }
}