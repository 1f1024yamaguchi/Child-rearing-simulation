using UnityEditor;
using UnityEngine;

/// <summary>
/// フラグのアセットを作る・消す。置き場所は Assets/Yamaguchi/Data/Story/Flags/Flag_&lt;ID&gt;.asset。
/// ID はセーブのキーなので、ゲームに出したあとは変えない（表示名は変えてよい）。
/// </summary>
public static class StoryFlagFactory
{
    public const string Folder = "Assets/Yamaguchi/Data/Story/Flags";

    /// <summary>ID として使えるか。使えなければ理由を返す（使えれば null）。</summary>
    public static string ValidateNewId(string id)
    {
        if (string.IsNullOrEmpty(id)) return "ID を入れてください";
        if (!EventRegistry.IsValidIdChars(id)) return "ID は英数字と _ だけにしてください";
        if (StoryIndex.CountId(id) > 0) return $"ID「{id}」はもう使われています";
        if (AssetDatabase.LoadAssetAtPath<StoryFlag>(PathOf(id)) != null) return $"{PathOf(id)} がもうあります";
        return null;
    }

    public static string PathOf(string id) => $"{Folder}/Flag_{id}.asset";

    public static StoryFlag Create(string id, string displayName)
    {
        string err = ValidateNewId(id);
        if (err != null) { Debug.LogError("[ストーリー] " + err); return null; }

        EnsureFolder();
        var f = ScriptableObject.CreateInstance<StoryFlag>();
        f.id = id;
        f.displayName = string.IsNullOrEmpty(displayName) ? id : displayName;
        AssetDatabase.CreateAsset(f, PathOf(id));
        AssetDatabase.SaveAssets();
        StoryIndex.Invalidate();
        return f;
    }

    /// <summary>どこからも使われていないときだけ消す。消せたら true。</summary>
    public static bool DeleteIfUnused(StoryFlag flag)
    {
        if (flag == null) return false;
        if (!StoryIndex.UsageOf(flag).IsUnused)
        {
            Debug.LogWarning($"[ストーリー] フラグ「{flag.displayName}」は使われているので消せません。");
            return false;
        }
        string path = AssetDatabase.GetAssetPath(flag);
        bool ok = AssetDatabase.DeleteAsset(path);
        StoryIndex.Invalidate();
        return ok;
    }

    private static void EnsureFolder()
    {
        if (AssetDatabase.IsValidFolder(Folder)) return;
        string[] parts = Folder.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
