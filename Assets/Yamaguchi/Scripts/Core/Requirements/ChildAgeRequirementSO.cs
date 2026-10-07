using UnityEngine;

/// <summary>子どもの年齢が範囲内（support-apply.md 4.3）。</summary>
[CreateAssetMenu(menuName = "子育て/申請条件/子どもの年齢")]
public class ChildAgeRequirementSO : RequirementSO
{
    public GameDay minAge = new GameDay(0, 1, 1);     // この日から（含む）
    public GameDay maxAge = new GameDay(19, 12, 31);  // この日まで（含む）

    public override bool IsSatisfied(GameContext ctx)
    {
        return ctx.ChildAgeDays >= minAge.ToAgeDays() && ctx.ChildAgeDays <= maxAge.ToAgeDays();
    }

    public override string Describe()
    {
        return $"子どもが {DescribeStart(minAge)}〜{DescribeEnd(maxAge)}";
    }

    // 1月1日始まり／12月31日終わりなら「歳」だけ、そうでなければ日付まで出す
    private static string DescribeStart(GameDay d)
    {
        return (d.month == 1 && d.day == 1) ? $"{d.year}歳" : $"{d.year}歳{d.month}月{d.day}日";
    }

    private static string DescribeEnd(GameDay d)
    {
        return (d.month == 12 && d.day == 31) ? $"{d.year}歳" : $"{d.year}歳{d.month}月{d.day}日";
    }

    private void OnValidate()
    {
        minAge = Fix(minAge);
        maxAge = Fix(maxAge);
    }

    private static GameDay Fix(GameDay d)
    {
        d.year  = Mathf.Clamp(d.year, 0, 19);
        d.month = Mathf.Clamp(d.month, 1, 12);
        d.day   = Mathf.Clamp(d.day, 1, GameDate.DaysInMonth(d.month));
        return d;
    }
}
