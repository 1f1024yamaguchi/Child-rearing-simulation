using Maruyama.Events;
using UnityEngine;

/// <summary>
/// ストーリーフラグの状態（立っている／立っていない）を判定する条件部品。
/// </summary>
public class StoryFlagCondition : EventConditionSO, IDescribable
{
    [Tooltip("対象のフラグ")]
    public StoryFlag flag;

    [Tooltip("フラグの状態条件（true = 立っている / false = 立っていない）")]
    public bool isSet = true;

    public override bool IsSatisfied()
    {
        if (flag == null) return false;
        if (StoryState.I == null) return true;

        bool current = StoryState.I.HasFlag(flag);
        return isSet ? current : !current;
    }

    public string Describe()
    {
        if (flag == null) return "（フラグ未設定）";
        string name = !string.IsNullOrEmpty(flag.displayName) ? flag.displayName : flag.name;
        return isSet ? $"フラグ「{name}」が立っている" : $"フラグ「{name}」が立っていない";
    }
}

