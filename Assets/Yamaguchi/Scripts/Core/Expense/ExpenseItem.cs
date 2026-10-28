using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 支出項目（expenses.md 3.1）。アセット1つ＝支出1つ。
/// </summary>
[CreateAssetMenu(menuName = "子育て/支出項目", fileName = "Expense_")]
public class ExpenseItem : ScriptableObject
{
    public string id;                                       // 例 "nursery_fee"（セーブ・制度の covers が参照。後から変えない）
    public string title;                                    // 例 "保育料"
    [TextArea] public string description;
    public ExpenseCategory category;
    public ExpenseMode mode = ExpenseMode.Auto;
    public List<CostTier> tiers = new List<CostTier>();     // 上から順に「年齢が範囲内 かつ 条件を全部満たす」最初の段を使う

    /// <summary>
    /// 今の状況に当てはまる段を上から順に探す。当てはまる段がなければ null。
    /// </summary>
    public CostTier FindTier(GameContext ctx)
    {
        if (ctx == null) ctx = GameContext.Current();
        if (tiers == null) return null;

        foreach (var tier in tiers)
        {
            if (tier != null && tier.Contains(ctx.ChildAgeDays) && tier.RequirementsSatisfied(ctx))
            {
                return tier;
            }
        }
        return null;
    }
}
