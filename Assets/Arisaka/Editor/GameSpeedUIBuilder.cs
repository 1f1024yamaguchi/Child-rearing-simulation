#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

namespace Arisaka.Editor
{
    public static class GameSpeedUIBuilder
    {
        private const string PrefabDir = "Assets/Arisaka/Prefabs";
        private const string PrefabPath = "Assets/Arisaka/Prefabs/GameSpeedUI.prefab";

        [MenuItem("Tools/Arisaka/Setup GameSpeedUI")]
        public static void Setup()
        {
            // 1. プレハブディレクトリの確保
            if (!AssetDatabase.IsValidFolder(PrefabDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Arisaka"))
                {
                    AssetDatabase.CreateFolder("Assets", "Arisaka");
                }
                AssetDatabase.CreateFolder("Assets/Arisaka", "Prefabs");
            }

            // 2. 日本語フォント取得
            TMP_FontAsset jpFont = Resources.Load<TMP_FontAsset>("JPFont");
            if (jpFont == null)
            {
                jpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Yamaguchi/Resources/JPFont.asset");
            }

            // 3. UI GameObjectの構築（一時オブジェクト）
            var rootGO = new GameObject("GameSpeedUI", typeof(RectTransform), typeof(GameSpeedUI));
            var rootRT = rootGO.GetComponent<RectTransform>();
            rootRT.sizeDelta = new Vector2(196f, 38f);
            rootRT.anchorMin = new Vector2(0.5f, 0.5f);
            rootRT.anchorMax = new Vector2(0.5f, 0.5f);
            rootRT.pivot = new Vector2(0.5f, 0.5f);

            var controller = rootGO.GetComponent<GameSpeedUI>();

            // ボタン生成ヘルパー
            string[] labels = { "通常", "2x", "3x" };
            string[] names = { "Btn_Normal", "Btn_2x", "Btn_3x" };
            float[] xOffsets = { -66f, 0f, 66f };

            Button[] buttons = new Button[3];
            TextMeshProUGUI[] texts = new TextMeshProUGUI[3];

            Color inactiveBg = new Color(0.93f, 0.93f, 0.93f, 1f);
            Color darkText = new Color(0.13f, 0.13f, 0.13f, 1f);

            for (int i = 0; i < 3; i++)
            {
                var btnGO = new GameObject(names[i], typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                btnGO.transform.SetParent(rootGO.transform, false);

                var btnRT = btnGO.GetComponent<RectTransform>();
                btnRT.sizeDelta = new Vector2(60f, 36f);
                btnRT.anchoredPosition = new Vector2(xOffsets[i], 0f);

                var btnImg = btnGO.GetComponent<Image>();
                btnImg.color = inactiveBg;

                var btn = btnGO.GetComponent<Button>();
                btn.targetGraphic = btnImg;

                var txtGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                txtGO.transform.SetParent(btnGO.transform, false);

                var txtRT = txtGO.GetComponent<RectTransform>();
                txtRT.anchorMin = Vector2.zero;
                txtRT.anchorMax = Vector2.one;
                txtRT.offsetMin = Vector2.zero;
                txtRT.offsetMax = Vector2.zero;

                var txt = txtGO.GetComponent<TextMeshProUGUI>();
                if (jpFont != null) txt.font = jpFont;
                txt.text = labels[i];
                txt.fontSize = 18f;
                txt.alignment = TextAlignmentOptions.Center;
                txt.color = darkText;
                txt.enableWordWrapping = false;
                txt.overflowMode = TextOverflowModes.Overflow;

                buttons[i] = btn;
                texts[i] = txt;
            }

            // SerializedObject で参照を設定
            var so = new SerializedObject(controller);
            so.FindProperty("btnNormal").objectReferenceValue = buttons[0];
            so.FindProperty("btn2x").objectReferenceValue = buttons[1];
            so.FindProperty("btn3x").objectReferenceValue = buttons[2];

            so.FindProperty("txtNormal").objectReferenceValue = texts[0];
            so.FindProperty("txt2x").objectReferenceValue = texts[1];
            so.FindProperty("txt3x").objectReferenceValue = texts[2];
            so.ApplyModifiedPropertiesWithoutUndo();

            // 4. プレハブ保存
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(rootGO, PrefabPath, InteractionMode.AutomatedAction);
            Debug.Log($"[GameSpeedUIBuilder] プレハブを作成しました: {PrefabPath}");

            // 5. 現在開いているシーンの Canvas に配置
            var canvasGO = GameObject.Find("Canvas");
            if (canvasGO != null)
            {
                // 既存の GameSpeedUI を探して置換
                var existing = canvasGO.transform.Find("GameSpeedUI");
                if (existing != null)
                {
                    Object.DestroyImmediate(existing.gameObject);
                }

                // rootGO を Canvas の子に移動して配置
                rootGO.transform.SetParent(canvasGO.transform, false);

                // 年月表示 (Canvas/Day) の右横に配置
                // Day の位置 (x: 14, y: 22) + day_text の位置 (x: -57, y: 180, width: 200)
                // day_text の中心は (-43, 202)。右端は x = 57。
                // GameSpeedUI (width: 196) の左端を x = 67 付近に置くため、中心は x = 165, y = 202。
                rootRT.anchoredPosition = new Vector2(165f, 202f);

                EditorSceneManager.MarkSceneDirty(canvasGO.scene);
                EditorSceneManager.SaveScene(canvasGO.scene);
                Debug.Log($"[GameSpeedUIBuilder] シーン '{canvasGO.scene.name}' の Canvas に GameSpeedUI を配置・保存しました (座標: {rootRT.anchoredPosition})");
            }
            else
            {
                Object.DestroyImmediate(rootGO);
                Debug.LogWarning("[GameSpeedUIBuilder] シーン内に Canvas が見つかりませんでした。");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
#endif

