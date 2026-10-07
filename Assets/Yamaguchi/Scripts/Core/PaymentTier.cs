using System.Collections.Generic;
using Maruyama.Core;

/// <summary>支給表の1段（support-apply.md 3.2）。「何歳から何歳まで、毎月いくら」。</summary>
[System.Serializable]
public class PaymentTier
{
    public string  label;                          // 表示用。例 "3歳未満"（空なら年齢から自動）
    public GameDay fromAge;                        // この日から（含む）
    public GameDay toAge;                          // この日まで（含む）
    public List<Effect> monthlyEffects = new List<Effect>();               // この段の毎月の効果。例 Money +15,000 / Household
    public List<RequirementSO> requirements = new List<RequirementSO>();   // この段に当てはまる追加条件（空＝なし）

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

    /// <summary>この段で毎月もらえるお金の合計（表示・ログ用）。</summary>
    public int MoneyAmount()
    {
        float sum = 0f;
        if (monthlyEffects != null)
        {
            foreach (var e in monthlyEffects)
            {
                if (e != null && e.kind == EffectKind.Money) sum += e.value;
            }
        }
        return UnityEngine.Mathf.RoundToInt(sum);
    }

    /// <summary>label があればそれ、無ければ年齢から「0〜2歳」。</summary>
    public string DisplayLabel()
    {
        if (!string.IsNullOrEmpty(label)) return label;
        return $"{fromAge.year}〜{toAge.year}歳";
    }
}
