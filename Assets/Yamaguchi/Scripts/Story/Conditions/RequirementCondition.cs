using Maruyama.Events;
using UnityEngine;

/// <summary>
/// 支援制度の申請条件（RequirementSO）をそのまま流用して判定する条件。
/// </summary>
public class RequirementCondition : EventConditionSO, IDescribable
{
    [Tooltip("流用する支援制度の申請条件")]
    public RequirementSO requirement;

    public override bool IsSatisfied()
    {
        if (requirement == null) return true;
        if (GameState.I == null) return true;

        return requirement.IsSatisfied(GameContext.Current());
    }

    public string Describe()
    {
        return requirement != null ? requirement.Describe() : "（条件未設定）";
    }
}

