using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 家計簿の［月］タブ表示（expenses.md 5.1・依頼E5）。
/// </summary>
public class LedgerMonthView : MonoBehaviour
{
    [Header("ヘッダー")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button prevMonthButton;
    [SerializeField] private Button nextMonthButton;

    [Header("スクロール一覧")]
    [SerializeField] private RectTransform incomeContent;
    [SerializeField] private RectTransform expenseContent;
    [SerializeField] private LedgerRowUI rowPrefab;

    [Header("下部サマリー")]
    [SerializeField] private TMP_Text balanceSummaryText;
    [SerializeField] private TMP_Text childCostSummaryText;

    private int selectedMonthIndex = -1;
    private readonly List<LedgerRowUI> spawnedRows = new List<LedgerRowUI>();

    private void Awake()
    {
        if (prevMonthButton != null) prevMonthButton.onClick.AddListener(OnPrevMonthClicked);
        if (nextMonthButton != null) nextMonthButton.onClick.AddListener(OnNextMonthClicked);
    }

    public void Show(int targetMonthIndex, LedgerPanelUI panel)
    {
        selectedMonthIndex = targetMonthIndex;
        Refresh(panel);
    }

    private void OnPrevMonthClicked()
    {
        int oldest = Ledger.I.OldestMonthIndex;
        if (selectedMonthIndex > oldest)
        {
            selectedMonthIndex--;
            Refresh(GetComponentInParent<LedgerPanelUI>());
        }
    }

    private void OnNextMonthClicked()
    {
        int current = Ledger.CurrentMonthIndex();
        if (selectedMonthIndex < current)
        {
            selectedMonthIndex++;
            Refresh(GetComponentInParent<LedgerPanelUI>());
        }
    }

    public void Refresh(LedgerPanelUI panel)
    {
        if (selectedMonthIndex < 0)
        {
            selectedMonthIndex = Ledger.CurrentMonthIndex();
        }

        int currentMax = Ledger.CurrentMonthIndex();
        int oldest = Ledger.I.OldestMonthIndex;

        // 範囲外なら丸める
        selectedMonthIndex = Mathf.Clamp(selectedMonthIndex, oldest, currentMax);

        // タイトル
        int age = selectedMonthIndex / 12;
        int month = (selectedMonthIndex % 12) + 1;
        if (titleText != null)
        {
            titleText.text = $"{age}歳 {month}月";
        }

        if (prevMonthButton != null) prevMonthButton.interactable = selectedMonthIndex > oldest;
        if (nextMonthButton != null) nextMonthButton.interactable = selectedMonthIndex < currentMax;

        ClearRows();

        var lines = Ledger.I.LinesOfMonth(selectedMonthIndex);
        var p = panel != null ? panel : GetComponentInParent<LedgerPanelUI>();

        Color incColor = p != null ? p.IncomeColor : new Color(0.18f, 0.49f, 0.20f);
        Color expColor = p != null ? p.ExpenseColor : new Color(0.78f, 0.16f, 0.16f);
        Color supColor = p != null ? p.SupportColor : new Color(0.08f, 0.40f, 0.75f);
        Color headColor = p != null ? p.HeaderColor : new Color(0.20f, 0.20f, 0.20f);
        Color normColor = p != null ? p.NormalColor : new Color(0.25f, 0.25f, 0.25f);

        int grossSalary = 0;
        int taxTotal = 0;
        int supportIncome = 0;
        int expenseTotal = 0;
        int coveredTotal = 0;
        int childCostTotal = 0;

        // ------------------------------------------------------------
        // 左：収入・天引き・支援
        // ------------------------------------------------------------
        if (incomeContent != null && rowPrefab != null)
        {
            // 給料
            foreach (var l in lines)
            {
                if (l.category == ExpenseCategory.給料 && l.amount > 0)
                {
                    grossSalary += l.amount;
                    SpawnRow(incomeContent, l.source, $"{l.amount:N0}", null, incColor, false);
                }
            }

            // 税金・社会保険
            foreach (var l in lines)
            {
                if (l.category == ExpenseCategory.税金社会保険)
                {
                    taxTotal += l.amount; // 通常は負数
                    string amtStr = l.amount < 0 ? $"−{-l.amount:N0}" : $"+{l.amount:N0}";
                    Color c = l.amount < 0 ? expColor : incColor;
                    SpawnRow(incomeContent, l.source, amtStr, null, c, false);
                }
            }

            // 支援制度
            foreach (var l in lines)
            {
                if (l.category == ExpenseCategory.支援制度 && l.amount > 0)
                {
                    supportIncome += l.amount;
                    SpawnRow(incomeContent, l.source, $"+{l.amount:N0}", null, supColor, false);
                }
            }
        }

        // ------------------------------------------------------------
        // 右：支出（種類ごとに見出し＋小計＋行）
        // ------------------------------------------------------------
        if (expenseContent != null && rowPrefab != null)
        {
            ExpenseCategory[] expenseCategories = new[]
            {
                ExpenseCategory.住居,
                ExpenseCategory.光熱費,
                ExpenseCategory.通信,
                ExpenseCategory.食費,
                ExpenseCategory.保育,
                ExpenseCategory.学校,
                ExpenseCategory.塾習い事,
                ExpenseCategory.医療,
                ExpenseCategory.その他
            };

            foreach (var cat in expenseCategories)
            {
                var catLines = new List<LedgerLine>();
                int catSubtotal = 0;

                foreach (var l in lines)
                {
                    if (l.category != cat) continue;
                    // 0円で肩代わりもない行は出さない
                    if (l.amount == 0 && l.covered == 0) continue;

                    catLines.Add(l);
                    int cost = Math.Abs(l.amount);
                    catSubtotal += cost;
                    expenseTotal += cost;
                    if (Ledger.IsChildCost(cat)) childCostTotal += cost;
                }

                if (catLines.Count == 0) continue;

                // 種類見出し行（小計つき）
                SpawnRow(expenseContent, cat.ToString(), $"{catSubtotal:N0}", null, headColor, true);

                // 個別行
                foreach (var l in catLines)
                {
                    int cost = Math.Abs(l.amount);
                    string note = l.covered > 0 ? $"（支援 −{l.covered:N0}円）" : null;
                    SpawnRow(expenseContent, $"  {l.source}", $"{cost:N0}", note, expColor, false);
                }
            }
        }

        // 全体の covered 合計
        foreach (var l in lines)
        {
            coveredTotal += l.covered;
        }

        // ------------------------------------------------------------
        // 下部サマリー
        // ------------------------------------------------------------
        int takeHome = grossSalary + taxTotal; // taxTotal は負数なので足し算で手取り
        int balance = takeHome + supportIncome - expenseTotal;
        string balanceSign = balance >= 0 ? $"+{balance:N0}" : $"−{-balance:N0}";

        if (balanceSummaryText != null)
        {
            balanceSummaryText.text = $"手取り {takeHome:N0} ＋支援 {supportIncome:N0} − 支出 {expenseTotal:N0} ＝ 差し引き {balanceSign}";
        }

        if (childCostSummaryText != null)
        {
            childCostSummaryText.text = $"支援で助かった額 {coveredTotal:N0}   子育てにかかったお金 {childCostTotal:N0}";
        }
    }

    private void SpawnRow(Transform parent, string name, string amount, string note, Color color, bool isHeader)
    {
        var row = Instantiate(rowPrefab, parent);
        row.gameObject.SetActive(true);
        row.Setup(name, amount, note, color, isHeader);
        spawnedRows.Add(row);
    }

    private void ClearRows()
    {
        foreach (var r in spawnedRows)
        {
            if (r != null) Destroy(r.gameObject);
        }
        spawnedRows.Clear();
    }
}
