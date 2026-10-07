using UnityEngine;

/// <summary>世帯年収が範囲内（support-apply.md 4.3）。年収は IncomeHistory（実際に入った給料・額面）。</summary>
[CreateAssetMenu(menuName = "子育て/申請条件/世帯年収")]
public class IncomeRequirementSO : RequirementSO
{
    [Tooltip("この額以下なら満たす（円）。たたき台の値は Inspector で調整する")]
    [Min(0)] public int maxAnnualIncome = 9100000;

    [Tooltip("この額以上なら満たす（円）。0 なら下限なし")]
    [Min(0)] public int minAnnualIncome = 0;

    public override bool IsSatisfied(GameContext ctx)
    {
        return ctx.AnnualIncome >= minAnnualIncome && ctx.AnnualIncome <= maxAnnualIncome;
    }

    public override string Describe()
    {
        if (minAnnualIncome > 0)
            return $"年収 {Man(minAnnualIncome)}〜{Man(maxAnnualIncome)}";
        return $"年収 {Man(maxAnnualIncome)}以下";
    }

    // 910万円 / 912.5万円
    private static string Man(int yen)
    {
        float man = yen / 10000f;
        return Mathf.Approximately(man, Mathf.Round(man)) ? $"{Mathf.RoundToInt(man)}万円" : $"{man:0.#}万円";
    }
}
