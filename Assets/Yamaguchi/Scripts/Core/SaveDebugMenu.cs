using UnityEngine;

/// <summary>
/// 確認用。GameManager に付けて、Inspector のコンポーネント右クリックから呼ぶ。
/// 本編からの「続きから」は、タイトル画面（TitleMenuUI）→ GameStartup が行う。
/// </summary>
public class SaveDebugMenu : MonoBehaviour
{
    [ContextMenu("Debug/オート枠に保存")]
    private void DebugSaveAuto() => SaveSystem.Save(SaveSlot.Auto);

    [ContextMenu("Debug/手動枠に保存")]
    private void DebugSaveManual() => SaveSystem.Save(SaveSlot.Manual);

    [ContextMenu("Debug/オート枠をロード")]
    private void DebugLoadAuto() => LoadSlot(SaveSlot.Auto);

    [ContextMenu("Debug/手動枠をロード")]
    private void DebugLoadManual() => LoadSlot(SaveSlot.Manual);

    private static void LoadSlot(SaveSlot slot)
    {
        if (!SaveSystem.Load(slot)) Debug.LogWarning($"[SaveDebugMenu] {slot} 枠をロードできませんでした（セーブが無いか、読めません）。");
    }

    [ContextMenu("Debug/両方のセーブを消す")]
    private void DebugDelete()
    {
        SaveSystem.Delete();
        Debug.Log("[SaveDebugMenu] 両方のセーブを消しました。");
    }

    [ContextMenu("Debug/セーブの場所をログに出す（2枠とも）")]
    private void DebugLogPath()
    {
        Debug.Log($"[SaveDebugMenu] オート: {SaveSystem.FilePathOf(SaveSlot.Auto)}（存在: {SaveSystem.HasSave(SaveSlot.Auto)}）\n" +
                  $"手動: {SaveSystem.FilePathOf(SaveSlot.Manual)}（存在: {SaveSystem.HasSave(SaveSlot.Manual)}）");
    }
}
