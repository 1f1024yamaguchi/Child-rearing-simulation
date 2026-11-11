using Maruyama.Events;
using UnityEngine;

/// <summary>
/// 指定した支援制度の受給状況（受給中／受けていない）を判定する条件。
/// </summary>
public class SupportCondition : EventConditionSO, IDescribable
{
    [Tooltip("対象の支援制度")]
    public SupportScheme scheme;

    [Tooltip("受給中であることを期待するか（true = 受給中 / false = 受けていない）")]
    public bool mustBeActive = true;

    public override bool IsSatisfied()
    {
        if (SupportManager.I == null || scheme == null) return true;

        bool isActive = SupportManager.I.GetState(scheme) == SupportState.Active;
        return isActive == mustBeActive;
    }

    public string Describe()
    {
        string title = scheme != null ? scheme.title : "（制度未指定）";
        string stateStr = mustBeActive ? "受給中" : "受けていない";
        return $"制度「{title}」を{stateStr}";
    }
}

