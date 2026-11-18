using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 職業（CareerDefinition）を足す・確かめるメニュー。
/// 職業は Resources/Careers/ に置いたものだけが夢・巣立ちの判定に入るので、
/// 「新しく作る」は必ずそこに作る（Project ウィンドウの Create メニューは選んでいるフォルダに作ってしまう）。
/// </summary>
public static class CareerMenu
{
    public const string Folder = "Assets/Yamaguchi/Resources/Careers";

    [MenuItem("子育て/職業/新しい職業を作る", priority = 0)]
    public static void CreateNew()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            Debug.LogError($"[職業] フォルダ {Folder} がありません。");
            return;
        }
        string path = AssetDatabase.GenerateUniqueAssetPath($"{Folder}/Career_new.asset");
        var c = ScriptableObject.CreateInstance<CareerDefinition>();
        c.id = System.IO.Path.GetFileNameWithoutExtension(path).Replace("Career_", "");
        c.displayName = "新しい職業";
        AssetDatabase.CreateAsset(c, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = c;
        EditorGUIUtility.PingObject(c);
        Debug.Log($"[職業] {path} を作りました。ID・表示名・条件・重みを Inspector で入れてください（ID はゲームに出したあと変えない）。");
    }

    [MenuItem("子育て/職業/一覧とチェック", priority = 1)]
    public static void Check()
    {
        var sb = new StringBuilder();
        int errors = 0, warnings = 0;
        var inFolder = new List<CareerDefinition>();
        var ids = new Dictionary<string, string>();
        int fallbackCount = 0;

        foreach (var guid in AssetDatabase.FindAssets("t:CareerDefinition"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var c = AssetDatabase.LoadAssetAtPath<CareerDefinition>(path);
            if (c == null) continue;
            if (!path.StartsWith(Folder + "/"))
            {
                sb.AppendLine($"⚠ {path}：{Folder} の外にあるので判定に入りません。");
                warnings++;
                continue;
            }
            inFolder.Add(c);
            if (string.IsNullOrEmpty(c.id)) { sb.AppendLine($"✕ {c.name}：ID が空です。"); errors++; }
            else if (!System.Text.RegularExpressions.Regex.IsMatch(c.id, "^[A-Za-z0-9_]+$")) { sb.AppendLine($"✕ {c.name}：ID は英数字と _ だけにしてください（{c.id}）。"); errors++; }
            else if (ids.ContainsKey(c.id)) { sb.AppendLine($"✕ {c.name}：ID「{c.id}」が {ids[c.id]} と重なっています。"); errors++; }
            else ids[c.id] = c.name;
            if (string.IsNullOrEmpty(c.displayName)) { sb.AppendLine($"⚠ {c.name}：表示名が空です。"); warnings++; }
            if (c.isFallback) fallbackCount++;
            if (c.minHobbyLevel > 0 && c.hobby == HobbyKind.なし) { sb.AppendLine($"✕ {c.name}：趣味Lv を求めているのに趣味の種類が「なし」です。"); errors++; }
            if (!c.isFallback && c.minGrade == 0 && c.minHealth == 0 && c.minHobbyLevel == 0 && (c.requirements == null || c.requirements.Count == 0))
            { sb.AppendLine($"⚠ {c.name}：条件が1つもないので、誰でも候補に入ります（会社員になる子がいなくなります）。"); warnings++; }
        }
        if (fallbackCount != 1) { sb.AppendLine($"✕ 既定の職業（isFallback）が {fallbackCount} 個です。1つにしてください。"); errors++; }

        inFolder.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        var head = new StringBuilder($"[職業] {inFolder.Count} 件（✕{errors} ⚠{warnings}）\n");
        foreach (var c in inFolder)
        {
            string cond = c.isFallback ? "既定" :
                $"成績{c.minGrade}以上 体調{c.minHealth}以上" + (c.minHobbyLevel > 0 ? $" {c.hobby}Lv{c.minHobbyLevel}以上" : "") +
                (c.requirements != null && c.requirements.Count > 0 ? $" ほか{c.requirements.Count}" : "");
            head.AppendLine($"  {c.DisplayNameOrName}（{c.id}）：{cond}");
        }
        string msg = head.ToString() + sb.ToString();
        if (errors > 0) Debug.LogError(msg); else if (warnings > 0) Debug.LogWarning(msg); else Debug.Log(msg);
    }
}
