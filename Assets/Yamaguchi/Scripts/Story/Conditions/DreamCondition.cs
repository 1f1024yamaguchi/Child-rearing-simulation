using Maruyama.Events;
using UnityEngine;

/// <summary>
/// 将来の夢を見る条件部品。
/// - 職業を空にすると「夢が決まっている（どの職業でも）」。夢が生まれるイベントに使う（文章は {dream}）。
/// - 職業を入れると「夢がその職業」。職業ごとのストーリーの入口に使う。
/// </summary>
public class DreamCondition : EventConditionSO, IDescribable
{
    [Tooltip("夢の職業（空 = どの職業でも、夢が決まっていればよい）")]
    public CareerDefinition career;

    [Tooltip("true = 満たす（決まっている／その職業）、false = 満たさない")]
    public bool isMatch = true;

    public override bool IsSatisfied()
    {
        var ds = DreamSystem.I;
        bool match;
        if (ds == null || !ds.HasDream) match = false;
        else if (career == null) match = true;
        else match = ds.Dream == career || (!string.IsNullOrEmpty(career.id) && ds.Dream.id == career.id);
        return isMatch ? match : !match;
    }

    public string Describe()
    {
        if (career == null) return isMatch ? "将来の夢が決まっている" : "将来の夢がまだ決まっていない";
        return isMatch ? $"将来の夢が「{career.DisplayNameOrName}」" : $"将来の夢が「{career.DisplayNameOrName}」ではない";
    }
}
