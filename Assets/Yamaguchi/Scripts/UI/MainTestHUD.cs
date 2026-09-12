using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Main_test 用の HUD をコードで構築するコントローラ。
/// 左上のお金／年齢（子供が生まれてから）、子供の成長ゲージ、
/// 右のステータス開閉ボタン、下部の サポート／カレンダー／セーブ／設定 ボタンを生成し、
/// GameState の値に連動させる。テキスト・ボタンはすべて TextMeshPro。
/// 日本語フォントアセットがプロジェクトに無いため、OS の日本語フォントから
/// 動的な TMP フォントアセットを実行時に生成して割り当てる。
/// </summary>
public class MainTestHUD : MonoBehaviour
{
    [Header("誕生日（この日を0歳0日とする。年齢表示の月日計算に使用）")]
    [SerializeField] private int birthMonth = 12;
    [SerializeField] private int birthDay = 25;

    [Header("色")]
    [SerializeField] private Color cyan = new Color(0.16f, 0.83f, 0.96f, 1f);
    [SerializeField] private Color panelGray = new Color(0.86f, 0.86f, 0.855f, 1f);
    [SerializeField] private Color boxGray = new Color(0.93f, 0.93f, 0.93f, 1f);
    [SerializeField] private Color darkText = new Color(0.13f, 0.13f, 0.13f, 1f);
    [SerializeField] private Color gaugeFill = new Color(1f, 0.67f, 0.14f, 1f);

    private TMP_FontAsset jpFont;
    private Sprite roundSprite;

    private TextMeshProUGUI moneyText;
    private TextMeshProUGUI ageText;
    private TextMeshProUGUI stageText;
    private RectTransform gaugeFillRect;
    private GameObject statusPanel;
    private TextMeshProUGUI toggleLabel;

    private static readonly int[] MonthDays = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

private void Start()
    {
        var existing = GameObject.Find("HUD_Canvas");
        if (existing != null)
        {
            BindExisting(existing.transform);
        }
        else
        {
            jpFont = CreateJapaneseFont();
            roundSprite = null;
            EnsureEventSystem();
            BuildUI();
        }

        if (GameState.I != null)
        {
            UpdateMoney(GameState.I.Money);
            UpdateAge(GameState.I.AgeDays);
            GameState.I.OnMoneyChanged += UpdateMoney;
            GameState.I.OnAgeChanged += UpdateAge;
        }
        else
        {
            UpdateMoney(0);
            UpdateAge(0f);
        }
    }

    private void OnDestroy()
    {
        if (GameState.I != null)
        {
            GameState.I.OnMoneyChanged -= UpdateMoney;
            GameState.I.OnAgeChanged -= UpdateAge;
        }
    }

    // ---------------- データ連動 ----------------

    private void UpdateMoney(int money)
    {
        if (moneyText != null) moneyText.text = $"お金：{money:N0}";
    }

private void UpdateAge(float ageDays)
    {
        int totalDays = Mathf.Max(0, Mathf.FloorToInt(ageDays));
        int years = totalDays / 365;
        GetMonthDay(totalDays, out int m, out int d);
        if (ageText != null) ageText.text = $"{years}歳  {m}月{d}日";
        if (stageText != null) stageText.text = StageName(years);
        if (gaugeFillRect != null)
        {
            float goal = (GameState.I != null && GameState.I.GoalAgeDays > 0) ? GameState.I.GoalAgeDays : 7300f;
            float p = Mathf.Clamp01(ageDays / goal);
            gaugeFillRect.anchorMin = new Vector2(0f, 0f);
            gaugeFillRect.anchorMax = new Vector2(p, 1f);
            gaugeFillRect.offsetMin = Vector2.zero;
            gaugeFillRect.offsetMax = Vector2.zero;
        }
    }

    private void GetMonthDay(int totalDays, out int month, out int day)
    {
        int startDoy = 0;
        for (int i = 0; i < birthMonth - 1; i++) startDoy += MonthDays[i];
        startDoy += (birthDay - 1);

        int doy = ((startDoy + totalDays) % 365 + 365) % 365;
        int mi = 0;
        while (doy >= MonthDays[mi]) { doy -= MonthDays[mi]; mi++; }
        month = mi + 1;
        day = doy + 1;
    }

    private string StageName(int years)
    {
        if (years < 1) return "あかちゃん";
        if (years < 6) return "幼児";
        if (years < 12) return "小学生";
        if (years < 15) return "中学生";
        if (years < 18) return "高校生";
        return "成人";
    }

    // ---------------- ボタンの動作（今は仮：ログ出力／パネル開閉） ----------------

    private void ToggleStatus()
    {
        if (statusPanel == null) return;
        bool open = !statusPanel.activeSelf;
        statusPanel.SetActive(open);
        if (toggleLabel != null) toggleLabel.text = open ? ">" : "<";
    }

    private void OnSupport()  { Debug.Log("[HUD] サポート を押した（制度一覧を開く予定）"); }
    private void OnCalendar() { Debug.Log("[HUD] カレンダー を押した"); }
    private void OnSave()     { Debug.Log("[HUD] セーブ を押した"); }
    private void OnSettings() { Debug.Log("[HUD] 設定 を押した"); }

    // ---------------- UI 構築 ----------------

private void BuildUI()
    {
        var canvasGO = new GameObject("HUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1440, 1024);
        scaler.matchWidthOrHeight = 0.5f;
        var root = canvasGO.GetComponent<RectTransform>();

        var bottom = MakeImage(root, "BottomBar", cyan);
        SetStretchBottom(bottom.rectTransform, 250f);

        var moneyBox = MakeImage(root, "MoneyBox", boxGray);
        SetRect(moneyBox.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -20), new Vector2(300, 72));
        moneyText = MakeText(moneyBox.rectTransform, "MoneyText", "お金：0", 30, TextAlignmentOptions.Left);
        SetStretchFull(moneyText.rectTransform, 16, 0);

        var ageBox = MakeImage(root, "AgeBox", boxGray);
        SetRect(ageBox.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(340, -20), new Vector2(300, 72));
        ageText = MakeText(ageBox.rectTransform, "AgeText", "0歳", 30, TextAlignmentOptions.Center);
        SetStretchFull(ageText.rectTransform, 12, 0);

        var toggleBtn = MakeButton(root, "StatusToggle", "<", out toggleLabel, cyan, Color.white, 44);
        SetRect(toggleBtn.image.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-18, 150), new Vector2(92, 112));
        toggleBtn.onClick.AddListener(ToggleStatus);

        var panel = MakeImage(root, "StatusPanel", new Color(1f, 0.98f, 0.94f, 0.98f));
        SetRect(panel.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0), new Vector2(380, 760));
        statusPanel = panel.gameObject;
        var panelTitle = MakeText(panel.rectTransform, "Title", "家族ステータス", 30, TextAlignmentOptions.Center);
        SetRect(panelTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(340, 50));
        panelTitle.color = darkText;
        var panelNote = MakeText(panel.rectTransform, "Note", "（母親・父親・子どものカードをここに配置）", 20, TextAlignmentOptions.Center);
        SetRect(panelNote.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -86), new Vector2(340, 120));
        panelNote.color = new Color(0.5f, 0.5f, 0.48f, 1f);
        panelNote.enableWordWrapping = true;
        statusPanel.SetActive(false);

        var track = MakeImage(root, "GrowthTrack", new Color(0.96f, 0.96f, 0.96f, 1f));
        SetRect(track.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(180, 150), new Vector2(1000, 34));
        var fill = MakeImage(track.rectTransform, "GrowthFill", gaugeFill);
        var fr = fill.rectTransform;
        fr.anchorMin = new Vector2(0f, 0f);
        fr.anchorMax = new Vector2(0f, 1f);
        fr.pivot = new Vector2(0f, 0.5f);
        fr.offsetMin = Vector2.zero;
        fr.offsetMax = Vector2.zero;
        gaugeFillRect = fr;

        stageText = MakeText(root, "StageLabel", "あかちゃん", 26, TextAlignmentOptions.Center);
        SetRect(stageText.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(680, 192), new Vector2(320, 44));
        stageText.color = darkText;
        var goalLabel = MakeText(root, "GoalLabel", "20歳", 30, TextAlignmentOptions.Right);
        SetRect(goalLabel.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(1, 0), new Vector2(1190, 190), new Vector2(150, 46));
        goalLabel.color = darkText;

        string[] labels = { "サポート", "カレンダー", "セーブ", "設定" };
        Action[] handlers = { OnSupport, OnCalendar, OnSave, OnSettings };
        float bw = 300f, gap = 53f, x0 = 40f, y = 20f;
        for (int i = 0; i < 4; i++)
        {
            var b = MakeButton(root, "Btn_" + labels[i], labels[i], out _, panelGray, darkText, 28);
            SetRect(b.image.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(x0 + i * (bw + gap), y), new Vector2(bw, 120));
            var handler = handlers[i];
            b.onClick.AddListener(() => handler());
        }
    }

    // ---------------- 生成ヘルパー ----------------

    private Image MakeImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        if (roundSprite != null) { img.sprite = roundSprite; img.type = Image.Type.Sliced; }
        return img;
    }

    private TextMeshProUGUI MakeText(Transform parent, string name, string text, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (jpFont != null) t.font = jpFont;
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = darkText;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    private Button MakeButton(Transform parent, string name, string label, out TextMeshProUGUI labelText, Color bg, Color textColor, float fontSize)
    {
        var img = MakeImage(parent, name, bg);
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        labelText = MakeText(img.rectTransform, "Label", label, fontSize, TextAlignmentOptions.Center);
        SetStretchFull(labelText.rectTransform, 0, 0);
        labelText.color = textColor;
        return btn;
    }

    // ---------------- RectTransform ヘルパー ----------------

    private void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
    {
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
    }

    private void SetStretchFull(RectTransform rt, float padX, float padY)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(padX, padY);
        rt.offsetMax = new Vector2(-padX, -padY);
    }

    private void SetStretchBottom(RectTransform rt, float height)
    {
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.sizeDelta = new Vector2(0, height);
        rt.anchoredPosition = Vector2.zero;
    }

    // ---------------- 日本語フォント（OSフォントから動的生成） ----------------

private TMP_FontAsset CreateJapaneseFont()
    {
        var fromResources = Resources.Load<TMP_FontAsset>("JPFont");
        if (fromResources != null) return fromResources;
        if (TMP_Settings.defaultFontAsset != null) return TMP_Settings.defaultFontAsset;
        Debug.LogWarning("[HUD] 日本語フォント(JPFont)が見つかりません。");
        return null;
    }

    // ---------------- EventSystem 確保 ----------------

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        if (FindObjectOfType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem", typeof(EventSystem));
        var inputSystemModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputSystemModule != null) go.AddComponent(inputSystemModule);
        else go.AddComponent<StandaloneInputModule>();
    }


private T FindComp<T>(Transform root, string name) where T : Component
    {
        var t = FindTransform(root, name);
        return t != null ? t.GetComponent<T>() : null;
    }


private Transform FindTransform(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var r = FindTransform(root.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }


private void WireButton(Transform root, string name, UnityEngine.Events.UnityAction action)
    {
        var t = FindTransform(root, name);
        if (t == null) return;
        var b = t.GetComponent<Button>();
        if (b != null) b.onClick.AddListener(action);
    }


private void BindExisting(Transform root)
    {
        moneyText = FindComp<TextMeshProUGUI>(root, "MoneyText");
        ageText = FindComp<TextMeshProUGUI>(root, "AgeText");
        stageText = FindComp<TextMeshProUGUI>(root, "StageLabel");
        var fillT = FindTransform(root, "GrowthFill");
        if (fillT != null) gaugeFillRect = fillT as RectTransform;
        var panelT = FindTransform(root, "StatusPanel");
        if (panelT != null) statusPanel = panelT.gameObject;

        var toggleT = FindTransform(root, "StatusToggle");
        if (toggleT != null)
        {
            var tb = toggleT.GetComponent<Button>();
            if (tb != null) { tb.onClick.AddListener(ToggleStatus); }
            toggleLabel = FindComp<TextMeshProUGUI>(toggleT, "Label");
        }

        WireButton(root, "Btn_サポート", OnSupport);
        WireButton(root, "Btn_カレンダー", OnCalendar);
        WireButton(root, "Btn_セーブ", OnSave);
        WireButton(root, "Btn_設定", OnSettings);
    }


public void EditorRebuild()
    {
        var old = GameObject.Find("HUD_Canvas");
        if (old != null) DestroyImmediate(old);
        jpFont = CreateJapaneseFont();
        roundSprite = null;
        EnsureEventSystem();
        BuildUI();
    }
}
