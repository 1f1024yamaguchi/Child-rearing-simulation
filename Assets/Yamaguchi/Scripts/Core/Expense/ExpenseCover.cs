using UnityEngine;

/// <summary>
/// 支援制度による支出の肩代わり1件（expenses.md 4章）。SupportScheme.covers に並べる。
/// 制度が「受給中」のあいだ、期間内の月だけ、対象の支出を減らす。
/// </summary>
[System.Serializable]
public class ExpenseCover
{
    public ExpenseItem expense;            // 肩代わりする支出（例：保育料）
    public CoverType   type = CoverType.Full;
    [Tooltip("Rate なら 0〜1、UpTo なら円。Full では使わない")]
    public float       value;
    public GameDay     fromAge;            // この期間だけ（含む。例：3歳1月1日）
    public GameDay     toAge;              // （含む。例：5歳12月31日）

    /// <summary>通算日数がこの肩代わりの期間に入っているか。</summary>
    public bool Contains(int ageDays) => ageDays >= fromAge.ToAgeDays() && ageDays <= toAge.ToAgeDays();

    /// <summary>この支出を肩代わりする対象か（同じアセット、または同じ id）。</summary>
    public bool Targets(ExpenseItem item)
    {
        if (expense == null || item == null) return false;
        if (expense == item) return true;
        return !string.IsNullOrEmpty(expense.id) && expense.id == item.id;
    }

    /// <summary>支出の額 cost のうち、この肩代わりで減る額（0〜cost）。</summary>
    public int CoveredAmount(int cost)
    {
        if (cost <= 0) return 0;
        int covered;
        switch (type)
        {
            case CoverType.Full: covered = cost; break;
            case CoverType.Rate: covered = Mathf.RoundToInt(cost * Mathf.Clamp01(value)); break;
            case CoverType.UpTo: covered = Mathf.RoundToInt(Mathf.Max(0f, value)); break;
            default:             covered = 0; break;
        }
        return Mathf.Clamp(covered, 0, cost);
    }
}
