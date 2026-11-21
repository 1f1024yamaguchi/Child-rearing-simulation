using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「子どもの名前を決める」パネルの UI コンポーネント。
/// 流れの制御（ChildNamingFlow）とは分離し、UI 表示・入力受付だけを担当する。
/// 時間の停止は OnEnable / OnDisable で自分自身が行う（AGENTS.md ルール8）。
/// </summary>
public class ChildNamePanelUI : MonoBehaviour
{
    // ─── 設定 ──────────────────────────────────────────────
    [Header("文言（Inspector から変更可）")]
    [Tooltip("見出しに出す文字")]
    [SerializeField] private string headingText = "子どもが生まれた！！！";

    [Tooltip("案内に出す文字")]
    [SerializeField] private string guideText = "名前を決めよう";

    [Tooltip("名前欄の初期値")]
    [SerializeField] private string defaultName = "みらい";

    [Header("名前の制限")]
    [Tooltip("名前の最小文字数")]
    [SerializeField] private int minNameLength = 1;

    [Tooltip("名前の最大文字数（入力欄にも設定する）")]
    [SerializeField] private int maxNameLength = 8;

    [Header("候補ボタン")]
    [Tooltip("候補名のリスト（ボタンとして並べる）")]
    [SerializeField] private List<string> nameCandidates = new List<string> { "みらい", "はると", "ゆい", "そうた", "めい", "あおい" };

    [Tooltip("候補ボタンのプレハブ（Button + TMP を持つもの）")]
    [SerializeField] private GameObject candidateButtonPrefab;

    [Tooltip("候補ボタンを並べる親 Transform（GridLayoutGroup 推奨）")]
    [SerializeField] private Transform candidateButtonContainer;

    [Header("参照：UI 部品")]
    [Tooltip("見出しを出す TMP")]
    [SerializeField] private TextMeshProUGUI headingLabel;

    [Tooltip("案内を出す TMP")]
    [SerializeField] private TextMeshProUGUI guideLabel;

    [Tooltip("名前を入力する TMP_InputField")]
    [SerializeField] private TMP_InputField nameInputField;

    [Tooltip("検証エラーの理由を出す TMP（小さく出す）")]
    [SerializeField] private TextMeshProUGUI errorLabel;

    [Tooltip("「決定」ボタン")]
    [SerializeField] private Button decideButton;

    [Tooltip("「ランダム」ボタン")]
    [SerializeField] private Button randomButton;

    [Header("確認ダイアログ")]
    [Tooltip("確認ダイアログ（ConfirmDialogUI。文言は外から変える）")]
    [SerializeField] private ConfirmDialogUI confirmDialog;

    [Tooltip("確認ダイアログの TMP（「○○でいいですか？」と書き換える）")]
    [SerializeField] private TextMeshProUGUI confirmMessageLabel;

    [Header("自由入力の ON/OFF")]
    [Tooltip("false にすると nameInputField を非表示にする（WebGL / iPad 動作確認が取れたら ON）")]
    [SerializeField] private bool useFreeInput = true;

    // ─── 内部 ──────────────────────────────────────────────
    private Action<string> onDecidedCallback;
    private int lastRandomIndex = -1;   // 直前と同じ候補が連続しないようにする
    private GameObject runtimeConfirmRoot;
    private TextMeshProUGUI runtimeConfirmMessage;
    private Button runtimeConfirmYes;
    private Button runtimeConfirmNo;

    // ─── 時間の停止（AGENTS.md ルール8）────────────────────
    private void OnEnable()
    {
        if (GameClock.I != null) GameClock.I.PushPause();
    }

    private void OnDisable()
    {
        if (GameClock.I != null) GameClock.I.PopPause();
    }

    // ─── 外部 API ───────────────────────────────────────────
    /// <summary>
    /// シーンに参照が無い場合でも、実行時に名前決定画面を生成する。
    /// シーン編集済みの参照がある場合は、既存UIを壊さない。
    /// </summary>
    public void EnsureRuntimeUI()
    {
        if (headingLabel != null && guideLabel != null && decideButton != null) return;

        var font = Resources.Load<TMP_FontAsset>("JPFont") ?? TMP_Settings.defaultFontAsset;

        var canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
        }
        if (GetComponent<CanvasScaler>() == null)
        {
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800f, 600f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        var rootRect = transform as RectTransform;
        if (rootRect != null)
        {
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
        }

        var dim = MakeObject("Dim", transform, typeof(Image));
        var dimRect = dim.GetComponent<RectTransform>();
        Stretch(dimRect);
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.60f);

        var sheet = MakeObject("Sheet", transform, typeof(Image));
        var sheetRect = sheet.GetComponent<RectTransform>();
        sheetRect.anchorMin = new Vector2(0.5f, 0.5f);
        sheetRect.anchorMax = new Vector2(0.5f, 0.5f);
        sheetRect.pivot = new Vector2(0.5f, 0.5f);
        sheetRect.sizeDelta = new Vector2(560f, 520f);
        sheet.GetComponent<Image>().color = new Color(1f, 0.98f, 0.92f, 1f);

        headingLabel = MakeText("Heading", sheet.transform, headingText, 32, FontStyles.Bold, font);
        var headingRect = headingLabel.rectTransform;
        headingRect.anchorMin = new Vector2(0f, 1f);
        headingRect.anchorMax = new Vector2(1f, 1f);
        headingRect.pivot = new Vector2(0.5f, 1f);
        headingRect.anchoredPosition = new Vector2(0f, -25f);
        headingRect.sizeDelta = new Vector2(-40f, 50f);

        guideLabel = MakeText("Guide", sheet.transform, guideText, 24, FontStyles.Normal, font);
        var guideRect = guideLabel.rectTransform;
        guideRect.anchorMin = new Vector2(0f, 1f);
        guideRect.anchorMax = new Vector2(1f, 1f);
        guideRect.pivot = new Vector2(0.5f, 1f);
        guideRect.anchoredPosition = new Vector2(0f, -78f);
        guideRect.sizeDelta = new Vector2(-40f, 40f);

        var candidateContainerObject = MakeObject("Candidates", sheet.transform);
        candidateButtonContainer = candidateContainerObject.transform;
        var candidateRect = candidateContainerObject.GetComponent<RectTransform>();
        candidateRect.anchorMin = new Vector2(0.5f, 1f);
        candidateRect.anchorMax = new Vector2(0.5f, 1f);
        candidateRect.pivot = new Vector2(0.5f, 1f);
        candidateRect.anchoredPosition = new Vector2(0f, -130f);
        candidateRect.sizeDelta = new Vector2(500f, 150f);
        var grid = candidateContainerObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(150f, 54f);
        grid.spacing = new Vector2(12f, 12f);
        grid.childAlignment = TextAnchor.MiddleCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;

        var nameInputObject = MakeObject("NameInput", sheet.transform, typeof(Image), typeof(TMP_InputField));
        var nameInputRect = nameInputObject.GetComponent<RectTransform>();
        nameInputRect.anchorMin = new Vector2(0.5f, 0f);
        nameInputRect.anchorMax = new Vector2(0.5f, 0f);
        nameInputRect.pivot = new Vector2(0.5f, 0f);
        nameInputRect.anchoredPosition = new Vector2(0f, 155f);
        nameInputRect.sizeDelta = new Vector2(280f, 54f);
        nameInputObject.GetComponent<Image>().color = Color.white;
        nameInputField = nameInputObject.GetComponent<TMP_InputField>();
        var nameText = MakeText("Text", nameInputObject.transform, "", 24, FontStyles.Normal, font);
        Stretch(nameText.rectTransform);
        nameText.rectTransform.offsetMin = new Vector2(10f, 4f);
        nameText.rectTransform.offsetMax = new Vector2(-10f, -4f);
        nameText.color = Color.black;
        nameInputField.textComponent = nameText;

        errorLabel = MakeText("Error", sheet.transform, "", 16, FontStyles.Normal, font);
        var errorRect = errorLabel.rectTransform;
        errorRect.anchorMin = new Vector2(0.5f, 0f);
        errorRect.anchorMax = new Vector2(0.5f, 0f);
        errorRect.pivot = new Vector2(0.5f, 0f);
        errorRect.anchoredPosition = new Vector2(0f, 112f);
        errorRect.sizeDelta = new Vector2(500f, 32f);
        errorLabel.alignment = TextAlignmentOptions.Center;
        errorLabel.color = new Color(0.65f, 0.1f, 0.1f);

        randomButton = MakeButton("RandomButton", sheet.transform, "ランダム", font);
        randomButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-145f, 52f);
        randomButton.GetComponent<RectTransform>().sizeDelta = new Vector2(250f, 48f);
        randomButton.onClick.AddListener(OnClickRandom);

        decideButton = MakeButton("DecideButton", sheet.transform, "決定", font);
        decideButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(145f, 52f);
        decideButton.GetComponent<RectTransform>().sizeDelta = new Vector2(250f, 48f);
        decideButton.onClick.AddListener(OnClickDecide);

        candidateButtonPrefab = MakeCandidatePrefab(font);

        BuildRuntimeConfirm(font);
        gameObject.SetActive(false);
    }

    private void BuildRuntimeConfirm(TMP_FontAsset font)
    {
        runtimeConfirmRoot = MakeObject("Confirm", transform, typeof(Image));
        Stretch(runtimeConfirmRoot.GetComponent<RectTransform>());
        runtimeConfirmRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

        var sheet = MakeObject("ConfirmSheet", runtimeConfirmRoot.transform, typeof(Image));
        var sheetRect = sheet.GetComponent<RectTransform>();
        sheetRect.anchorMin = new Vector2(0.5f, 0.5f);
        sheetRect.anchorMax = new Vector2(0.5f, 0.5f);
        sheetRect.pivot = new Vector2(0.5f, 0.5f);
        sheetRect.sizeDelta = new Vector2(440f, 220f);
        sheet.GetComponent<Image>().color = new Color(1f, 0.98f, 0.92f, 1f);

        runtimeConfirmMessage = MakeText("ConfirmMessage", sheet.transform, "", 26, FontStyles.Normal, font);
        runtimeConfirmMessage.color = Color.black;
        runtimeConfirmMessage.alignment = TextAlignmentOptions.Center;
        runtimeConfirmMessage.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        runtimeConfirmMessage.rectTransform.anchorMax = new Vector2(1f, 1f);
        runtimeConfirmMessage.rectTransform.offsetMin = new Vector2(30f, 0f);
        runtimeConfirmMessage.rectTransform.offsetMax = new Vector2(-30f, -20f);

        runtimeConfirmYes = MakeButton("Yes", sheet.transform, "はい", font);
        runtimeConfirmYes.GetComponentInChildren<TextMeshProUGUI>().color = Color.black;
        runtimeConfirmYes.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0f);
        runtimeConfirmYes.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0f);
        runtimeConfirmYes.GetComponent<RectTransform>().pivot = new Vector2(1f, 0f);
        runtimeConfirmYes.GetComponent<RectTransform>().anchoredPosition = new Vector2(-8f, 25f);
        runtimeConfirmYes.GetComponent<RectTransform>().sizeDelta = new Vector2(150f, 48f);

        runtimeConfirmNo = MakeButton("No", sheet.transform, "やり直す", font);
        runtimeConfirmNo.GetComponentInChildren<TextMeshProUGUI>().color = Color.black;
        runtimeConfirmNo.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0f);
        runtimeConfirmNo.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0f);
        runtimeConfirmNo.GetComponent<RectTransform>().pivot = new Vector2(0f, 0f);
        runtimeConfirmNo.GetComponent<RectTransform>().anchoredPosition = new Vector2(8f, 25f);
        runtimeConfirmNo.GetComponent<RectTransform>().sizeDelta = new Vector2(150f, 48f);
        runtimeConfirmRoot.SetActive(false);
    }

    private GameObject MakeCandidatePrefab(TMP_FontAsset font)
    {
        var prefab = MakeObject("CandidateButtonPrefab", transform, typeof(Image), typeof(Button));
        prefab.GetComponent<Image>().color = new Color(0.84f, 0.94f, 0.98f, 1f);
        MakeText("Label", prefab.transform, "", 22, FontStyles.Normal, font).rectTransform.sizeDelta = Vector2.zero;
        prefab.SetActive(false);
        return prefab;
    }

    private static GameObject MakeObject(string name, Transform parent, params System.Type[] components)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        foreach (var type in components)
        {
            if (type != null) go.AddComponent(type);
        }
        return go;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static TextMeshProUGUI MakeText(string name, Transform parent, string text, int size, FontStyles style, TMP_FontAsset font)
    {
        var go = MakeObject(name, parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.overflowMode = TextOverflowModes.Overflow;
        if (font != null) tmp.font = font;
        return tmp;
    }

    private static Button MakeButton(string name, Transform parent, string text, TMP_FontAsset font)
    {
        var go = MakeObject(name, parent, typeof(Image), typeof(Button));
        go.GetComponent<Image>().color = new Color(0.16f, 0.83f, 0.96f, 1f);
        var button = go.GetComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        var label = MakeText("Label", go.transform, text, 22, FontStyles.Bold, font);
        Stretch(label.rectTransform);
        return button;
    }

    /// <summary>パネルを開く。決定されたら onDecided(name) が呼ばれる。</summary>
    public void Open(Action<string> onDecided)
    {
        EnsureRuntimeUI();
        onDecidedCallback = onDecided;
        gameObject.SetActive(true);   // OnEnable で時間が止まる

        // 見出し・案内
        if (headingLabel != null) headingLabel.text = headingText;
        if (guideLabel != null)   guideLabel.text   = guideText;

        // 自由入力欄
        if (nameInputField != null)
        {
            nameInputField.gameObject.SetActive(useFreeInput);
            if (useFreeInput)
            {
                if (nameInputField.textComponent != null)
                    nameInputField.textComponent.color = Color.black;
                nameInputField.characterLimit = maxNameLength;
                nameInputField.text = defaultName;
                nameInputField.onValueChanged.RemoveAllListeners();
                nameInputField.onValueChanged.AddListener(OnInputChanged);
            }
        }

        // 候補ボタンを生成
        BuildCandidateButtons();

        // 初期値を名前欄に入れる
        SetNameField(defaultName);

        // エラーを消す
        if (errorLabel != null) errorLabel.text = string.Empty;

        // 決定ボタンの状態を更新
        RefreshDecideButton();
    }

    /// <summary>パネルを閉じる。OnDisable で時間が戻る。</summary>
    public void Close()
    {
        gameObject.SetActive(false);
    }

    // ─── 候補ボタン ─────────────────────────────────────────
    private void BuildCandidateButtons()
    {
        if (candidateButtonContainer == null || candidateButtonPrefab == null) return;

        // 既存のボタンをすべて削除
        foreach (Transform child in candidateButtonContainer)
            Destroy(child.gameObject);

        // 候補ごとにボタンを生成
        foreach (string candidate in nameCandidates)
        {
            var go = Instantiate(candidateButtonPrefab, candidateButtonContainer);
            go.SetActive(true);
            var label = go.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = candidate;
                Stretch(label.rectTransform);
            }

            var btn = go.GetComponent<Button>();
            if (btn != null)
            {
                string captured = candidate;   // クロージャ対策
                btn.onClick.AddListener(() => OnCandidateClicked(captured));
            }
        }
    }

    // ─── イベントハンドラ ───────────────────────────────────
    private void OnCandidateClicked(string candidate)
    {
        SetNameField(candidate);
    }

    private void OnInputChanged(string value)
    {
        RefreshDecideButton();
    }

    /// <summary>「ランダム」ボタンの OnClick に Inspector で登録する。</summary>
    public void OnClickRandom()
    {
        if (nameCandidates == null || nameCandidates.Count == 0) return;
        if (nameCandidates.Count == 1)
        {
            SetNameField(nameCandidates[0]);
            return;
        }

        // 直前と同じインデックスを引き直す
        int index;
        do
        {
            index = UnityEngine.Random.Range(0, nameCandidates.Count);
        } while (index == lastRandomIndex);

        lastRandomIndex = index;
        SetNameField(nameCandidates[index]);
    }

    /// <summary>「決定」ボタンの OnClick に Inspector で登録する。</summary>
    public void OnClickDecide()
    {
        string name = CurrentInputName();
        if (!ChildNameValidator.Validate(name, minNameLength, maxNameLength, out string err))
        {
            ShowError(err);
            return;
        }

        // 確認ダイアログを開く
        if (confirmDialog != null)
        {
            if (confirmMessageLabel != null)
            {
                confirmMessageLabel.color = Color.black;
                confirmMessageLabel.text = $"「{name}」でいいですか？";
            }

            confirmDialog.Open(
                onYes: () => DecideAndClose(name),
                onNo:  null   // 「やり直す」は何もしない（パネルに戻るだけ）
            );
        }
        else if (runtimeConfirmRoot != null)
        {
            if (runtimeConfirmMessage != null)
                runtimeConfirmMessage.text = $"「{name}」でいいですか？";

            runtimeConfirmYes.onClick.RemoveAllListeners();
            runtimeConfirmYes.onClick.AddListener(() => {
                runtimeConfirmRoot.SetActive(false);
                DecideAndClose(name);
            });

            runtimeConfirmNo.onClick.RemoveAllListeners();
            runtimeConfirmNo.onClick.AddListener(() => runtimeConfirmRoot.SetActive(false));
            runtimeConfirmRoot.SetActive(true);
        }
        else
        {
            // ConfirmDialogUI が無い場合は直接決定（フォールバック）
            DecideAndClose(name);
        }
    }

    // ─── 内部ヘルパー ────────────────────────────────────────
    /// <summary>名前欄の現在の入力値を返す（前後の空白を削った状態）。</summary>
    private string CurrentInputName()
    {
        if (nameInputField != null && nameInputField.gameObject.activeSelf)
            return (nameInputField.text ?? string.Empty).Trim();
        // 自由入力が OFF の場合、候補ボタンで選んだ値を _currentCandidate に記録する
        return _currentCandidate.Trim();
    }

    private string _currentCandidate = string.Empty;

    private void SetNameField(string name)
    {
        _currentCandidate = name;
        if (nameInputField != null && nameInputField.gameObject.activeSelf)
            nameInputField.SetTextWithoutNotify(name);
        RefreshDecideButton();
    }

    private void RefreshDecideButton()
    {
        if (decideButton == null) return;
        string name = CurrentInputName();
        bool ok = ChildNameValidator.Validate(name, minNameLength, maxNameLength, out string err);
        decideButton.interactable = ok;
        if (errorLabel != null)
            errorLabel.text = ok ? string.Empty : err;
    }

    private void ShowError(string message)
    {
        if (errorLabel != null) errorLabel.text = message;
    }

    private void DecideAndClose(string name)
    {
        var cb = onDecidedCallback;
        onDecidedCallback = null;
        Close();           // OnDisable で時間が戻る
        cb?.Invoke(name);
    }
}
