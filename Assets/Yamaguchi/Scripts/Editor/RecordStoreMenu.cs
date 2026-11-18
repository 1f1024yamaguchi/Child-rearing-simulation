using UnityEditor;
using UnityEngine;

/// <summary>育成記録（RecordStore）の確認用メニュー。</summary>
public static class RecordStoreMenu
{
    [MenuItem("子育て/セーブ/育成記録をログ")]
    private static void Log()
    {
        var list = RecordStore.LoadAll();
        var sb = new System.Text.StringBuilder($"[育成記録] {list.Count} 件（{RecordStore.FilePath}）\n");
        foreach (var r in list)
            sb.AppendLine($"  {r.finishedAt} {r.reason} {r.childName} {r.GetAgeText()} 職業「{r.careerName}」夢「{r.dreamName}」子育て{r.totalChildCost:N0}円 支援{r.totalSupport:N0}円");
        Debug.Log(sb.ToString());
    }

    [MenuItem("子育て/セーブ/育成記録を消す")]
    private static void Delete()
    {
        if (!EditorUtility.DisplayDialog("育成記録を消す", "育成記録をすべて消します。元に戻せません。", "消す", "やめる")) return;
        RecordStore.DeleteAll();
        Debug.Log("[育成記録] 消しました。");
    }
}
