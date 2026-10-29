#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 家計簿画面（LedgerPanel.prefab）の生成と、シーンへの配置・［家計簿］ボタンとの連携を行うエディタ拡張（依頼E5）。
/// </summary>
public static class LedgerUIBuilder
{
    private const string PrefabDir = "Assets/Yamaguchi/Prefabs/UI";
    private const string RowPrefabPath = "Assets/Yamaguchi/Prefabs/UI/LedgerRow.prefab";
    private const string PanelPrefabPath = "Assets/Yamaguchi/Prefabs/UI/LedgerPanel.prefab";

    private static readonly Color Paper = new Color(0.98f, 0.97f, 0.95f);
    private static readonly Color HeaderBg = new Color(0.92f, 0.90f, 0.86f);
    private static readonly Color Ink = new Color(0.20f, 0.20f, 0.20f);
    private static readonly Color SubText = new Color(0.45f, 0.45f, 0.45f);

    // ------------------------------------------------------------
    // 1. 家計簿の画面のプレハブを作る
    // ------------------------------------------------------------

    [MenuItem("子育て/家計簿/1. 家計簿の画面のプレハブを作る")]
    public static void BuildPrefabs()
    {
        if (!Directory.Exists(PrefabDir))
        {
            Directory.CreateDirectory(PrefabDir);
        }

        var font = LoadFont();

        // 1-1. LedgerRow プレハブの作成
        var rowPrefabObj = BuildRowPrefab(font);

        // 1-2. LedgerPanel プレハブの作成
        BuildPanelPrefab(font, rowPrefabObj);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[LedgerUIBuilder] プレハブを作成しました：\n  {RowPrefabPath}\n  {PanelPrefabPath}");
    }

    private static LedgerRowUI BuildRowPrefab(TMP_FontAsset font)
    {
        var root = NewUI("LedgerRow", null);
        var rt = (RectTransform)root.transform;
        rt.sizeDelta = new Vector2(340f, 24f);

        var bg = root.AddComponent<Image>();
        bg.color = new Color(0.92f, 0.91f, 0.88f, 0.8f);
        bg.raycastTarget = true;

        var btn = root.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.transition = Selectable.Transition.ColorTint;

        var le = root.AddComponent<LayoutElement>();
        le.preferredHeight = 24f;
        le.minHeight = 22f;
        le.flexibleWidth = 1f;

        var hlg = root.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(6, 6, 2, 2);
        hlg.spacing = 6f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;

        var nameText = NewText("NameText", root.transform, font, "項目名", 13f, Ink, TextAlignmentOptions.Left);
        var nameLe = nameText.gameObject.AddComponent<LayoutElement>();
        nameLe.flexibleWidth = 1f;

        var noteText = NewText("NoteText", root.transform, font, "（支援 −○○円）", 11f, new Color(0.10f, 0.45f, 0.80f), TextAlignmentOptions.Right);
        var noteLe = noteText.gameObject.AddComponent<LayoutElement>();
        noteLe.preferredWidth = 120f;
        noteLe.flexibleWidth = 0f;

        var amountText = NewText("AmountText", root.transform, font, "0", 13f, Ink, TextAlignmentOptions.Right);
        var amtLe = amountText.gameObject.AddComponent<LayoutElement>();
        amtLe.preferredWidth = 80f;
        amtLe.flexibleWidth = 0f;

        var comp = root.AddComponent<LedgerRowUI>();
        var so = new SerializedObject(comp);
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("amountText").objectReferenceValue = amountText;
        so.FindProperty("noteText").objectReferenceValue = noteText;
        so.FindProperty("button").objectReferenceValue = btn;
        so.FindProperty("bgImage").objectReferenceValue = bg;
        so.ApplyModifiedPropertiesWithoutUndo();

        var saved = PrefabUtility.SaveAsPrefabAsset(root, RowPrefabPath);
        Object.DestroyImmediate(root);
        return saved.GetComponent<LedgerRowUI>();
    }

    private static void BuildPanelPrefab(TMP_FontAsset font, LedgerRowUI rowPrefab)
    {
        var root = NewUI("LedgerPanel", null);
        Stretch(root);

        // 半透明暗幕（Dim）
        var dim = NewUI("Dim", root.transform);
        Stretch(dim);
        var dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.55f);
        var dimBtn = dim.AddComponent<Button>();
        dimBtn.targetGraphic = dimImg;

        // メインウィンドウ（Window：740 x 520）
        var win = NewUI("Window", root.transform);
        var winRt = (RectTransform)win.transform;
        winRt.anchorMin = winRt.anchorMax = new Vector2(0.5f, 0.5f);
        winRt.pivot = new Vector2(0.5f, 0.5f);
        winRt.sizeDelta = new Vector2(740f, 520f);
        var winImg = win.AddComponent<Image>();
        winImg.color = Paper;

        // ------------------------------------------------------------
        // ヘッダー（タイトル、タブ、×ボタン）
        // ------------------------------------------------------------
        var header = NewUI("Header", win.transform);
        var headerRt = (RectTransform)header.transform;
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.anchoredPosition = new Vector2(0f, 0f);
        headerRt.sizeDelta = new Vector2(0f, 44f);
        var headerImg = header.AddComponent<Image>();
        headerImg.color = HeaderBg;

        var title = NewText("Title", header.transform, font, "家計簿", 20f, Ink, TextAlignmentOptions.Left);
        title.fontStyle = FontStyles.Bold;
        var tRt = title.rectTransform;
        tRt.anchorMin = new Vector2(0f, 0.5f);
        tRt.anchorMax = new Vector2(0f, 0.5f);
        tRt.pivot = new Vector2(0f, 0.5f);
        tRt.anchoredPosition = new Vector2(16f, 0f);
        tRt.sizeDelta = new Vector2(100f, 32f);

        // タブボタン［月］［年］
        var tabsGo = NewUI("Tabs", header.transform);
        var tabsRt = (RectTransform)tabsGo.transform;
        tabsRt.anchorMin = new Vector2(0f, 0.5f);
        tabsRt.anchorMax = new Vector2(0f, 0.5f);
        tabsRt.pivot = new Vector2(0f, 0.5f);
        tabsRt.anchoredPosition = new Vector2(110f, 0f);
        tabsRt.sizeDelta = new Vector2(140f, 32f);
        var tabsHlg = tabsGo.AddComponent<HorizontalLayoutGroup>();
        tabsHlg.spacing = 8f;
        tabsHlg.childControlWidth = true;
        tabsHlg.childControlHeight = true;

        var monthBtn = NewButton("MonthTab", tabsGo.transform, font, "月", new Color(1.00f, 0.85f, 0.55f), out var monthLabel);
        var yearBtn = NewButton("YearTab", tabsGo.transform, font, "年", Color.white, out var yearLabel);

        // × 閉じるボタン
        var closeBtn = NewButton("CloseButton", header.transform, font, "×", new Color(0.85f, 0.83f, 0.80f), out _);
        var cRt = (RectTransform)closeBtn.transform;
        cRt.anchorMin = new Vector2(1f, 0.5f);
        cRt.anchorMax = new Vector2(1f, 0.5f);
        cRt.pivot = new Vector2(1f, 0.5f);
        cRt.anchoredPosition = new Vector2(-12f, 0f);
        cRt.sizeDelta = new Vector2(32f, 32f);

        // ------------------------------------------------------------
        // 月の画面（MonthView）
        // ------------------------------------------------------------
        var monthViewGo = NewUI("MonthView", win.transform);
        Stretch(monthViewGo, 0f);
        var mvRt = (RectTransform)monthViewGo.transform;
        mvRt.offsetMax = new Vector2(0f, -44f); // ヘッダー分下げる

        // 月ナビゲーション
        var mNav = NewUI("MonthNav", monthViewGo.transform);
        var mNavRt = (RectTransform)mNav.transform;
        mNavRt.anchorMin = new Vector2(0f, 1f);
        mNavRt.anchorMax = new Vector2(1f, 1f);
        mNavRt.pivot = new Vector2(0.5f, 1f);
        mNavRt.anchoredPosition = new Vector2(0f, -4f);
        mNavRt.sizeDelta = new Vector2(-32f, 32f);

        var mPrevBtn = NewButton("PrevBtn", mNav.transform, font, "‹", new Color(0.90f, 0.88f, 0.85f), out _);
        var mpRt = (RectTransform)mPrevBtn.transform;
        mpRt.anchorMin = new Vector2(0.5f, 0.5f);
        mpRt.anchorMax = new Vector2(0.5f, 0.5f);
        mpRt.anchoredPosition = new Vector2(-120f, 0f);
        mpRt.sizeDelta = new Vector2(36f, 28f);

        var mTitle = NewText("MonthTitle", mNav.transform, font, "‹  0歳 1月  ›", 17f, Ink, TextAlignmentOptions.Center);
        mTitle.fontStyle = FontStyles.Bold;
        var mtRt = mTitle.rectTransform;
        mtRt.anchorMin = new Vector2(0.5f, 0.5f);
        mtRt.anchorMax = new Vector2(0.5f, 0.5f);
        mtRt.anchoredPosition = new Vector2(0f, 0f);
        mtRt.sizeDelta = new Vector2(180f, 28f);

        var mNextBtn = NewButton("NextBtn", mNav.transform, font, "›", new Color(0.90f, 0.88f, 0.85f), out _);
        var mnRt = (RectTransform)mNextBtn.transform;
        mnRt.anchorMin = new Vector2(0.5f, 0.5f);
        mnRt.anchorMax = new Vector2(0.5f, 0.5f);
        mnRt.anchoredPosition = new Vector2(120f, 0f);
        mnRt.sizeDelta = new Vector2(36f, 28f);

        // 左右パネルコンテナ
        var mPanels = NewUI("Panels", monthViewGo.transform);
        var mPanelsRt = (RectTransform)mPanels.transform;
        mPanelsRt.anchorMin = new Vector2(0f, 0f);
        mPanelsRt.anchorMax = new Vector2(1f, 1f);
        mPanelsRt.offsetMin = new Vector2(16f, 52f);
        mPanelsRt.offsetMax = new Vector2(-16f, -40f);
        var mHlg = mPanels.AddComponent<HorizontalLayoutGroup>();
        mHlg.spacing = 16f;
        mHlg.childControlWidth = true;
        mHlg.childControlHeight = true;

        var mIncomePanel = BuildScrollSection("IncomePanel", mPanels.transform, font, "収入・天引き・支援", out var mIncomeContent);
        var mExpensePanel = BuildScrollSection("ExpensePanel", mPanels.transform, font, "支出", out var mExpenseContent);

        // 下部サマリー
        var mSummary = NewUI("Summary", monthViewGo.transform);
        var mSumRt = (RectTransform)mSummary.transform;
        mSumRt.anchorMin = new Vector2(0f, 0f);
        mSumRt.anchorMax = new Vector2(1f, 0f);
        mSumRt.pivot = new Vector2(0.5f, 0f);
        mSumRt.anchoredPosition = new Vector2(0f, 4f);
        mSumRt.sizeDelta = new Vector2(-32f, 44f);
        var mSumVlg = mSummary.AddComponent<VerticalLayoutGroup>();
        mSumVlg.spacing = 2f;
        mSumVlg.childControlWidth = true;
        mSumVlg.childControlHeight = true;

        var mBalText = NewText("BalanceText", mSummary.transform, font, "手取り 0 ＋支援 0 − 支出 0 ＝ 差し引き 0", 13f, Ink, TextAlignmentOptions.Center);
        mBalText.fontStyle = FontStyles.Bold;
        var mCostText = NewText("CostText", mSummary.transform, font, "支援で助かった額 0   子育てにかかったお金 0", 12f, SubText, TextAlignmentOptions.Center);

        var monthViewComp = monthViewGo.AddComponent<LedgerMonthView>();
        var mvSo = new SerializedObject(monthViewComp);
        mvSo.FindProperty("titleText").objectReferenceValue = mTitle;
        mvSo.FindProperty("prevMonthButton").objectReferenceValue = mPrevBtn;
        mvSo.FindProperty("nextMonthButton").objectReferenceValue = mNextBtn;
        mvSo.FindProperty("incomeContent").objectReferenceValue = mIncomeContent;
        mvSo.FindProperty("expenseContent").objectReferenceValue = mExpenseContent;
        mvSo.FindProperty("rowPrefab").objectReferenceValue = rowPrefab;
        mvSo.FindProperty("balanceSummaryText").objectReferenceValue = mBalText;
        mvSo.FindProperty("childCostSummaryText").objectReferenceValue = mCostText;
        mvSo.ApplyModifiedPropertiesWithoutUndo();

        // ------------------------------------------------------------
        // 年の画面（YearView）
        // ------------------------------------------------------------
        var yearViewGo = NewUI("YearView", win.transform);
        Stretch(yearViewGo, 0f);
        var yvRt = (RectTransform)yearViewGo.transform;
        yvRt.offsetMax = new Vector2(0f, -44f);

        // 年ナビゲーション
        var yNav = NewUI("YearNav", yearViewGo.transform);
        var yNavRt = (RectTransform)yNav.transform;
        yNavRt.anchorMin = new Vector2(0f, 1f);
        yNavRt.anchorMax = new Vector2(1f, 1f);
        yNavRt.pivot = new Vector2(0.5f, 1f);
        yNavRt.anchoredPosition = new Vector2(0f, -4f);
        yNavRt.sizeDelta = new Vector2(-32f, 30f);

        var yPrevBtn = NewButton("PrevBtn", yNav.transform, font, "‹", new Color(0.90f, 0.88f, 0.85f), out _);
        var ypRt = (RectTransform)yPrevBtn.transform;
        ypRt.anchorMin = new Vector2(0.5f, 0.5f);
        ypRt.anchorMax = new Vector2(0.5f, 0.5f);
        ypRt.anchoredPosition = new Vector2(-150f, 0f);
        ypRt.sizeDelta = new Vector2(36f, 26f);

        var yTitle = NewText("YearTitle", yNav.transform, font, "‹  0歳の1年（1月〜12月）  ›", 17f, Ink, TextAlignmentOptions.Center);
        yTitle.fontStyle = FontStyles.Bold;
        var ytRt = yTitle.rectTransform;
        ytRt.anchorMin = new Vector2(0.5f, 0.5f);
        ytRt.anchorMax = new Vector2(0.5f, 0.5f);
        ytRt.anchoredPosition = new Vector2(0f, 0f);
        ytRt.sizeDelta = new Vector2(260f, 26f);

        var yNextBtn = NewButton("NextBtn", yNav.transform, font, "›", new Color(0.90f, 0.88f, 0.85f), out _);
        var ynRt = (RectTransform)yNextBtn.transform;
        ynRt.anchorMin = new Vector2(0.5f, 0.5f);
        ynRt.anchorMax = new Vector2(0.5f, 0.5f);
        ynRt.anchoredPosition = new Vector2(150f, 0f);
        ynRt.sizeDelta = new Vector2(36f, 26f);

        // 棒グラフエリア
        var barArea = NewUI("BarChartArea", yearViewGo.transform);
        var barAreaRt = (RectTransform)barArea.transform;
        barAreaRt.anchorMin = new Vector2(0f, 1f);
        barAreaRt.anchorMax = new Vector2(1f, 1f);
        barAreaRt.pivot = new Vector2(0.5f, 1f);
        barAreaRt.anchoredPosition = new Vector2(0f, -36f);
        barAreaRt.sizeDelta = new Vector2(-32f, 76f);
        var barBg = barArea.AddComponent<Image>();
        barBg.color = new Color(0.95f, 0.94f, 0.91f);

        var barTitle = NewText("BarTitle", barArea.transform, font, "月ごとの支出（押すとその月へ）", 11f, SubText, TextAlignmentOptions.Left);
        var btRt = barTitle.rectTransform;
        btRt.anchorMin = new Vector2(0f, 1f);
        btRt.anchorMax = new Vector2(0f, 1f);
        btRt.pivot = new Vector2(0f, 1f);
        btRt.anchoredPosition = new Vector2(8f, -4f);
        btRt.sizeDelta = new Vector2(220f, 16f);

        var barContainer = NewUI("BarContainer", barArea.transform);
        var bcRt = (RectTransform)barContainer.transform;
        bcRt.anchorMin = new Vector2(0f, 0f);
        bcRt.anchorMax = new Vector2(1f, 1f);
        bcRt.offsetMin = new Vector2(8f, 2f);
        bcRt.offsetMax = new Vector2(-8f, -18f);
        var bcHlg = barContainer.AddComponent<HorizontalLayoutGroup>();
        bcHlg.spacing = 6f;
        bcHlg.childControlWidth = true;
        bcHlg.childControlHeight = true;
        bcHlg.childForceExpandWidth = true;
        bcHlg.childForceExpandHeight = true;

        // 年の左右パネル
        var yPanels = NewUI("Panels", yearViewGo.transform);
        var yPanelsRt = (RectTransform)yPanels.transform;
        yPanelsRt.anchorMin = new Vector2(0f, 0f);
        yPanelsRt.anchorMax = new Vector2(1f, 1f);
        yPanelsRt.offsetMin = new Vector2(16f, 52f);
        yPanelsRt.offsetMax = new Vector2(-16f, -116f);
        var yHlg = yPanels.AddComponent<HorizontalLayoutGroup>();
        yHlg.spacing = 16f;
        yHlg.childControlWidth = true;
        yHlg.childControlHeight = true;

        var yIncomePanel = BuildScrollSection("IncomePanel", yPanels.transform, font, "収入と税金", out var yIncomeContent);
        var yExpensePanel = BuildScrollSection("ExpensePanel", yPanels.transform, font, "支出（種類ごと）", out var yExpenseContent);

        // 年の下部サマリー
        var ySummary = NewUI("Summary", yearViewGo.transform);
        var ySumRt = (RectTransform)ySummary.transform;
        ySumRt.anchorMin = new Vector2(0f, 0f);
        ySumRt.anchorMax = new Vector2(1f, 0f);
        ySumRt.pivot = new Vector2(0.5f, 0f);
        ySumRt.anchoredPosition = new Vector2(0f, 4f);
        ySumRt.sizeDelta = new Vector2(-32f, 44f);
        var ySumVlg = ySummary.AddComponent<VerticalLayoutGroup>();
        ySumVlg.spacing = 2f;
        ySumVlg.childControlWidth = true;
        ySumVlg.childControlHeight = true;

        var yBalText = NewText("BalanceText", ySummary.transform, font, "手取り 0 ＋支援 0 − 支出 0 ＝ 1年の差し引き 0", 13f, Ink, TextAlignmentOptions.Center);
        yBalText.fontStyle = FontStyles.Bold;
        var yCostText = NewText("CostText", ySummary.transform, font, "子育てにかかったお金 0（うち支援で助かった額 0）", 12f, SubText, TextAlignmentOptions.Center);

        var yearViewComp = yearViewGo.AddComponent<LedgerYearView>();
        var yvSo = new SerializedObject(yearViewComp);
        yvSo.FindProperty("titleText").objectReferenceValue = yTitle;
        yvSo.FindProperty("prevYearButton").objectReferenceValue = yPrevBtn;
        yvSo.FindProperty("nextYearButton").objectReferenceValue = yNextBtn;
        yvSo.FindProperty("barChartContainer").objectReferenceValue = bcRt;
        yvSo.FindProperty("incomeContent").objectReferenceValue = yIncomeContent;
        yvSo.FindProperty("expenseContent").objectReferenceValue = yExpenseContent;
        yvSo.FindProperty("rowPrefab").objectReferenceValue = rowPrefab;
        yvSo.FindProperty("balanceSummaryText").objectReferenceValue = yBalText;
        yvSo.FindProperty("childCostSummaryText").objectReferenceValue = yCostText;
        yvSo.ApplyModifiedPropertiesWithoutUndo();

        // ------------------------------------------------------------
        // LedgerPanelUI 本体の配線
        // ------------------------------------------------------------
        var panelComp = root.AddComponent<LedgerPanelUI>();
        var pSo = new SerializedObject(panelComp);
        pSo.FindProperty("monthTabButton").objectReferenceValue = monthBtn;
        pSo.FindProperty("yearTabButton").objectReferenceValue = yearBtn;
        pSo.FindProperty("monthTabLabel").objectReferenceValue = monthLabel;
        pSo.FindProperty("yearTabLabel").objectReferenceValue = yearLabel;
        pSo.FindProperty("closeButton").objectReferenceValue = closeBtn;
        pSo.FindProperty("monthView").objectReferenceValue = monthViewComp;
        pSo.FindProperty("yearView").objectReferenceValue = yearViewComp;
        pSo.ApplyModifiedPropertiesWithoutUndo();

        // 暗幕クリックでも閉じるように登録
        UnityEventTools.AddPersistentListener(dimBtn.onClick, panelComp.Close);

        // 初期表示状態：MonthView のみアクティブ、YearView は非アクティブ
        monthViewGo.SetActive(true);
        yearViewGo.SetActive(false);

        PrefabUtility.SaveAsPrefabAsset(root, PanelPrefabPath);
        Object.DestroyImmediate(root);
    }

    private static GameObject BuildScrollSection(string name, Transform parent, TMP_FontAsset font, string headerTitle, out RectTransform contentRt)
    {
        var section = NewUI(name, parent);
        var sRt = (RectTransform)section.transform;
        sRt.sizeDelta = new Vector2(340f, 300f);
        var sVlg = section.AddComponent<VerticalLayoutGroup>();
        sVlg.spacing = 6f;
        sVlg.childControlWidth = true;
        sVlg.childControlHeight = true;
        sVlg.childForceExpandWidth = true;
        sVlg.childForceExpandHeight = false;

        var header = NewText(name + "_Header", section.transform, font, headerTitle, 14f, Ink, TextAlignmentOptions.Left);
        header.fontStyle = FontStyles.Bold;
        var hLe = header.gameObject.AddComponent<LayoutElement>();
        hLe.preferredHeight = 22f;

        var scrollGo = NewUI("ScrollView", section.transform);
        var scLe = scrollGo.AddComponent<LayoutElement>();
        scLe.flexibleHeight = 1f;
        scLe.flexibleWidth = 1f;

        var scrollImg = scrollGo.AddComponent<Image>();
        scrollImg.color = Color.white;
        scrollImg.raycastTarget = true;

        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.scrollSensitivity = 25f;

        var viewport = NewUI("Viewport", scrollGo.transform);
        Stretch(viewport);
        var vImg = viewport.AddComponent<Image>();
        vImg.color = new Color(1f, 1f, 1f, 0f);
        var mask = viewport.AddComponent<RectMask2D>();

        var content = NewUI("Content", viewport.transform);
        contentRt = (RectTransform)content.transform;
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.sizeDelta = new Vector2(0f, 0f);

        var cVlg = content.AddComponent<VerticalLayoutGroup>();
        cVlg.spacing = 3f;
        cVlg.padding = new RectOffset(4, 4, 4, 4);
        cVlg.childControlWidth = true;
        cVlg.childControlHeight = true;
        cVlg.childForceExpandWidth = true;
        cVlg.childForceExpandHeight = false;

        var csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = (RectTransform)viewport.transform;
        scrollRect.content = contentRt;

        return section;
    }

    // ------------------------------------------------------------
    // 2. 開いているシーンに置いて［家計簿］ボタンにつなぐ
    // ------------------------------------------------------------

    [MenuItem("子育て/家計簿/2. 開いているシーンに置いて［家計簿］ボタンにつなぐ")]
    public static void AttachToCurrentScene()
    {
        var canvas = GameObject.Find("Canvas");
        if (canvas == null || canvas.GetComponent<Canvas>() == null)
        {
            Debug.LogError("[LedgerUIBuilder] Canvas が見つかりません。シーンに Canvas が必要です");
            return;
        }

        var panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
        if (panelPrefab == null)
        {
            Debug.LogError($"[LedgerUIBuilder] プレハブが見つかりません：{PanelPrefabPath}。先にメニュー 1 を実行してください");
            return;
        }

        // 既存の LedgerPanel を探す
        var existing = canvas.transform.Find("LedgerPanel");
        GameObject panelObj;
        if (existing != null)
        {
            panelObj = existing.gameObject;
        }
        else
        {
            panelObj = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, canvas.transform);
            Undo.RegisterCreatedObjectUndo(panelObj, "Instantiate LedgerPanel");
        }

        // Canvas の子の一番下に配置（他の HUD より手前）
        panelObj.transform.SetAsLastSibling();
        panelObj.SetActive(false); // 初期状態は非表示

        var panelComp = panelObj.GetComponent<LedgerPanelUI>();
        if (panelComp == null)
        {
            Debug.LogError("[LedgerUIBuilder] LedgerPanel に LedgerPanelUI コンポーネントが見つかりません");
            return;
        }

        // Canvas/Buttons/Kakeibo ボタンを探す
        var kakeiboBtn = canvas.transform.Find("Buttons/Kakeibo")?.GetComponent<Button>();
        if (kakeiboBtn == null)
        {
            // フォールバック
            var allBtns = canvas.GetComponentsInChildren<Button>(true);
            foreach (var b in allBtns)
            {
                if (b.name == "Kakeibo")
                {
                    kakeiboBtn = b;
                    break;
                }
            }
        }

        if (kakeiboBtn == null)
        {
            Debug.LogWarning("[LedgerUIBuilder] Canvas/Buttons/Kakeibo ボタンが見つかりませんでした。ボタンへの配線は手動で行ってください");
        }
        else
        {
            Undo.RecordObject(kakeiboBtn, "Wire Kakeibo Button");

            // OnClick に LedgerPanelUI.Open を登録
            UnityEventTools.RemovePersistentListener(kakeiboBtn.onClick, panelComp.Open);
            UnityEventTools.AddPersistentListener(kakeiboBtn.onClick, panelComp.Open);

            // フォントを JPFont に変更
            var font = LoadFont();
            var tmp = kakeiboBtn.GetComponentInChildren<TMP_Text>();
            if (tmp != null && font != null)
            {
                Undo.RecordObject(tmp, "Update Kakeibo Font");
                tmp.font = font;
            }

            Debug.Log($"[LedgerUIBuilder] {kakeiboBtn.name} ボタンの onClick に LedgerPanelUI.Open を設定し、フォントを JPFont に更新しました");
        }

        EditorSceneManager.MarkSceneDirty(canvas.scene);
        Debug.Log("[LedgerUIBuilder] シーンへの配置と設定が完了しました。シーンを保存してください");
    }

    // ------------------------------------------------------------
    // ヘルパー
    // ------------------------------------------------------------

    private static TMP_FontAsset LoadFont()
    {
        var font = Resources.Load<TMP_FontAsset>("JPFont");
        if (font == null) Debug.LogWarning("[LedgerUIBuilder] JPFont が Resources に見つかりません");
        return font;
    }

    private static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(GameObject go, float margin = 0f)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(margin, margin);
        rt.offsetMax = new Vector2(-margin, -margin);
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, string text,
        float size, Color color, TextAlignmentOptions align)
    {
        var go = NewUI(name, parent);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    private static Button NewButton(string name, Transform parent, TMP_FontAsset font, string label, Color color, out TextMeshProUGUI labelText)
    {
        var go = NewUI(name, parent);
        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        labelText = NewText("Label", go.transform, font, label, 14f, Ink, TextAlignmentOptions.Center);
        Stretch(labelText.gameObject);
        return btn;
    }
}
#endif
