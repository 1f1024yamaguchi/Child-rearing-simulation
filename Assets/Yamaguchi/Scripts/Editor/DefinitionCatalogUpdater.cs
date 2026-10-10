using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// プロジェクト内の TraitDefinition / ConditionDefinition を検索して
/// Assets/Yamaguchi/Resources/DefinitionCatalog.asset にまとめるエディタ拡張。
/// </summary>
public static class DefinitionCatalogUpdater
{
    private const string AssetPath = "Assets/Yamaguchi/Resources/DefinitionCatalog.asset";
    private const string ResourcesDir = "Assets/Yamaguchi/Resources";

    [MenuItem("子育て/セーブ/カタログを更新")]
    public static void UpdateCatalog()
    {
        if (!Directory.Exists(ResourcesDir))
        {
            Directory.CreateDirectory(ResourcesDir);
            AssetDatabase.Refresh();
        }

        var catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetPath);
        bool created = false;
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<DefinitionCatalog>();
            AssetDatabase.CreateAsset(catalog, AssetPath);
            created = true;
        }

        catalog.traits.Clear();
        string[] traitGuids = AssetDatabase.FindAssets("t:TraitDefinition");
        foreach (string guid in traitGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var trait = AssetDatabase.LoadAssetAtPath<TraitDefinition>(path);
            if (trait != null && !catalog.traits.Contains(trait))
            {
                catalog.traits.Add(trait);
            }
        }

        catalog.conditions.Clear();
        string[] conditionGuids = AssetDatabase.FindAssets("t:ConditionDefinition");
        foreach (string guid in conditionGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var condition = AssetDatabase.LoadAssetAtPath<ConditionDefinition>(path);
            if (condition != null && !catalog.conditions.Contains(condition))
            {
                catalog.conditions.Add(condition);
            }
        }

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[DefinitionCatalog] カタログを更新しました ({(created ? "新規作成" : "上書き")}): " +
                  $"特性 {catalog.traits.Count} 件, 状態 {catalog.conditions.Count} 件 -> {AssetPath}");
    }
}
