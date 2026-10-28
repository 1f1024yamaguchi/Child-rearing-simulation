using System.Collections.Generic;
using Maruyama.Core;

/// <summary>
/// 支出の年齢・条件ごとの段（expenses.md 3.1）。PaymentTier と同じ考え方。
/// </summary>
[System.Serializable]
public class CostTier
{
    public string label;                                                      // 表示用（例 "0〜2歳"）
    public GameDay fromAge;                                                   // この日から（含む）
    public GameDay toAge;                                                     // この日まで（含む）
    public int monthlyCost;                                                   // この段の毎月の額（円）
    public List<RequirementSO> requirements = new List<RequirementSO>();      // 例：年収○円以下（保育料の所得段階）
    public List<Effect> monthlyEffects = new List<Effect>();                  // お金以外の毎月の効果（塾：成績+、子ストレス+）

    /// <summary>通算日数がこの段の期間に入っているか。</summary>
    public bool Contains(int ageDays) => ageDays >= fromAge.ToAgeDays() && ageDays <= toAge.ToAgeDays();

    /// <summary>追加条件を全部満たしているか（空なら true）。</summary>
    public bool RequirementsSatisfied(GameContext ctx)
    {
        if (requirements == null) return true;
        foreach (var r in requirements)
        {
            if (r != null && !r.IsSatisfied(ctx)) return false;
        }
        return true;
    }

    /// <summary>label があればそれ、無ければ年齢から「0〜2歳」。</summary>
    public string DisplayLabel()
    {
        if (!string.IsNullOrEmpty(label)) return label;
        return $"{fromAge.year}〜{toAge.year}歳";
    }
}
