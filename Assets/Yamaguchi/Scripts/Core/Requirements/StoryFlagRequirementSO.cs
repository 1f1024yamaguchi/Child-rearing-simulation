using UnityEngine;

/// <summary>
/// ストーリーフラグの状態を判定する RequirementSO。
/// 支援制度の申請条件や進路・職業の判定条件で使用できる。
/// </summary>
[CreateAssetMenu(fileName = "Req_StoryFlag_", menuName = "子育て/支援制度/条件/フラグ")]
public class StoryFlagRequirementSO : RequirementSO
{
    [Tooltip("判定対象のフラグ")]
    public StoryFlag flag;

    [Tooltip("true: フラグが立っていること、false: 立っていないこと")]
    public bool mustBeSet = true;

    public override bool IsSatisfied(GameContext ctx)
    {
        if (flag == null) return true;
        bool has = StoryState.I != null && StoryState.I.HasFlag(flag);
        return has == mustBeSet;
    }

    public override string Describe()
    {
        string name = flag != null ? flag.displayName : "（フラグ未指定）";
        return mustBeSet ? $"フラグ「{name}」が立っている" : $"フラグ「{name}」が立っていない";
    }
}

