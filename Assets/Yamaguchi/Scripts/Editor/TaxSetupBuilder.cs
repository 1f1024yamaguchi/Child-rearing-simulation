#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 税金の仕組み（taxes.md 7章）を、メニューから1回で組み立てる道具。
/// 1. 税金の表と控除のアセットを Assets/Yamaguchi/Data/Tax に作る（もうあれば作らない＝調整した数値を消さない）
/// 2. 申告画面のプレハブを作る（再実行するとプレハブだけ作り直す）
/// 3. 開いているシーンの GameManager（HouseholdIncome が付いている物）に TaxManager を付け、Canvas に申告画面を置く
/// 作ったあとは Inspector で見た目や数値を自由に直してよい。
/// </summary>
public static class TaxSetupBuilder
{
    private const string DataFolder = "Assets/Yamaguchi/Data/Tax";
    private const string TablePath = DataFolder + "/TaxTable.asset";
    private const string DialogPrefabPath = "Assets/Yamaguchi/Prefabs/UI/TaxFilingDialog.prefab";

    private static readonly Color Ink = new Color(0.20f, 0.17f, 0.14f);
    private static readonly Color SubInk = new Color(0.40f, 0.36f, 0.32f);
    private static readonly Color Paper = new Color(0.99f, 0.97f, 0.92f);
    private static readonly Color RowBg = new Color(1f, 1f, 1f, 0.85f);
    private static readonly Color Orange = new Color(0.97f, 0.62f, 0.15f);
    private static readonly Color Gray = new Color(0.72f, 0.72f, 0.75f);
    private static readonly Color Blue = new Color(0.55f, 0.75f, 0.95f);

    [MenuItem("子育て/税金/まとめてセットアップ")]
    public static void SetupAll()
    {
        var table = CreateTable();
        var deductions = CreateDeductions();
        var prefab = BuildDialogPrefab();
        PlaceInScene(table, deductions, prefab);
    }

    [MenuItem("子育て/税金/申告画面のプレハブだけ作り直す")]
    public static void RebuildPrefabOnly() => BuildDialogPrefab();

    // ------------------------------------------------------------
    // 1. アセット
    // ------------------------------------------------------------

    private static TaxTable CreateTable()
    {
        EnsureFolder(DataFolder);
        var table = AssetDatabase.LoadAssetAtPath<TaxTable>(TablePath);
        if (table != null) return table;
        table = ScriptableObject.CreateInstance<TaxTable>();   // 既定値＝taxes.md 3.1 のたたき台
        AssetDatabase.CreateAsset(table, TablePath);
        AssetDatabase.SaveAssets();
        Debug.Log($"TaxSetupBuilder: 税金の表を作りました：{TablePath}");
        return table;
    }

    private static List<TaxDeduction> CreateDeductions()
    {
        var list = new List<TaxDeduction>
        {
            Deduction("Deduction_DependentChild", "dependent_child", "扶養控除（子）",
                "16〜18歳の子どもを養っているとき。15歳までは児童手当があるので、この控除はありません。",
                DeductionRoute.YearEnd, DeductionKind.Fixed, 380000, 330000, 16, 18, 0),
            Deduction("Deduction_SpecificDependent", "specific_dependent", "特定扶養控除（子）",
                "19〜22歳の子どもを養っているとき。大学生の年代は学費がかかるので、控除が大きくなります。",
                DeductionRoute.YearEnd, DeductionKind.Fixed, 630000, 450000, 19, 22, 0),
            Deduction("Deduction_Spouse", "spouse", "配偶者控除",
                "パートナーの所得が少ないとき（給料なら年およそ132万円以下）。自分の所得が多いと使えません。",
                DeductionRoute.YearEnd, DeductionKind.Spouse, 380000, 330000, -1, -1, 9000000),
            Deduction("Deduction_Medical", "medical", "医療費控除",
                "1年の医療費が10万円をこえたとき。確定申告でしか出せません。支援で払わなくてよかった分は入りません。",
                DeductionRoute.TaxReturnOnly, DeductionKind.Medical, 0, 0, -1, -1, 0),
        };
        AssetDatabase.SaveAssets();
        return list;
    }

    private static TaxDeduction Deduction(string file, string id, string title, string desc,
        DeductionRoute route, DeductionKind kind, int incomeAmt, int residentAmt, int ageMin, int ageMax, int claimantMax)
    {
        string path = $"{DataFolder}/{file}.asset";
        var d = AssetDatabase.LoadAssetAtPath<TaxDeduction>(path);
        if (d != null) return d;   // もうあれば触らない（調整した数値を消さない）

        d = ScriptableObject.CreateInstance<TaxDeduction>();
        d.id = id;
        d.title = title;
        d.description = desc;
        d.route = route;
        d.kind = kind;
        d.incomeTaxAmount = incomeAmt;
        d.residentTaxAmount = residentAmt;
        d.childAgeMin = ageMin;
        d.childAgeMax = ageMax;
        d.claimantIncomeMax = claimantMax;
        AssetDatabase.CreateAsset(d, path);
        Debug.Log($"TaxSetupBuilder: 控除を作りました：{path}");
        return d;
    }

    // ------------------------------------------------------------
    // 2. 申告画面のプレハブ
    // ------------------------------------------------------------

    private static TaxFilingDialog BuildDialogPrefab()
    {
        var font = Resources.Load<TMP_FontAsset>("JPFont");
        if (font == null) Debug.LogWarning("TaxSetupBuilder: JPFont が Resources に見つかりません。日本語が表示されません");

        var root = NewUI("TaxFilingDialog", null);
        Stretch(root);

        var window = NewUI("Window", root.transform);
        Stretch(window);

        var dim = NewUI("Dim", window.transform);
        Stretch(dim);
        dim.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);   // 後ろを押せないようにするだけ（閉じない）

        var box = NewUI("Box", window.transform);
        var boxRt = (RectTransform)box.transform;
        boxRt.anchorMin = boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(640f, 300f);
        box.AddComponent<Image>().color = Paper;
        SetupVertical(box.AddComponent<VerticalLayoutGroup>(), 10f, new RectOffset(24, 24, 20, 20));
        var fitter = box.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var title = NewText("title", box.transform, font, "年末調整の書類（0歳分）", 24, Ink, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        var body = NewText("body", box.transform, font, "説明", 15, SubInk, TextAlignmentOptions.Left);

        var rowsGo = NewUI("rows", box.transform);
        SetupVertical(rowsGo.AddComponent<VerticalLayoutGroup>(), 8f, new RectOffset(0, 0, 4, 4));
        var rowTemplate = BuildRowTemplate(rowsGo.transform, font);

        var preview = NewText("preview", box.transform, font, "戻ってくる見込み：+0円", 17, new Color(0.85f, 0.45f, 0.05f), TextAlignmentOptions.Center);
        preview.fontStyle = FontStyles.Bold;

        var buttons = NewUI("buttons", box.transform);
        var h = buttons.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 16f;
        h.padding = new RectOffset(0, 0, 6, 0);
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = false;
        var cancel = NewButton("btn_cancel", buttons.transform, font, "出さない", Gray, 48f, out var cancelLabel);
        var ok = NewButton("btn_ok", buttons.transform, font, "提出する", Orange, 48f, out var okLabel);

        var dialog = root.AddComponent<TaxFilingDialog>();
        var so = new SerializedObject(dialog);
        so.FindProperty("window").objectReferenceValue = window;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("bodyText").objectReferenceValue = body;
        so.FindProperty("previewText").objectReferenceValue = preview;
        so.FindProperty("rowsParent").objectReferenceValue = rowsGo.transform;
        so.FindProperty("rowTemplate").objectReferenceValue = rowTemplate;
        so.FindProperty("cancelButton").objectReferenceValue = cancel;
        so.FindProperty("cancelLabel").objectReferenceValue = cancelLabel;
        so.FindProperty("okButton").objectReferenceValue = ok;
        so.FindProperty("okLabel").objectReferenceValue = okLabel;
        so.ApplyModifiedPropertiesWithoutUndo();

        window.SetActive(false);
        rowTemplate.gameObject.SetActive(false);

        EnsureFolder("Assets/Yamaguchi/Prefabs/UI");
        var saved = PrefabUtility.SaveAsPrefabAsset(root, DialogPrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log($"TaxSetupBuilder: 申告画面のプレハブを作りました：{DialogPrefabPath}");
        return saved.GetComponent<TaxFilingDialog>();
    }

    private static TaxDeductionRowUI BuildRowTemplate(Transform parent, TMP_FontAsset font)
    {
        var row = NewUI("row_template", parent);
        row.AddComponent<Image>().color = RowBg;
        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 12f;
        h.padding = new RectOffset(10, 10, 8, 8);
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;

        // チェック
        var toggleGo = NewUI("toggle", row.transform);
        var tle = toggleGo.AddComponent<LayoutElement>();
        tle.preferredWidth = tle.minWidth = 30f;
        tle.preferredHeight = tle.minHeight = 30f;
        var bg = toggleGo.AddComponent<Image>();
        bg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        bg.type = Image.Type.Sliced;
        var check = NewUI("check", toggleGo.transform);
        Stretch(check, 3f);
        var checkImg = check.AddComponent<Image>();
        checkImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
        checkImg.color = Ink;
        var toggle = toggleGo.AddComponent<Toggle>();
        toggle.targetGraphic = bg;
        toggle.graphic = checkImg;
        toggle.isOn = false;

        // 文字（控除名・額・説明）
        var texts = NewUI("texts", row.transform);
        SetupVertical(texts.AddComponent<VerticalLayoutGroup>(), 2f, new RectOffset(0, 0, 0, 0));
        texts.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var title = NewText("title", texts.transform, font, "扶養控除（子）", 18, Ink, TextAlignmentOptions.Left);
        title.fontStyle = FontStyles.Bold;
        var amount = NewText("amount", texts.transform, font, "所得から −38万円", 15, new Color(0.10f, 0.50f, 0.10f), TextAlignmentOptions.Left);
        var desc = NewText("description", texts.transform, font, "説明", 13, SubInk, TextAlignmentOptions.Left);

        // 誰の控除か
        var who = NewButton("btn_who", row.transform, font, "父", Blue, 36f, out var whoLabel);
        var wle = who.GetComponent<LayoutElement>();
        wle.preferredWidth = wle.minWidth = 56f;

        var comp = row.AddComponent<TaxDeductionRowUI>();
        var so = new SerializedObject(comp);
        so.FindProperty("toggle").objectReferenceValue = toggle;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("amountText").objectReferenceValue = amount;
        so.FindProperty("descriptionText").objectReferenceValue = desc;
        so.FindProperty("whoButton").objectReferenceValue = who;
        so.FindProperty("whoLabel").objectReferenceValue = whoLabel;
        so.ApplyModifiedPropertiesWithoutUndo();
        return comp;
    }

    // ------------------------------------------------------------
    // 3. シーンに置く
    // ------------------------------------------------------------

    private static void PlaceInScene(TaxTable table, List<TaxDeduction> deductions, TaxFilingDialog prefab)
    {
        var income = Object.FindFirstObjectByType<HouseholdIncome>(FindObjectsInactive.Include);
        if (income == null)
        {
            Debug.LogWarning("TaxSetupBuilder: シーンに HouseholdIncome が見つかりません。TaxManager は手で GameManager に付けてください");
        }
        else
        {
            var mgr = income.GetComponent<TaxManager>();
            if (mgr == null) mgr = Undo.AddComponent<TaxManager>(income.gameObject);
            var so = new SerializedObject(mgr);
            var tableProp = so.FindProperty("table");
            if (tableProp.objectReferenceValue == null) tableProp.objectReferenceValue = table;
            var list = so.FindProperty("deductions");
            if (list.arraySize == 0)
            {
                list.arraySize = deductions.Count;
                for (int i = 0; i < deductions.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = deductions[i];
            }
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(income.gameObject.scene);
            Debug.Log($"TaxSetupBuilder: {income.gameObject.name} に TaxManager を付けました");
        }

        if (Object.FindFirstObjectByType<TaxFilingDialog>(FindObjectsInactive.Include) != null)
        {
            Debug.Log("TaxSetupBuilder: 申告画面はもうシーンにあります（プレハブの変更は自動で反映されます）");
            return;
        }
        var canvas = GameObject.Find("Canvas");
        if (canvas == null || canvas.GetComponent<Canvas>() == null)
        {
            Debug.LogWarning("TaxSetupBuilder: Canvas が見つかりません。TaxFilingDialog のプレハブを Canvas の直下に手で置いてください");
            return;
        }
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, canvas.transform);
        Undo.RegisterCreatedObjectUndo(inst, "Create TaxFilingDialog");
        Stretch(inst);
        inst.transform.SetAsLastSibling();
        EditorSceneManager.MarkSceneDirty(inst.scene);
        Debug.Log("TaxSetupBuilder: Canvas に申告画面を置きました。シーンを保存してください");
    }

    // ------------------------------------------------------------
    // 部品
    // ------------------------------------------------------------

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
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

    private static void SetupVertical(VerticalLayoutGroup v, float spacing, RectOffset padding)
    {
        v.spacing = spacing;
        v.padding = padding;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
    }

    /// <summary>文字。Overflow は既定のまま（Ellipsis にしない）。</summary>
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

    private static Button NewButton(string name, Transform parent, TMP_FontAsset font, string label, Color color, float height, out TextMeshProUGUI labelText)
    {
        var go = NewUI(name, parent);
        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;

        labelText = NewText("Label", go.transform, font, label, 20, Ink, TextAlignmentOptions.Center);
        Stretch(labelText.gameObject);
        return btn;
    }
}
#endif
