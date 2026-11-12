#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Kusao.TutorialGuide.Editor
{
    /// <summary>
    /// メニュー「子育て → チュートリアル → スタートガイドを作成・設定」。
    /// スポットライト型チュートリアル（TutorialGuide_Canvas）をシーンに自動生成・設定します。
    /// </summary>
    public static class TutorialGuideSetup
    {
        private const string PrefabDir = "Assets/Kusao/TutorialGuide";
        private const string PrefabPath = PrefabDir + "/TutorialGuide_Canvas.prefab";

        private static readonly Color CreamBg = new Color32(255, 252, 245, 250);
        private static readonly Color DarkText = new Color32(35, 35, 35, 255);
        private static readonly Color MutedText = new Color32(110, 110, 110, 255);
        private static readonly Color AccentCyan = new Color(0.28f, 0.72f, 0.85f, 1f);
        private static readonly Color GrayBtn = new Color(0.88f, 0.88f, 0.88f, 1f);

        private static TMP_FontAsset font;

        [MenuItem("子育て/チュートリアル/スタートガイドを作成・設定", false, 10)]
        public static void Setup()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("スタートガイド", "再生を止めてから実行してください。", "OK");
                return;
            }

            var existing = Object.FindFirstObjectByType<TutorialGuideController>(FindObjectsInactive.Include);
            if (existing != null)
            {
                bool rebuild = EditorUtility.DisplayDialog(
                    "スタートガイド",
                    $"シーン上に既にスタートガイドが存在します（{existing.name}）。\n作り直しますか？（位置調整等は初期化されます）",
                    "作り直す", "やめる");

                if (!rebuild)
                {
                    Selection.activeObject = existing.gameObject;
                    return;
                }
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            var guideRoot = Build();
            Selection.activeObject = guideRoot;

            EditorSceneManager.MarkSceneDirty(guideRoot.scene);
            EditorSceneManager.SaveScene(guideRoot.scene);
            Debug.Log($"[TutorialGuideSetup] スタートガイド（TutorialGuide_Canvas）を生成・設定しました。シーンを保存しました。");
        }

        public static void BatchSetup()
        {
            var scenePath = "Assets/Yamaguchi/scenes/Main_test2.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            
            var existing = Object.FindFirstObjectByType<TutorialGuideController>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var guideRoot = Build();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[TutorialGuideSetup] バッチモードでのスタートガイド配置とプレハブ生成が完了しました。");
        }

        public static GameObject Build()
        {
            font = Resources.Load<TMP_FontAsset>("JPFont");
            if (font == null) font = TMP_Settings.defaultFontAsset;

            // 1. Root Canvas
            var root = new GameObject("TutorialGuide_Canvas", typeof(RectTransform));
            var rootRect = root.GetComponent<RectTransform>();
            StretchFull(rootRect);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            root.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(root, "Create TutorialGuide_Canvas");

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300; // 最前面

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 1024);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();
            var controller = root.AddComponent<TutorialGuideController>();

            // 2. Spotlight
            var spotGO = new GameObject("Spotlight", typeof(RectTransform));
            spotGO.transform.SetParent(root.transform, false);
            StretchFull(spotGO.GetComponent<RectTransform>());
            var spotlight = spotGO.AddComponent<TutorialGuideSpotlight>();
            spotlight.EnsurePanels();

            // 3. Popup
            var popupGO = new GameObject("Popup", typeof(RectTransform));
            popupGO.transform.SetParent(root.transform, false);
            StretchFull(popupGO.GetComponent<RectTransform>());
            var popup = popupGO.AddComponent<TutorialGuidePopup>();

            // Popup Panel
            var panelGO = MakeImage("PopupPanel", popupGO.transform, CreamBg);
            var panelRt = panelGO.GetComponent<RectTransform>();
            panelRt.sizeDelta = new Vector2(580f, 260f);

            // Title
            var titleRt = MakeText("Title", panelRt, "機能の見出し", 26, FontStyles.Bold, DarkText, TextAlignmentOptions.Left);
            SetRect(titleRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(28f, -20f), new Vector2(-150f, 40f));

            // Progress ("1 / 11")
            var progRt = MakeText("Progress", panelRt, "1 / 11", 18, FontStyles.Normal, MutedText, TextAlignmentOptions.Right);
            SetRect(progRt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -22f), new Vector2(100f, 36f));

            // Body
            var bodyRt = MakeText("Body", panelRt, "ここに説明文が表示されます。", 20, FontStyles.Normal, DarkText, TextAlignmentOptions.TopLeft);
            bodyRt.GetComponent<TextMeshProUGUI>().enableWordWrapping = true;
            SetRect(bodyRt, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 5f), new Vector2(-56f, -125f));

            // HintText
            var hintRt = MakeText("Hint", panelRt, "↑ ボタンを押してみましょう！", 18, FontStyles.Bold, AccentCyan, TextAlignmentOptions.Center);
            SetRect(hintRt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 68f), new Vector2(-56f, 32f));

            // Buttons Area
            var btnArea = new GameObject("ButtonsArea", typeof(RectTransform));
            btnArea.transform.SetParent(panelRt, false);
            var btnAreaRt = btnArea.GetComponent<RectTransform>();
            SetRect(btnAreaRt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(-56f, 46f));

            // Next Button
            var nextBtnGO = MakeButton("NextButton", btnAreaRt, "次へ ＞", AccentCyan, Color.white, 20);
            var nextBtnRt = nextBtnGO.GetComponent<RectTransform>();
            SetRect(nextBtnRt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(180f, 44f));

            // Skip Button
            var skipBtnGO = MakeButton("SkipButton", btnAreaRt, "スキップ", GrayBtn, DarkText, 16);
            var skipBtnRt = skipBtnGO.GetComponent<RectTransform>();
            SetRect(skipBtnRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(110f, 40f));

            // Arrows (Pointers)
            var arrowTopGO = MakeImage("ArrowTop", panelRt, CreamBg);
            var arrowTopRt = arrowTopGO.GetComponent<RectTransform>();
            arrowTopRt.sizeDelta = new Vector2(24f, 14f);
            arrowTopRt.anchorMin = new Vector2(0.5f, 0f);
            arrowTopRt.anchorMax = new Vector2(0.5f, 0f);
            arrowTopRt.pivot = new Vector2(0.5f, 1f);
            arrowTopRt.anchoredPosition = new Vector2(0f, 0f);

            var arrowBottomGO = MakeImage("ArrowBottom", panelRt, CreamBg);
            var arrowBottomRt = arrowBottomGO.GetComponent<RectTransform>();
            arrowBottomRt.sizeDelta = new Vector2(24f, 14f);
            arrowBottomRt.anchorMin = new Vector2(0.5f, 1f);
            arrowBottomRt.anchorMax = new Vector2(0.5f, 1f);
            arrowBottomRt.pivot = new Vector2(0.5f, 0f);
            arrowBottomRt.anchoredPosition = new Vector2(0f, 0f);

            // 4. 配線 (SerializedObject)
            var soPopup = new SerializedObject(popup);
            soPopup.FindProperty("popupPanel").objectReferenceValue = panelRt;
            soPopup.FindProperty("titleText").objectReferenceValue = titleRt.GetComponent<TextMeshProUGUI>();
            soPopup.FindProperty("bodyText").objectReferenceValue = bodyRt.GetComponent<TextMeshProUGUI>();
            soPopup.FindProperty("progressText").objectReferenceValue = progRt.GetComponent<TextMeshProUGUI>();
            soPopup.FindProperty("hintText").objectReferenceValue = hintRt.GetComponent<TextMeshProUGUI>();
            soPopup.FindProperty("nextButton").objectReferenceValue = nextBtnGO.GetComponent<Button>();
            soPopup.FindProperty("nextButtonLabel").objectReferenceValue = nextBtnGO.GetComponentInChildren<TextMeshProUGUI>();
            soPopup.FindProperty("skipButton").objectReferenceValue = skipBtnGO.GetComponent<Button>();
            soPopup.FindProperty("arrowTop").objectReferenceValue = arrowTopRt;
            soPopup.FindProperty("arrowBottom").objectReferenceValue = arrowBottomRt;
            soPopup.ApplyModifiedPropertiesWithoutUndo();

            var soController = new SerializedObject(controller);
            soController.FindProperty("spotlight").objectReferenceValue = spotlight;
            soController.FindProperty("popup").objectReferenceValue = popup;
            soController.ApplyModifiedPropertiesWithoutUndo();

            var soSpot = new SerializedObject(spotlight);
            soSpot.FindProperty("pulseBorder").boolValue = false;
            soSpot.FindProperty("dimColor").colorValue = new Color(0f, 0f, 0f, 0.62f);
            soSpot.FindProperty("borderColor").colorValue = new Color(0.40f, 0.78f, 0.90f, 0.75f);
            soSpot.FindProperty("borderWidth").floatValue = 2f;
            soSpot.ApplyModifiedPropertiesWithoutUndo();

            controller.AutoResolveReferences();

            // 5. プレハブの保存
            if (!Directory.Exists(PrefabDir)) Directory.CreateDirectory(PrefabDir);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);

            return root;
        }

        // ---------------- ヘルパー ----------------

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static GameObject MakeImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            return go;
        }

        private static RectTransform MakeText(string name, Transform parent, string text, float size, FontStyles style, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.overflowMode = TextOverflowModes.Overflow;
            return go.GetComponent<RectTransform>();
        }

        private static GameObject MakeButton(string name, Transform parent, string label, Color bg, Color textCol, float fontSize)
        {
            var go = MakeImage(name, parent, bg);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();

            var textRt = MakeText("Label", go.transform, label, fontSize, FontStyles.Bold, textCol, TextAlignmentOptions.Center);
            StretchFull(textRt);
            return go;
        }
    }
}
#endif
