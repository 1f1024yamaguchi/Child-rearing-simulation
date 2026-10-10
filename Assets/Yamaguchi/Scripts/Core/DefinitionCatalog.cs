using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プロジェクト内の特性（TraitDefinition）と状態（ConditionDefinition）の一覧。
/// セーブ復元時に id からアセットの実体を引くために使う。
/// アセット実体: Assets/Yamaguchi/Resources/DefinitionCatalog.asset
/// エディタメニュー「子育て/セーブ/カタログを更新」で作成・更新する。
/// </summary>
[CreateAssetMenu(menuName = "ChildRearing/DefinitionCatalog", fileName = "DefinitionCatalog")]
public class DefinitionCatalog : ScriptableObject
{
    public const string ResourcePath = "DefinitionCatalog";

    public List<TraitDefinition> traits = new List<TraitDefinition>();
    public List<ConditionDefinition> conditions = new List<ConditionDefinition>();

    private static DefinitionCatalog instance;
    public static DefinitionCatalog I => Load();

    public static DefinitionCatalog Load()
    {
        if (instance == null)
        {
            instance = Resources.Load<DefinitionCatalog>(ResourcePath);
        }
        return instance;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    public TraitDefinition FindTrait(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return traits.Find(t => t != null && t.id == id);
    }

    public ConditionDefinition FindCondition(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return conditions.Find(c => c != null && c.id == id);
    }
}
