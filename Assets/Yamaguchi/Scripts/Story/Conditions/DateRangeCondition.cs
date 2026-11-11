using Maruyama.Events;
using UnityEngine;

/// <summary>
/// 指定した日付の範囲（○歳○月○日〜○歳○月○日）のときだけ発生を許す条件。両端を含む。
/// </summary>
public class DateRangeCondition : EventConditionSO, IDescribable
{
    [Tooltip("開始日（この日以降）")]
    public GameDay fromDay;

    [Tooltip("終了日（この日以前）")]
    public GameDay toDay;

    public override bool IsSatisfied()
    {
        if (GameState.I == null) return true;

        int today = Mathf.RoundToInt(GameState.I.AgeDays);
        int lo = Mathf.Min(fromDay.ToAgeDays(), toDay.ToAgeDays());
        int hi = Mathf.Max(fromDay.ToAgeDays(), toDay.ToAgeDays());
        return today >= lo && today <= hi;
    }

    public string Describe()
    {
        string fromStr = $"{fromDay.year}歳{fromDay.month}月{fromDay.day}日";
        string toStr = $"{toDay.year}歳{toDay.month}月{toDay.day}日";
        if (fromStr == toStr) return fromStr;
        return $"{fromStr}〜{toStr}";
    }
}

