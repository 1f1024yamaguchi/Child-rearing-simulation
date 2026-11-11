using System.IO;
using System.Text;
using Maruyama.Core;
using UnityEditor;
using UnityEngine;

/// <summary>制度の新規作成（テンプレから）と複製。作ったら SupportManager に自動登録する。</summary>
public static class SupportSchemeFactory
{
    public enum Template { Empty, LumpSum, MonthlyAllowance, Free, MedicalAid, Leave }

    public static readonly string[] TemplateNames =
    {
        "空", "一時金", "毎月の手当", "無償化", "医療費の助成", "育休・時短",
    };

    /// <summary>保存先を聞いて、テンプレから新しい制度を作る。やめたら null。</summary>
    public static SupportScheme Create(Template template)
    {
        SupportEditorGUI.EnsureFolder(SupportEditorGUI.DataDir);
        string path = EditorUtility.SaveFilePanelInProject("支援制度を作る", "Support_New", "asset",
            "ファイル名を決めてください（例：Support_ChildAllowance）", SupportEditorGUI.DataDir);
        if (string.IsNullOrEmpty(path)) return null;
        if (AssetDatabase.LoadMainAssetAtPath(path) != null) path = AssetDatabase.GenerateUniqueAssetPath(path);   // 既存の制度は上書きしない

        var asset = ScriptableObject.CreateInstance<SupportScheme>();
        AssetDatabase.CreateAsset(asset, path);
        SupportRegistry.Invalidate();

        var so = new SerializedObject(asset);
        so.FindProperty("id").stringValue = MakeId(Path.GetFileNameWithoutExtension(path), asset);
        ApplyTemplate(so, template);
        so.ApplyModifiedPropertiesWithoutUndo();

        return Finish(asset);
    }

    /// <summary>制度をコピーして新しいファイルにする。id だけ新しいファイル名から付け直す。やめたら null。</summary>
    public static SupportScheme Duplicate(SupportScheme source)
    {
        if (source == null) return null;
        string srcPath = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(srcPath)) return null;

        string dir = Path.GetDirectoryName(srcPath).Replace('\\', '/');
        string path = EditorUtility.SaveFilePanelInProject("複製して作る", source.name + "_Copy", "asset",
            "新しいファイル名を決めてください", dir);
        if (string.IsNullOrEmpty(path)) return null;
        if (AssetDatabase.LoadMainAssetAtPath(path) != null) path = AssetDatabase.GenerateUniqueAssetPath(path);
        if (!AssetDatabase.CopyAsset(srcPath, path)) return null;

        var copy = AssetDatabase.LoadAssetAtPath<SupportScheme>(path);
        if (copy == null) return null;
        SupportRegistry.Invalidate();

        var so = new SerializedObject(copy);
        so.FindProperty("id").stringValue = MakeId(Path.GetFileNameWithoutExtension(path), copy);
        so.ApplyModifiedPropertiesWithoutUndo();

        return Finish(copy);
    }

    private static SupportScheme Finish(SupportScheme asset)
    {
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        SupportRegistry.Invalidate();
        SupportRegistry.SetRegistered(asset, true);   // SupportManager が無ければ何もしない
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
        return asset;
    }

    // ─── テンプレ ────────────────────────────────────────────────
    private static void ApplyTemplate(SerializedObject so, Template template)
    {
        switch (template)
        {
            case Template.LumpSum:
                so.FindProperty("effectTiming").intValue = (int)EffectTiming.OnApply;
                SupportPaymentSection.AppendEffect(so.FindProperty("effects"), EffectKind.Money, EffectTarget.Household, 0f);
                break;

            case Template.MonthlyAllowance:
                so.FindProperty("effectTiming").intValue = (int)EffectTiming.Monthly;
                var tier = SupportPaymentSection.AppendTier(so.FindProperty("monthlyTiers"));
                SupportEditorGUI.SetDay(tier.FindPropertyRelative("toAge"), 2, 12, 31);
                SupportPaymentSection.AppendEffect(tier.FindPropertyRelative("monthlyEffects"), EffectKind.Money, EffectTarget.Household, 0f);
                break;

            case Template.Free:
                so.FindProperty("effectTiming").intValue = (int)EffectTiming.Monthly;
                SupportCoverSection.AppendCover(so.FindProperty("covers"), true);
                break;

            case Template.MedicalAid:
                so.FindProperty("effectTiming").intValue = (int)EffectTiming.Monthly;
                SupportCoverSection.AppendCover(so.FindProperty("medicalExpenseCovers"), false);
                break;

            case Template.Leave:
                so.FindProperty("effectTiming").intValue = (int)EffectTiming.Monthly;
                so.FindProperty("changesWorkMode").boolValue = true;
                SupportWorkSection.AppendWageTier(so.FindProperty("wageLinkedTiers"));
                break;
        }
    }

    // ─── id の自動生成 ───────────────────────────────────────────
    /// <summary>ファイル名から id を作る。Support_ChildAllowance → child_allowance。重複したら _2 を付ける。</summary>
    public static string MakeId(string fileName, SupportScheme self)
    {
        string n = fileName.StartsWith("Support_") ? fileName.Substring("Support_".Length) : fileName;

        var sb = new StringBuilder();
        for (int i = 0; i < n.Length; i++)
        {
            char c = n[i];
            bool upper = c >= 'A' && c <= 'Z';
            bool lower = c >= 'a' && c <= 'z';
            bool digit = c >= '0' && c <= '9';
            if (upper)
            {
                bool afterLowerOrDigit = i > 0 && IsLowerOrDigit(n[i - 1]);
                bool acronymEnd = i > 0 && n[i - 1] >= 'A' && n[i - 1] <= 'Z' &&
                                  i + 1 < n.Length && n[i + 1] >= 'a' && n[i + 1] <= 'z';
                if ((afterLowerOrDigit || acronymEnd) && sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
                sb.Append((char)(c + ('a' - 'A')));
            }
            else if (lower || digit || c == '_')
            {
                sb.Append(c);
            }
            else if (sb.Length > 0 && sb[sb.Length - 1] != '_')
            {
                sb.Append('_');
            }
        }

        string baseId = sb.ToString().Trim('_');
        if (baseId.Length == 0) baseId = "new_support";

        string id = baseId;
        int suffix = 2;
        while (IsUsed(id, self)) id = baseId + "_" + suffix++;
        return id;
    }

    private static bool IsLowerOrDigit(char c) => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');

    private static bool IsUsed(string id, SupportScheme self)
    {
        foreach (var s in SupportRegistry.AllSchemes())
        {
            if (s != null && s != self && s.id == id) return true;
        }
        return false;
    }
}
