using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// カレンダー画面のUIコントローラー。
/// Qiita記事「【Unity】カレンダーの作成」(hire1105) の42マス (7列×6行) グリッド方式を採用。
/// 日付計算は GameDate（ゲーム内通算日数）に連動。
/// カレンダー開閉時に GameClock の PushPause() / PopPause() を呼び出し、時間の進行を一時停止・再開する。
/// </summary>
public class CalendarUI : MonoBehaviour
{
    private const int CellCount = 42; // 7列 × 6行

    [Header("UI 参照（未アタッチの場合は自動構築）")]
    [SerializeField] private RectTransform containerPanel;
    [SerializeField] private TextMeshProUGUI monthLabel;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Transform gridParent;

    [Header("サイズ設定")]
    [SerializeField] private Vector2 windowSize = new Vector2(560f, 440f);
    [SerializeField] private Vector2 cellSize = new Vector2(68f, 52f);
    [SerializeField] private Vector2 cellSpacing = new Vector2(6f, 6f);
    [SerializeField] private float headerHeight = 56f;

    [Header("カラー設定（HUDと統一）")]
    [SerializeField] private Color headerCyan = new Color(0.16f, 0.83f, 0.96f, 1f);
    [SerializeField] private Color panelBg = new Color(0.95f, 0.95f, 0.95f, 0.98f);
    [SerializeField] private Color boxGray = new Color(0.88f, 0.88f, 0.88f, 1f);
    [SerializeField] private Color darkText = new Color(0.13f, 0.13f, 0.13f, 1f);
    [SerializeField] private Color otherMonthText = new Color(0.65f, 0.65f, 0.65f, 1f);
    [SerializeField] private Color cellBg = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color todayHighlightBg = new Color(1f, 0.85f, 0.35f, 1f);
    [SerializeField] private Color selectedCellBg = new Color(0.7f, 0.92f, 1f, 1f);

    /// <summary>日付マスがタップされたイベント。引数は通算日数 (ageDays)</summary>
    public event Action<int> OnDaySelected;

    private readonly List<Button> cells = new List<Button>();
    private readonly List<TextMeshProUGUI> cellLabels = new List<TextMeshProUGUI>();
    private readonly List<Image> cellImages = new List<Image>();

    private int viewYear;
    private int viewMonth;
    private int selectedAgeDays = -1;
    private bool isPausedBySelf = false;

    private void Awake()
    {
        EnsureUIBuilt();
    }

    /// <summary>
    /// どのシーンからでも呼び出し可能な静的表示メソッド。
    /// 既存のモーダルがあれば表示し、無ければアクティブなCanvas配下に動的生成する。
    /// </summary>
    public static CalendarUI ShowCalendar()
    {
        var existing = FindObjectOfType<CalendarUI>(true);
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            return existing;
        }

        var canvasGO = GameObject.Find("HUD_Canvas") ?? GameObject.Find("Canvas") ?? FindObjectOfType<Canvas>()?.gameObject;
        if (canvasGO == null)
        {
            var newCanvas = new GameObject("Calendar_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = newCanvas.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 999;
            var scaler = newCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 1024);
            canvasGO = newCanvas;
        }

        var calGO = new GameObject("CalendarUI_Modal", typeof(RectTransform));
        calGO.transform.SetParent(canvasGO.transform, false);
        var rt = calGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var ui = calGO.AddComponent<CalendarUI>();
        ui.gameObject.SetActive(true);
        return ui;
    }

    /// <summary>
    /// シーンロード時に自動でシーン内の「カレンダー」ボタンを検索し、クリック時にカレンダーが開くよう紐付ける。
    /// これにより Main_test, Main_test2 等どのシーンでも配置済みのカレンダーボタンが動作する。
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBindCalendarButtons()
    {
        BindSceneCalendarButtons();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => BindSceneCalendarButtons();
    }

    public static void BindSceneCalendarButtons()
    {
        var buttons = FindObjectsOfType<Button>(true);
        foreach (var btn in buttons)
        {
            if (btn.GetComponentInParent<CalendarUI>() != null) continue;

            string name = btn.gameObject.name.ToLower();
            string text = btn.GetComponentInChildren<TextMeshProUGUI>()?.text ?? btn.GetComponentInChildren<Text>()?.text ?? "";

            if (name.Contains("calendar") || name.Contains("カレンダー") || text.Contains("カレンダー"))
            {
                btn.onClick.RemoveListener(OnAutoCalendarButtonClicked);
                btn.onClick.AddListener(OnAutoCalendarButtonClicked);
            }
        }
    }

    private static void OnAutoCalendarButtonClicked()
    {
        ShowCalendar();
    }

    private void OnEnable()
    {
        // カレンダーが開いたときに時間の進行を停止する (Decision 0004)
        if (!isPausedBySelf)
        {
            if (GameClock.Instance != null)
            {
                GameClock.Instance.PushPause();
            }
            else if (GameClock.I != null)
            {
                GameClock.I.PushPause();
            }
            isPausedBySelf = true;
        }

        // 今日の年月を取得して表示
        if (GameState.I != null)
        {
            GameDate.FromAgeDays(GameState.I.AgeDays, out viewYear, out viewMonth, out _);
            selectedAgeDays = Mathf.FloorToInt(GameState.I.AgeDays);
        }
        else
        {
            viewYear = 0;
            viewMonth = 1;
            selectedAgeDays = 0;
        }

        Refresh();
    }

    private void OnDisable()
    {
        // カレンダーが閉じたときに時間の進行一時停止を解除する (Decision 0004)
        if (isPausedBySelf)
        {
            if (GameClock.Instance != null)
            {
                GameClock.Instance.PopPause();
            }
            else if (GameClock.I != null)
            {
                GameClock.I.PopPause();
            }
            isPausedBySelf = false;
        }
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    private void ShiftMonth(int delta)
    {
        viewMonth += delta;
        if (viewMonth < 1)
        {
            if (viewYear > 0)
            {
                viewMonth = 12;
                viewYear--;
            }
            else
            {
                viewMonth = 1; // 0歳1月より前には戻れない
            }
        }
        else if (viewMonth > 12)
        {
            viewMonth = 1;
            viewYear++;
        }
        Refresh();
    }

    /// <summary>
    /// カレンダーの再描画 (Qiita 42マス方式)
    /// </summary>
    public void Refresh()
    {
        EnsureUIBuilt();

        if (monthLabel != null)
        {
            monthLabel.text = $"{viewYear}歳 {viewMonth}月";
        }

        int firstAgeDays = GameDate.ToAgeDays(viewYear, viewMonth, 1);
        int monthEndDays = GameDate.DaysInMonth(viewMonth);

        int todayAgeDays = (GameState.I != null) ? Mathf.FloorToInt(GameState.I.AgeDays) : 0;

        for (int i = 0; i < CellCount; i++)
        {
            if (i >= cells.Count) break;

            Button cell = cells[i];
            TextMeshProUGUI label = cellLabels[i];
            Image img = cellImages[i];

            cell.onClick.RemoveAllListeners();

            int dayNum = i + 1;

            if (dayNum > monthEndDays)
            {
                // 翌月の日付（淡いグレー・操作不可）
                int nextMonthDay = dayNum - monthEndDays;
                label.text = nextMonthDay.ToString();
                label.color = otherMonthText;
                img.color = boxGray;
                cell.interactable = false;
            }
            else
            {
                // 当月の日付
                int currentAgeDays = firstAgeDays + (dayNum - 1);
                label.text = dayNum.ToString();
                label.color = darkText;
                cell.interactable = true;

                // 今日の強調・選択日のハイライト
                if (currentAgeDays == todayAgeDays)
                {
                    img.color = todayHighlightBg;
                }
                else if (currentAgeDays == selectedAgeDays)
                {
                    img.color = selectedCellBg;
                }
                else
                {
                    img.color = cellBg;
                }

                int capturedAgeDays = currentAgeDays;
                cell.onClick.AddListener(() =>
                {
                    selectedAgeDays = capturedAgeDays;
                    OnDaySelected?.Invoke(capturedAgeDays);
                    Refresh();
                });
            }
        }
    }

    // ---------------- UI 動的構築・参照補完 ----------------

    private void EnsureUIBuilt()
    {
        if (cells.Count >= CellCount && monthLabel != null) return;

        // UIコンポーネントがアタッチされていない場合は動的に作成
        cells.Clear();
        cellLabels.Clear();
        cellImages.Clear();

        // 1. パネル背景
        var rootRT = GetComponent<RectTransform>();
        if (rootRT == null) rootRT = gameObject.AddComponent<RectTransform>();

        // 全画面フェード背景（モーダル用）
        var bgImage = GetComponent<Image>();
        if (bgImage == null)
        {
            bgImage = gameObject.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.45f);
        }

        // 2. メインウィンドウカード
        if (containerPanel == null)
        {
            var winGO = new GameObject("CalendarWindow", typeof(RectTransform), typeof(Image));
            winGO.transform.SetParent(transform, false);
            var winImg = winGO.GetComponent<Image>();
            winImg.color = panelBg;
            containerPanel = winGO.GetComponent<RectTransform>();
            SetRect(containerPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, windowSize);
        }
        else
        {
            containerPanel.sizeDelta = windowSize;
        }

        // 3. ヘッダーバー（シアン色）
        var headerGO = new GameObject("HeaderBar", typeof(RectTransform), typeof(Image));
        headerGO.transform.SetParent(containerPanel, false);
        var headerImg = headerGO.GetComponent<Image>();
        headerImg.color = headerCyan;
        var headerRT = headerGO.GetComponent<RectTransform>();
        SetRect(headerRT, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(0, headerHeight));

        // ヘッダータイトル (年月表示)
        if (monthLabel == null)
        {
            var titleGO = new GameObject("MonthLabel", typeof(RectTransform));
            titleGO.transform.SetParent(headerRT, false);
            monthLabel = titleGO.AddComponent<TextMeshProUGUI>();
            monthLabel.text = "0歳 1月";
            monthLabel.fontSize = 22;
            monthLabel.fontStyle = FontStyles.Bold;
            monthLabel.alignment = TextAlignmentOptions.Center;
            monthLabel.color = Color.white;
            SetStretchFull(monthLabel.rectTransform, 100, 0);
        }

        // 前月ボタン 「＜」
        if (prevButton == null)
        {
            prevButton = CreateHeaderButton(headerRT, "PrevBtn", "＜", new Vector2(12, -9), new Vector2(44, 38), 20);
            prevButton.onClick.AddListener(() => ShiftMonth(-1));
        }

        // 次月ボタン 「＞」
        if (nextButton == null)
        {
            nextButton = CreateHeaderButton(headerRT, "NextBtn", "＞", new Vector2(62, -9), new Vector2(44, 38), 20);
            nextButton.onClick.AddListener(() => ShiftMonth(1));
        }

        // 右上バツボタン 「✕」
        if (closeButton == null)
        {
            closeButton = CreateHeaderButton(headerRT, "CloseBtn", "✕", new Vector2(-12, -9), new Vector2(44, 38), 20, new Vector2(1, 1), new Vector2(1, 1));
            closeButton.onClick.AddListener(Close);
        }

        // 4. カレンダーグリッドエリア (7列 × 6行)
        if (gridParent == null)
        {
            var gridGO = new GameObject("DayGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridGO.transform.SetParent(containerPanel, false);
            var gridRT = gridGO.GetComponent<RectTransform>();
            SetRect(gridRT, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -(headerHeight / 2f)), new Vector2(-20, -(headerHeight + 20f)));

            var glg = gridGO.GetComponent<GridLayoutGroup>();
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 7;
            glg.cellSize = cellSize;
            glg.spacing = cellSpacing;
            glg.childAlignment = TextAnchor.MiddleCenter;

            gridParent = gridGO.transform;
        }

        // 5. 42個のマスセル作成
        for (int i = 0; i < CellCount; i++)
        {
            var cellGO = new GameObject($"DayCell_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
            cellGO.transform.SetParent(gridParent, false);

            var img = cellGO.GetComponent<Image>();
            img.color = cellBg;

            var btn = cellGO.GetComponent<Button>();
            btn.targetGraphic = img;

            var txtGO = new GameObject("Label", typeof(RectTransform));
            txtGO.transform.SetParent(cellGO.transform, false);

            var txt = txtGO.AddComponent<TextMeshProUGUI>();
            txt.text = (i + 1).ToString();
            txt.fontSize = 18;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = darkText;
            SetStretchFull(txt.rectTransform, 0, 0);

            cells.Add(btn);
            cellLabels.Add(txt);
            cellImages.Add(img);
        }
    }

    private Button CreateHeaderButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, float fontSize = 20f, Vector2 anchorMin = default, Vector2 anchorMax = default)
    {
        if (anchorMin == default && anchorMax == default)
        {
            anchorMin = new Vector2(0, 1);
            anchorMax = new Vector2(0, 1);
        }

        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.25f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;

        var rt = go.GetComponent<RectTransform>();
        SetRect(rt, anchorMin, anchorMax, new Vector2(0.5f, 1), pos, size);

        var txtGO = new GameObject("Text", typeof(RectTransform));
        txtGO.transform.SetParent(go.transform, false);
        var txt = txtGO.AddComponent<TextMeshProUGUI>();
        txt.text = label;
        txt.fontSize = fontSize;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;
        SetStretchFull(txt.rectTransform, 0, 0);

        return btn;
    }

    private void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
    {
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
    }

    private void SetStretchFull(RectTransform rt, float padX, float padY)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(padX, padY);
        rt.offsetMax = new Vector2(-padX, -padY);
    }
}
