using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 家計簿の［年］タブ表示（expenses.md 5.1・依頼E5）。
/// </summary>
public class LedgerYearView : MonoBehaviour
{
    [Header("ヘッダー")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button prevYearButton;
    [SerializeField] private Button nextYearButton;

    [Header("12か月の支出の棒グラフ")]
    [SerializeField] private RectTransform barChartContainer;
    [SerializeField] private GameObject barPrefab; // 棒プレハブ（または12本のプレースホルダ）
    [SerializeField] private float maxBarHeight = 40f;

    [Header("スクロール一覧")]
    [SerializeField] private RectTransform incomeContent;
    [SerializeField] private RectTransform expenseContent;
    [SerializeField] private LedgerRowUI rowPrefab;

    [Header("下部サマリー")]
    [SerializeField] private TMP_Text balanceSummaryText;
    [SerializeField] private TMP_Text childCostSummaryText;

    private int selectedAge = -1;
    private readonly HashSet<ExpenseCategory> expandedCategories = new HashSet<ExpenseCategory>();
    private readonly List<LedgerRowUI> spawnedRows = new List<LedgerRowUI>();
    private readonly List<GameObject> spawnedBars = new List<GameObject>();

    private void Awake()
    {
        if (prevYearButton != null) prevYearButton.onClick.AddListener(OnPrevYearClicked);
        if (nextYearButton != null) nextYearButton.onClick.AddListener(OnNextYearClicked);
    }

    public void Show(int targetAge, LedgerPanelUI panel)
    {
        selectedAge = targetAge;
        Refresh(panel);
    }

    private void OnPrevYearClicked()
    {
        if (selectedAge > 0)
        {
            selectedAge--;
            Refresh(GetComponentInParent<LedgerPanelUI>());
        }
    }

    private void OnNextYearClicked()
    {
        int currentMaxAge = Ledger.CurrentMonthIndex() / 12;
        if (selectedAge < currentMaxAge)
        {
            selectedAge++;
            Refresh(GetComponentInParent<LedgerPanelUI>());
        }
    }

    public void Refresh(LedgerPanelUI panel)
    {
        int currentMaxAge = Ledger.CurrentMonthIndex() / 12;
        if (selectedAge < 0)
        {
            selectedAge = currentMaxAge;
        }

        selectedAge = Mathf.Clamp(selectedAge, 0, currentMaxAge);

        if (titleText != null)
        {
            titleText.text = $"{selectedAge}歳の1年（1月〜12月）";
        }

        if (prevYearButton != null) prevYearButton.interactable = selectedAge > 0;
        if (nextYearButton != null) nextYearButton.interactable = selectedAge < currentMaxAge;

        var p = panel != null ? panel : GetComponentInParent<LedgerPanelUI>();
        Color incColor = p != null ? p.IncomeColor : new Color(0.18f, 0.49f, 0.20f);
        Color expColor = p != null ? p.ExpenseColor : new Color(0.78f, 0.16f, 0.16f);
        Color supColor = p != null ? p.SupportColor : new Color(0.08f, 0.40f, 0.75f);
        Color headColor = p != null ? p.HeaderColor : new Color(0.20f, 0.20f, 0.20f);
        Color normColor = p != null ? p.NormalColor : new Color(0.25f, 0.25f, 0.25f);

        // ------------------------------------------------------------
        // 12か月の棒グラフ
        // ------------------------------------------------------------
        BuildBarChart(p);

        // ------------------------------------------------------------
        // リスト行生成
        // ------------------------------------------------------------
        ClearRows();

        var lines = Ledger.I.LinesOfYear(selectedAge);

        int grossSalary = 0;
        int taxSum = 0;
        int supportIncome = 0;
        int expenseTotal = 0;
        int coveredTotal = 0;
        int childCostTotal = 0;

        // ------------------------------------------------------------
        // 左：収入と税金
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
            // 「年末調整」「確定申告の還付」は別の行にする
            var normalTaxLines = new List<LedgerLine>();
            var specialTaxLines = new List<LedgerLine>();

            foreach (var l in lines)
            {
                if (l.category == ExpenseCategory.税金社会保険)
                {
                    taxSum += l.amount;
                    if (l.source.StartsWith("年末調整") || l.source.StartsWith("確定申告"))
                    {
                        specialTaxLines.Add(l);
                    }
                    else
                    {
                        normalTaxLines.Add(l);
                    }
                }
            }

            foreach (var l in normalTaxLines)
            {
                string amtStr = l.amount < 0 ? $"−{-l.amount:N0}" : $"+{l.amount:N0}";
                Color c = l.amount < 0 ? expColor : incColor;
                SpawnRow(incomeContent, l.source, amtStr, null, c, false);
            }

            foreach (var l in specialTaxLines)
            {
                string amtStr = l.amount < 0 ? $"−{-l.amount:N0}" : $"+{l.amount:N0}";
                Color c = l.amount < 0 ? expColor : incColor;
                SpawnRow(incomeContent, l.source, amtStr, null, c, false);
            }

            // 1年の税金の合計行
            if (normalTaxLines.Count > 0 || specialTaxLines.Count > 0)
            {
                string taxTotalStr = taxSum < 0 ? $"−{-taxSum:N0}" : $"+{taxSum:N0}";
                Color c = taxSum < 0 ? expColor : incColor;
                SpawnRow(incomeContent, "→ 1年の税金", taxTotalStr, null, c, true);
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
        // 右：支出（種類ごとの1年合計。押すと中の行が開く）
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
                    if (l.amount == 0 && l.covered == 0) continue;

                    catLines.Add(l);
                    int cost = Math.Abs(l.amount);
                    catSubtotal += cost;
                    expenseTotal += cost;
                    if (Ledger.IsChildCost(cat)) childCostTotal += cost;
                }

                if (catLines.Count == 0) continue;

                bool isExpanded = expandedCategories.Contains(cat);
                string arrow = isExpanded ? "▼ " : "▶ ";

                // 見出し行（種類名＋合計、クリックでトグル）
                var headerRow = SpawnRow(expenseContent, $"{arrow}{cat}", $"{catSubtotal:N0}", null, headColor, true);
                if (headerRow != null && headerRow.ClickButton != null)
                {
                    var c = cat;
                    headerRow.ClickButton.onClick.RemoveAllListeners();
                    headerRow.ClickButton.onClick.AddListener(() =>
                    {
                        if (expandedCategories.Contains(c)) expandedCategories.Remove(c);
                        else expandedCategories.Add(c);
                        Refresh(GetComponentInParent<LedgerPanelUI>());
                    });
                }

                // 展開中の個別行
                if (isExpanded)
                {
                    foreach (var l in catLines)
                    {
                        int cost = Math.Abs(l.amount);
                        string note = l.covered > 0 ? $"（支援 −{l.covered:N0}円）" : null;
                        SpawnRow(expenseContent, $"    {l.source}", $"{cost:N0}", note, expColor, false);
                    }
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
        int takeHome = grossSalary + taxSum;
        int balance = takeHome + supportIncome - expenseTotal;
        string balanceSign = balance >= 0 ? $"+{balance:N0}" : $"−{-balance:N0}";

        if (balanceSummaryText != null)
        {
            balanceSummaryText.text = $"手取り {takeHome:N0} ＋支援 {supportIncome:N0} − 支出 {expenseTotal:N0} ＝ 1年の差し引き {balanceSign}";
        }

        if (childCostSummaryText != null)
        {
            childCostSummaryText.text = $"子育てにかかったお金 {childCostTotal:N0}（うち支援で助かった額 {coveredTotal:N0}）";
        }
    }

    private void BuildBarChart(LedgerPanelUI panel)
    {
        if (barChartContainer == null) return;

        foreach (var b in spawnedBars)
        {
            if (b != null) Destroy(b);
        }
        spawnedBars.Clear();

        int[] monthlyExpenses = new int[12];
        int maxExp = 1;
        for (int i = 0; i < 12; i++)
        {
            int mIndex = selectedAge * 12 + i;
            var summary = Ledger.I.MonthSummary(mIndex);
            monthlyExpenses[i] = summary.expense;
            if (summary.expense > maxExp) maxExp = summary.expense;
        }

        // 12本の棒を配置
        for (int i = 0; i < 12; i++)
        {
            int mIndex = selectedAge * 12 + i;
            int exp = monthlyExpenses[i];
            int monthNum = i + 1;

            GameObject barGo;
            if (barPrefab != null)
            {
                barGo = Instantiate(barPrefab, barChartContainer);
            }
            else
            {
                barGo = CreateDefaultBarObject(barChartContainer, monthNum);
            }
            barGo.SetActive(true);
            spawnedBars.Add(barGo);

            // 棒の高さ調整
            var fillImage = barGo.transform.Find("Fill")?.GetComponent<Image>();
            if (fillImage != null)
            {
                var rt = fillImage.rectTransform;
                float height = maxExp > 0 ? (exp / (float)maxExp) * maxBarHeight : 0f;
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, Mathf.Max(2f, height));
            }

            // 金額表示
            var labelText = barGo.transform.Find("Amount")?.GetComponent<TMP_Text>();
            if (labelText != null)
            {
                if (exp > 0)
                {
                    labelText.text = exp >= 10000 ? $"{exp / 10000f:0.#}万" : $"{exp:N0}";
                    labelText.gameObject.SetActive(true);
                }
                else
                {
                    labelText.gameObject.SetActive(false);
                }
            }

            // 月ラベル
            var monthLabel = barGo.transform.Find("Month")?.GetComponent<TMP_Text>();
            if (monthLabel != null)
            {
                monthLabel.text = $"{monthNum}月";
            }

            // クリックでその月の［月］タブへ
            var btn = barGo.GetComponent<Button>() ?? barGo.GetComponentInChildren<Button>();
            if (btn != null)
            {
                int targetMonth = mIndex;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    var p = panel != null ? panel : GetComponentInParent<LedgerPanelUI>();
                    if (p != null) p.ShowMonth(targetMonth);
                });
            }
        }
    }

    private GameObject CreateDefaultBarObject(Transform parent, int monthNum)
    {
        var go = new GameObject($"Bar_{monthNum}", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth = 36f;
        le.flexibleWidth = 1f;
        le.minHeight = 80f;

        var btn = go.AddComponent<Button>();

        // 金額ラベル（上）
        var amtGo = new GameObject("Amount", typeof(RectTransform));
        amtGo.transform.SetParent(go.transform, false);
        var amtRt = amtGo.GetComponent<RectTransform>();
        amtRt.anchorMin = new Vector2(0.5f, 1f);
        amtRt.anchorMax = new Vector2(0.5f, 1f);
        amtRt.pivot = new Vector2(0.5f, 1f);
        amtRt.anchoredPosition = new Vector2(0f, 0f);
        amtRt.sizeDelta = new Vector2(50f, 16f);
        var amtTmp = amtGo.AddComponent<TextMeshProUGUI>();
        amtTmp.fontSize = 10f;
        amtTmp.alignment = TextAlignmentOptions.Center;
        amtTmp.color = new Color(0.25f, 0.25f, 0.25f);
        amtTmp.font = Resources.Load<TMP_FontAsset>("JPFont");

        // 棒本体（Fill）
        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(go.transform, false);
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0.5f, 0f);
        fillRt.anchorMax = new Vector2(0.5f, 0f);
        fillRt.pivot = new Vector2(0.5f, 0f);
        fillRt.anchoredPosition = new Vector2(0f, 18f);
        fillRt.sizeDelta = new Vector2(20f, 10f);
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.color = new Color(0.85f, 0.40f, 0.35f);
        btn.targetGraphic = fillImg;

        // 月ラベル（下）
        var mGo = new GameObject("Month", typeof(RectTransform));
        mGo.transform.SetParent(go.transform, false);
        var mRt = mGo.GetComponent<RectTransform>();
        mRt.anchorMin = new Vector2(0.5f, 0f);
        mRt.anchorMax = new Vector2(0.5f, 0f);
        mRt.pivot = new Vector2(0.5f, 0f);
        mRt.anchoredPosition = new Vector2(0f, 0f);
        mRt.sizeDelta = new Vector2(40f, 16f);
        var mTmp = mGo.AddComponent<TextMeshProUGUI>();
        mTmp.fontSize = 11f;
        mTmp.alignment = TextAlignmentOptions.Center;
        mTmp.color = new Color(0.35f, 0.35f, 0.35f);
        mTmp.font = Resources.Load<TMP_FontAsset>("JPFont");

        return go;
    }

    private LedgerRowUI SpawnRow(Transform parent, string name, string amount, string note, Color color, bool isHeader)
    {
        var row = Instantiate(rowPrefab, parent);
        row.gameObject.SetActive(true);
        row.Setup(name, amount, note, color, isHeader);
        spawnedRows.Add(row);
        return row;
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
