using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 開いているシーン内の全 Button（非アクティブを含む）に ButtonClickSE を一括で付与するエディタ拡張。
/// シーンの保存は人間が行う。
/// </summary>
public static class ButtonSeSetupBuilder
{
    [MenuItem("子育て/サウンド/全ボタンにSEを付ける")]
    public static void AttachButtonSeToAllButtonsInOpenScenes()
    {
        Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int addedCount = 0;

        foreach (Button button in buttons)
        {
            if (button == null) continue;
            if (!button.gameObject.scene.IsValid() || !button.gameObject.scene.isLoaded) continue;

            if (button.GetComponent<ButtonClickSE>() != null) continue;

            Undo.AddComponent<ButtonClickSE>(button.gameObject);
            EditorSceneManager.MarkSceneDirty(button.gameObject.scene);
            addedCount++;
        }

        Debug.Log($"[ButtonSeSetupBuilder] {addedCount} 個の Button に ButtonClickSE を付けました（シーン内 Button 総数: {buttons.Length}）。確認後、シーンを保存（Ctrl+S）してください。");
    }
}

