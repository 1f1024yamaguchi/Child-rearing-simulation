using UnityEngine;

/// <summary>
/// 確認用。GameManager に付けて、Inspector のコンポーネント右クリックから呼ぶ。
/// タイトル画面ができたら「続きから」で SaveSystem.Load() を呼ぶようにして、このメニューは不要になる。
/// </summary>
public class SaveDebugMenu : MonoBehaviour
{
    [ContextMenu("Debug/セーブ")]
    private void DebugSave() => SaveSystem.Save();

    [ContextMenu("Debug/ロード")]
    private void DebugLoad()
    {
        if (!SaveSystem.Load()) Debug.LogWarning("[SaveDebugMenu] ロードできませんでした（セーブが無いか、読めません）。");
    }

    [ContextMenu("Debug/セーブを消す")]
    private void DebugDelete()
    {
        SaveSystem.Delete();
        Debug.Log("[SaveDebugMenu] セーブを消しました。");
    }

    [ContextMenu("Debug/セーブファイルの場所をログに出す")]
    private void DebugLogPath()
    {
        Debug.Log($"[SaveDebugMenu] {SaveSystem.FilePath}（存在: {SaveSystem.HasSave()}）");
    }
}
