#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Yamaguchi.UI;

namespace Yamaguchi.Editor
{
    public static class HUDVisibilitySetup
    {
        [MenuItem("Tools/Yamaguchi/Setup HUD Visibility")]
        public static void Setup()
        {
            var canvasGO = GameObject.Find("Canvas");
            if (canvasGO == null)
            {
                Debug.LogError("[HUDVisibilitySetup] Canvas が見つかりませんでした。");
                return;
            }

            // Canvas に HUDVisibilityController を追加または取得
            var controller = canvasGO.GetComponent<HUDVisibilityController>();
            if (controller == null)
            {
                controller = canvasGO.AddComponent<HUDVisibilityController>();
                Undo.RegisterCreatedObjectUndo(controller, "Add HUDVisibilityController");
            }

            var supportsT = canvasGO.transform.Find("supports");
            var ledgerT = canvasGO.transform.Find("LedgerPanel");
            var detailT = canvasGO.transform.Find("detail");
            var buttonsT = canvasGO.transform.Find("Buttons");
            var speedT = canvasGO.transform.Find("GameSpeedUI");
            Transform kakeiboT = null;
            if (buttonsT != null)
            {
                kakeiboT = buttonsT.Find("Kakeibo");
            }

            var so = new SerializedObject(controller);
            if (supportsT != null) so.FindProperty("supportsPanel").objectReferenceValue = supportsT.gameObject;
            if (ledgerT != null) so.FindProperty("ledgerPanel").objectReferenceValue = ledgerT.gameObject;
            if (detailT != null) so.FindProperty("detailSlidePanel").objectReferenceValue = detailT.GetComponent<SlidePanel>();
            if (buttonsT != null) so.FindProperty("buttonsRoot").objectReferenceValue = buttonsT.gameObject;
            if (kakeiboT != null) so.FindProperty("kakeiboButton").objectReferenceValue = kakeiboT.gameObject;
            if (speedT != null) so.FindProperty("gameSpeedUIRoot").objectReferenceValue = speedT.gameObject;

            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(canvasGO.scene);
            EditorSceneManager.SaveScene(canvasGO.scene);

            Debug.Log("[HUDVisibilitySetup] Canvas に HUDVisibilityController の設定・配線・シーン保存が完了しました。");
        }
    }
}
#endif
