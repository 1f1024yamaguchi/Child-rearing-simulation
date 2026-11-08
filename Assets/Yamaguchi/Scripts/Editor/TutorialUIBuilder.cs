using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// メニュー「子育て → チュートリアル → 画面をシーンに作る」。
/// チュートリアル画面（TutorialCanvas）を、いま開いているシーンに実体として作り、プレハブにも保存する。
/// 作ったあとの位置・大きさの調整は、Canvas 上のオブジェクトを直接動かして行う（作り直すと調整は元に戻る）。
/// 家出のチュートリアル（Tutorial_Runaway）が無ければ一緒に作る。
/// </summary>
public static class TutorialUIBuilder
{
    private const string PrefabDir = "Assets/Yamaguchi/Prefabs/UI";
    private const string PrefabPath = PrefabDir + "/TutorialCanvas.prefab";
    public const string DataDir = "Assets/Yamaguchi/Data/Tutorial";
    private const string RunawayDataPath = DataDir + "/Tutorial_Runaway.asset";

    private static readonly Color Cream = new Color32(255, 250, 235, 255);
    private static readonly Color Ink = new Color32(40, 40, 40, 255);
    private static readonly Color Edge = new Color32(70, 60, 50, 255);
    private static readonly Color PlaceholderGray = new Color32(224, 224, 224, 255);
    private static readonly Color Green = new Color32(92, 158, 76, 255);
    private static readonly Color DotGray = new Color32(200, 200, 200, 255);

    private static TMP_FontAsset font;

    [MenuItem("子育て/チュートリアル/画面をシーンに作る", false, 1)]
    public static void BuildMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("チュートリアル", "再生を止めてから実行してください。", "OK");
            return;
        }

        var existing = Object.FindFirstObjectByType<TutorialManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            bool rebuild = EditorUtility.DisplayDialog("チュートリアル",
                $"シーンにもうチュートリアル画面があります（{existing.name}）。\n" +
                "作り直すと、Canvas で調整した位置や大きさは元に戻ります。作り直しますか？",
                "作り直す", "やめる");
            if (!rebuild)
            {
                Selection.activeObject = existing.gameObject;
                return;
            }
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        var root = Build();
        Selection.activeObject = root;
        Debug.Log($"[TutorialUIBuilder] チュートリアル画面を作りました（プレハブ：{PrefabPath}）。シーンを保存してください。");
    }

    /// 画面一式を作る。作ったルート（TutorialCanvas）を返す。
    public static GameObject Build()
    {
        font = Resources.Load<TMP_FontAsset>("JPFont");
        if (font == null) Debug.LogWarning("[TutorialUIBuilder] JPFont が見つかりません。TMP の既定フォントで作ります");

        // ── Canvas（常に表示。TutorialManager はここ）
        var root = new GameObject("TutorialCanvas", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;   // 他の画面より手前
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800, 600);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();
        var manager = root.AddComponent<TutorialManager>();

        // ── パネル（ふだんは非表示）
        var panelRt = Stretch(Make("TutorialPanel", root.transform));
        var panel = panelRt.gameObject.AddComponent<TutorialPanelUI>();

        var dim = Stretch(Make("Dim", panelRt));
        AddImage(dim, new Color(0f, 0f, 0f, 0.6f), true);   // 後ろの画面を押せないようにする

        // 台紙 740x580
        var sheet = Make("Sheet", panelRt);
        Place(sheet, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(740, 580));
        AddImage(sheet, Cream, true);
        var outline = sheet.gameObject.AddComponent<Outline>();
        outline.effectColor = Edge;
        outline.effectDistance = new Vector2(4, -4);

        // ── 画像の枠（黒枠。ここをスワイプする）上から20、560x290
        var frame = Make("ImageFrame", sheet);
        Place(frame, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(560, 290));
        AddImage(frame, Color.black, true);
        var swipe = frame.gameObject.AddComponent<TutorialSwipeArea>();

        var viewport = Make("Viewport", frame);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(6, 6);
        viewport.offsetMax = new Vector2(-6, -6);
        AddImage(viewport, PlaceholderGray, false);
        viewport.gameObject.AddComponent<RectMask2D>();

        var track = Stretch(Make("Track", viewport));
        float pageW = 560 - 12;
        var prevSlot = MakeSlot("Slot_Prev", track, -pageW);
        var curSlot = MakeSlot("Slot_Current", track, 0);
        var nextSlot = MakeSlot("Slot_Next", track, pageW);

        // ── 左右の矢印（枠の中心の高さ）
        float arrowY = -20 - 290 / 2f;
        var prevBtn = MakeButton("Arrow_Left", sheet, "＜", 34, new Vector2(-318, arrowY), new Vector2(48, 68));
        var nextBtn = MakeButton("Arrow_Right", sheet, "＞", 34, new Vector2(318, arrowY), new Vector2(48, 68));

        // ── ページの点（Image を横に並べる）
        var dotsRt = Make("PageDots", sheet);
        Place(dotsRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -326), new Vector2(300, 16));
        var layout = dotsRt.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 10;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        var dotTemplate = Make("DotTemplate", dotsRt);
        dotTemplate.sizeDelta = new Vector2(12, 12);
        var dotImg = AddImage(dotTemplate, DotGray, false);
        dotTemplate.gameObject.SetActive(false);
        var dots = dotsRt.gameObject.AddComponent<TutorialPageDots>();

        // ── 見出しと説明文
        var textBlock = Make("TextBlock", sheet);
        Place(textBlock, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -340), new Vector2(660, 160));
        var textGroup = textBlock.gameObject.AddComponent<CanvasGroup>();

        var heading = Make("Heading", textBlock);
        heading.anchorMin = new Vector2(0, 1);
        heading.anchorMax = new Vector2(1, 1);
        heading.pivot = new Vector2(0.5f, 1f);
        heading.anchoredPosition = Vector2.zero;
        heading.sizeDelta = new Vector2(0, 54);
        var headingText = AddText(heading, "見出し", 44, Ink, TextAlignmentOptions.Center);
        headingText.fontStyle = FontStyles.Bold;

        var body = Make("Body", textBlock);
        body.anchorMin = Vector2.zero;
        body.anchorMax = Vector2.one;
        body.offsetMin = Vector2.zero;
        body.offsetMax = new Vector2(0, -58);
        var bodyText = AddText(body, "説明文", 17, Ink, TextAlignmentOptions.TopLeft);
        bodyText.lineSpacing = 8;

        // ── 閉じるボタン（最後のページだけ出る）
        var okBtn = MakeButton("Btn_OK", sheet, "わかった", 22, Vector2.zero, new Vector2(180, 44));
        var okRt = (RectTransform)okBtn.transform;
        okRt.anchorMin = okRt.anchorMax = new Vector2(0.5f, 0f);
        okRt.pivot = new Vector2(0.5f, 0f);
        okRt.anchoredPosition = new Vector2(0, 16);

        // ── 参照をつなぐ
        SetRefs(panel,
            ("swipeArea", swipe), ("viewport", viewport), ("track", track),
            ("prevSlot", prevSlot), ("currentSlot", curSlot), ("nextSlot", nextSlot),
            ("headingText", headingText), ("bodyText", bodyText), ("textGroup", textGroup),
            ("prevButton", prevBtn), ("nextButton", nextBtn), ("okButton", okBtn), ("pageDots", dots));
        SetRefs(dots, ("dotTemplate", dotImg));
        SetRefs(manager, ("panel", panel));

        // ── 出すタイミング（全チュートリアル共通のスケジューラ。家出を登録しておく）
        var triggers = Make("Triggers", root.transform);
        var scheduler = triggers.gameObject.AddComponent<TutorialScheduler>();
        var sso = new SerializedObject(scheduler);
        var list = sso.FindProperty("tutorials");
        list.arraySize = 1;
        list.GetArrayElementAtIndex(0).objectReferenceValue = EnsureRunawayData();
        sso.ApplyModifiedPropertiesWithoutUndo();   // RunawayWatcher はシーンの物なので実行時に自動で探す

        panelRt.gameObject.SetActive(false);

        Undo.RegisterCreatedObjectUndo(root, "チュートリアル画面を作る");
        SavePrefab(root);
        EditorSceneManager.MarkSceneDirty(root.scene);
        return root;
    }

    // ─── 家出のチュートリアル ─────────────────────────────

    /// 家出のチュートリアルのアセットを返す。無ければ作る。
    public static TutorialData EnsureRunawayData()
    {
        var data = AssetDatabase.LoadAssetAtPath<TutorialData>(RunawayDataPath);
        if (data != null) return data;

        EnsureFolder(DataDir);
        data = ScriptableObject.CreateInstance<TutorialData>();
        AssetDatabase.CreateAsset(data, RunawayDataPath);

        var so = new SerializedObject(data);
        so.FindProperty("id").stringValue = "runaway";
        so.FindProperty("timing").FindPropertyRelative("kind").intValue = (int)TutorialTimingKind.RunawayStart;
        var pages = so.FindProperty("pages");
        pages.arraySize = 1;
        var p = pages.GetArrayElementAtIndex(0);
        p.FindPropertyRelative("image").objectReferenceValue = null;
        p.FindPropertyRelative("imageNote").stringValue =
            "こどもが背を向けて家から出ているドット絵。\n夕日に向かって歩いている。\nこの画像は実際の家でイベントで出てくる";
        p.FindPropertyRelative("heading").stringValue = "家出";
        p.FindPropertyRelative("body").stringValue =
            "子どものストレスがずっと高い状態だと家出をしてしまいます！\n" +
            "家出の時点でゲームは終了ですので、こまめにチェックしてあげてください！\n" +
            "（このイベントは中学生になってから起こります）";
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        return data;
    }

    // ─── 部品 ─────────────────────────────────────────────

    // 画像1枚ぶん：Image（ドット絵）＋差し替え枠（灰色）＋絵の指示メモ
    private static TutorialImageSlot MakeSlot(string name, RectTransform parent, float x)
    {
        var rt = Stretch(Make(name, parent));
        rt.anchoredPosition = new Vector2(x, 0);
        var img = AddImage(rt, Color.white, false);
        img.preserveAspect = true;
        img.enabled = false;

        var ph = Stretch(Make("Placeholder", rt));
        AddImage(ph, PlaceholderGray, false);
        var note = Make("Note", ph);
        note.anchorMin = Vector2.zero;
        note.anchorMax = Vector2.one;
        note.offsetMin = new Vector2(24, 16);
        note.offsetMax = new Vector2(-24, -16);
        var noteText = AddText(note, "", 16, Ink, TextAlignmentOptions.Center);

        var slot = rt.gameObject.AddComponent<TutorialImageSlot>();
        SetRefs(slot, ("image", img), ("placeholder", ph.gameObject), ("placeholderNote", noteText));
        return slot;
    }

    private static Button MakeButton(string name, RectTransform parent, string label, float size, Vector2 pos, Vector2 boxSize)
    {
        var rt = Make(name, parent);
        Place(rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), pos, boxSize);
        var img = AddImage(rt, Green, true);
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var labelRt = Stretch(Make("Label", rt));
        var t = AddText(labelRt, label, size, Color.white, TextAlignmentOptions.Center);
        t.fontStyle = FontStyles.Bold;
        return btn;
    }

    private static RectTransform Make(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static RectTransform Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static Image AddImage(RectTransform rt, Color color, bool raycast)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    private static TextMeshProUGUI AddText(RectTransform rt, string text, float size, Color color, TextAlignmentOptions align)
    {
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = true;
        t.overflowMode = TextOverflowModes.Overflow;   // Ellipsis は文字が消えるので使わない
        t.raycastTarget = false;
        return t;
    }

    private static void SetRefs(Object target, params (string field, Object value)[] refs)
    {
        var so = new SerializedObject(target);
        foreach (var (field, value) in refs)
        {
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[TutorialUIBuilder] {target.GetType().Name}.{field} が見つかりません"); continue; }
            p.objectReferenceValue = value;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SavePrefab(GameObject root)
    {
        EnsureFolder(PrefabDir);
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
