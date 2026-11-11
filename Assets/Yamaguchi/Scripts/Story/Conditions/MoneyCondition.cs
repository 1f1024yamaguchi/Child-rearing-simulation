using Maruyama.Events;
using UnityEngine;

/// <summary>数値の比較演算。</summary>
public enum NumberComparison
{
    以上,
    以下
}

/// <summary>
/// 世帯の所持金（GameState.I.Money）が指定額以上／以下のときだけ発生を許す条件。
/// </summary>
public class MoneyCondition : EventConditionSO, IDescribable
{
    [Tooltip("比較方法（以上／以下）")]
    public NumberComparison comparison = NumberComparison.以下;

    [Tooltip("基準となる金額（円）")]
    [Min(0)]
    public int amount = 100000;

    public override bool IsSatisfied()
    {
        if (GameState.I == null) return true;

        int money = GameState.I.Money;
        return comparison == NumberComparison.以上 ? money >= amount : money <= amount;
    }

    public string Describe()
    {
        return $"所持金が {amount:#,0}円{comparison}";
    }
}

