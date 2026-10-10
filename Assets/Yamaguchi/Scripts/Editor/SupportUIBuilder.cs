#if UNITY_EDITOR
using Maruyama.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ステップ3後半の UI（確認ダイアログ・通知・ボタンのバッジ）を、メニューから1回で作る道具。
/// 手でプレハブを組むと階層・レイアウト設定の取りこぼしが出やすいので、コードで作る。
/// 作ったあとは Inspector で見た目（色・大きさ）を自由に直してよい。再実行すると作り直す（ダイアログのプレハブのみ上書き）。
/// </summary>
public static class SupportUIBuilder
{
    private const string DialogPrefabPath = "Assets/Yamaguchi/Prefabs/UI/SupportApplyDialog.prefab";
    private const string ChipPrefabPath = "Assets/Maruyama/Prefabs/Events/EffectChip.prefab";
    private const string DefaultButtonPath = "Canvas/Buttons/suport";

    private static readonly Color Ink = new Color(0.20f, 0.17f, 0.14f);
    private static readonly Color Paper = new Color(0.99f, 0.97f, 0.92f);
    private static readonly Color Orange = new Color(0.97f, 0.62f, 0.15f);
    private static readonly Color Gray = new Color(0.72f, 0.72f, 0.75f);

    // ------------------------------------------------------------
    // 1. 確認ダイアログのプレハブ
    // ------------------------------------------------------------

    [MenuItem("子育て/サポートUI/1. SupportApplyDialog プレハブを作る")]
    public static void BuildDialogPrefab()
    {
        var font = LoadFont();
        var chipPrefab = AssetDatabase.LoadAssetAtPath<EffectChipUI>(ChipPrefabPath);
        if (chipPrefab == null) Debug.LogWarning($"SupportUIBuilder: 効果チップが見つかりません：{ChipPrefabPath}（チップは出ません）");

        var root = NewUI("SupportApplyDialog", null);
        Stretch(root);

        var window = NewUI("Window", root.transform);
        Stretch(window);

        var dim = NewUI("Dim", window.transform);
        Stretch(dim);
        var dimImage = dim.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.6f);
        var dimButton = dim.AddComponent<Button>();
        dimButton.targetGraphic = dimImage;
        dimButton.transition = Selectable.Transition.None;

        var box = NewUI("Box", window.transform);
        var boxRt = (RectTransform)box.transform;
        boxRt.anchorMin = boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(580f, 200f);
        var boxImage = box.AddComponent<Image>();
        boxImage.color = Paper;
        var boxLayout = box.AddComponent<VerticalLayoutGroup>();
        SetupVertical(boxLayout, 12f, new RectOffset(28, 28, 24, 24));
        var fitter = box.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var title = NewText("title", box.transform, font, "制度名を申請しますか？", 26, Ink, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        var timing = NewText("timing", box.transform, font, "（来月から）", 18, new Color(0.85f, 0.45f, 0.05f), TextAlignmentOptions.Center);

        var meritSection = NewSection("merit_section", box.transform, font, "もらえるもの", new Color(0.10f, 0.50f, 0.10f), out var meritRow);
        var costSection = NewSection("cost_section", box.transform, font, "かかるもの", new Color(0.70f, 0.10f, 0.10f), out var costRow);

        var tier = NewText("tier_text", box.transform, font, "", 15, Ink, TextAlignmentOptions.Left);

        var noteSection = NewUI("note_section", box.transform);
        SetupVertical(noteSection.AddComponent<VerticalLayoutGroup>(), 4f, new RectOffset(0, 0, 0, 0));
        NewText("note_header", noteSection.transform, font, "注意", 16, new Color(0.70f, 0.10f, 0.10f), TextAlignmentOptions.Left).fontStyle = FontStyles.Bold;
        var note = NewText("note_text", noteSection.transform, font, "", 15, new Color(0.70f, 0.10f, 0.10f), TextAlignmentOptions.Left);

        var buttons = NewUI("buttons", box.transform);
        var buttonsLayout = buttons.AddComponent<HorizontalLayoutGroup>();
        buttonsLayout.spacing = 16f;
        buttonsLayout.padding = new RectOffset(0, 0, 8, 0);
        buttonsLayout.childAlignment = TextAnchor.MiddleCenter;
        buttonsLayout.childControlWidth = true;
        buttonsLayout.childControlHeight = true;
        buttonsLayout.childForceExpandWidth = true;
        buttonsLayout.childForceExpandHeight = false;

        var cancel = NewButton("btn_cancel", buttons.transform, font, "やめる", Gray, out _);
        var ok = NewButton("btn_ok", buttons.transform, font, "申請する", Orange, out var okLabel);

        // SupportApplyDialog の項目をつなぐ
        var dialog = root.AddComponent<SupportApplyDialog>();
        var so = new SerializedObject(dialog);
        so.FindProperty("window").objectReferenceValue = window;
        so.FindProperty("dimButton").objectReferenceValue = dimButton;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("timingText").objectReferenceValue = timing;
        so.FindProperty("meritSection").objectReferenceValue = meritSection;
        so.FindProperty("meritRow").objectReferenceValue = meritRow;
        so.FindProperty("costSection").objectReferenceValue = costSection;
        so.FindProperty("costRow").objectReferenceValue = costRow;
        so.FindProperty("tierText").objectReferenceValue = tier;
        so.FindProperty("noteSection").objectReferenceValue = noteSection;
        so.FindProperty("noteText").objectReferenceValue = note;
        so.FindProperty("cancelButton").objectReferenceValue = cancel;
        so.FindProperty("okButton").objectReferenceValue = ok;
        so.FindProperty("okLabel").objectReferenceValue = okLabel;
        so.FindProperty("chipPrefab").objectReferenceValue = chipPrefab;
        so.ApplyModifiedPropertiesWithoutUndo();

        var saved = PrefabUtility.SaveAsPrefabAsset(root, DialogPrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();

        // サポート画面に割り当てる（まだ空のときだけ）
        var panel = Object.FindFirstObjectByType<SupportPanelController>(FindObjectsInactive.Include);
        if (panel != null)
        {
            var pso = new SerializedObject(panel);
            var prop = pso.FindProperty("dialogPrefab");
            if (prop.objectReferenceValue == null)
            {
                prop.objectReferenceValue = saved.GetComponent<SupportApplyDialog>();
                pso.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
                Debug.Log("SupportUIBuilder: SupportPanelController の Dialog Prefab に割り当てました。シーンを保存してください");
            }
        }
        else
        {
            Debug.LogWarning("SupportUIBuilder: シーンに SupportPanelController が見つかりません。Dialog Prefab は手で割り当ててください");
        }

        Selection.activeObject = saved;
        Debug.Log($"SupportUIBuilder: ダイアログのプレハブを作りました：{DialogPrefabPath}");
    }

    // ------------------------------------------------------------
    // 2. 通知（Canvas の直下）
    // ------------------------------------------------------------

    [MenuItem("子育て/サポートUI/2. SupportToast を Canvas の直下に作る")]
    public static void BuildToast()
    {
        var canvas = GameObject.Find("Canvas");
        if (canvas == null || canvas.GetComponent<Canvas>() == null)
        {
            Debug.LogError("SupportUIBuilder: Canvas が見つかりません（シーンに Canvas という名前の Canvas が必要です）");
            return;
        }
        if (Object.FindFirstObjectByType<SupportToast>(FindObjectsInactive.Include) != null)
        {
            Debug.LogWarning("SupportUIBuilder: SupportToast はすでにあります（作りませんでした）");
            return;
        }

        var font = LoadFont();

        var root = NewUI("SupportToast", canvas.transform);
        Stretch(root);
        Undo.RegisterCreatedObjectUndo(root, "Create SupportToast");

        var panel = NewUI("Panel", root.transform);
        var rt = (RectTransform)panel.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -70f);
        rt.sizeDelta = new Vector2(620f, 56f);
        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.12f, 0.18f, 0.92f);
        bg.raycastTarget = false;
        var group = panel.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        var label = NewText("label", panel.transform, font, "", 20, Color.white, TextAlignmentOptions.Center);
        Stretch(label.gameObject, 12f);

        var toast = root.AddComponent<SupportToast>();
        var so = new SerializedObject(toast);
        so.FindProperty("group").objectReferenceValue = group;
        so.FindProperty("label").objectReferenceValue = label;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("SupportUIBuilder: SupportToast を作りました。シーンを保存してください");
    }

    // ------------------------------------------------------------
    // 3. ［サポート］ボタンのバッジ
    // ------------------------------------------------------------

    [MenuItem("子育て/サポートUI/3. ［サポート］ボタンにバッジを付ける")]
    public static void BuildBadge()
    {
        // 選んでいるボタンがあればそれ、無ければ Canvas/Buttons/suport
        GameObject button = Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<Button>() != null
            ? Selection.activeGameObject
            : GameObject.Find(DefaultButtonPath);
        if (button == null)
        {
            Debug.LogError($"SupportUIBuilder: ボタンが見つかりません。{DefaultButtonPath} を選んでからもう一度実行してください");
            return;
        }
        if (button.GetComponent<SupportButtonBadge>() != null)
        {
            Debug.LogWarning("SupportUIBuilder: このボタンにはもうバッジが付いています");
            return;
        }

        var font = LoadFont();

        var badge = NewUI("badge", button.transform);
        Undo.RegisterCreatedObjectUndo(badge, "Create badge");
        var rt = (RectTransform)badge.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(-8f, -8f);
        rt.sizeDelta = new Vector2(34f, 34f);
        var img = badge.AddComponent<Image>();
        img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        img.color = new Color(0.85f, 0.15f, 0.15f);
        img.raycastTarget = false;

        var count = NewText("count", badge.transform, font, "0", 20, Color.white, TextAlignmentOptions.Center);
        count.fontStyle = FontStyles.Bold;
        Stretch(count.gameObject);

        Undo.AddComponent<SupportButtonBadge>(button);
        var comp = button.GetComponent<SupportButtonBadge>();
        var so = new SerializedObject(comp);
        so.FindProperty("badgeRoot").objectReferenceValue = badge;
        so.FindProperty("countText").objectReferenceValue = count;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(button.scene);
        Debug.Log($"SupportUIBuilder: {button.name} にバッジを付けました。シーンを保存してください");
    }

    // ------------------------------------------------------------
    // 部品
    // ------------------------------------------------------------

    private static TMP_FontAsset LoadFont()
    {
        var font = Resources.Load<TMP_FontAsset>("JPFont");
        if (font == null) Debug.LogWarning("SupportUIBuilder: JPFont が Resources に見つかりません。日本語が表示されません");
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

    private static Button NewButton(string name, Transform parent, TMP_FontAsset font, string label, Color color, out TextMeshProUGUI labelText)
    {
        var go = NewUI(name, parent);
        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 48f;
        le.minHeight = 48f;

        labelText = NewText("Label", go.transform, font, label, 20, Ink, TextAlignmentOptions.Center);
        Stretch(labelText.gameObject);
        return btn;
    }

    /// <summary>見出し＋効果チップの行（グリッドで折り返す）。</summary>
    private static GameObject NewSection(string name, Transform parent, TMP_FontAsset font, string header, Color headerColor, out RectTransform row)
    {
        var section = NewUI(name, parent);
        SetupVertical(section.AddComponent<VerticalLayoutGroup>(), 4f, new RectOffset(0, 0, 0, 0));
        NewText(name + "_header", section.transform, font, header, 16, headerColor, TextAlignmentOptions.Left).fontStyle = FontStyles.Bold;

        var rowGo = NewUI(name + "_row", section.transform);
        var grid = rowGo.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(170f, 32f);
        grid.spacing = new Vector2(6f, 6f);
        grid.constraint = GridLayoutGroup.Constraint.Flexible;
        row = (RectTransform)rowGo.transform;
        return section;
    }
}
#endif
