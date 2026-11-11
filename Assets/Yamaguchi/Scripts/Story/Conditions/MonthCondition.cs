using System.Collections.Generic;
using Maruyama.Events;
using UnityEngine;

/// <summary>
/// 指定した月のときだけ発生を許す条件。
/// </summary>
public class MonthCondition : EventConditionSO, IDescribable
{
    [Tooltip("発生を許す月（1〜12）のリスト")]
    public List<int> months = new List<int>();

    public override bool IsSatisfied()
    {
        if (GameState.I == null) return true;
        if (months == null || months.Count == 0) return true;

        GameDate.FromAgeDays(GameState.I.AgeDays, out _, out int currentMonth, out _);
        return months.Contains(currentMonth);
    }

    public string Describe()
    {
        if (months == null || months.Count == 0) return "すべての月";

        var sorted = new List<int>(months);
        sorted.Sort();

        if (sorted.Count == 12) return "通年（全月）";

        var set = new HashSet<int>(sorted);
        bool hasSpring = set.Contains(3) && set.Contains(4) && set.Contains(5);
        bool hasSummer = set.Contains(6) && set.Contains(7) && set.Contains(8);
        bool hasAutumn = set.Contains(9) && set.Contains(10) && set.Contains(11);
        bool hasWinter = set.Contains(12) && set.Contains(1) && set.Contains(2);

        var seasons = new List<string>();
        if (hasSpring && sorted.Count == 3) return "春（3・4・5月）";
        if (hasSummer && sorted.Count == 3) return "夏（6・7・8月）";
        if (hasAutumn && sorted.Count == 3) return "秋（9・10・11月）";
        if (hasWinter && sorted.Count == 3) return "冬（12・1・2月）";

        if (hasSpring) seasons.Add("春");
        if (hasSummer) seasons.Add("夏");
        if (hasAutumn) seasons.Add("秋");
        if (hasWinter) seasons.Add("冬");

        string monthList = string.Join("・", sorted) + "月";
        if (seasons.Count > 0)
        {
            return $"{string.Join("・", seasons)}（{monthList}）";
        }
        return monthList;
    }
}

