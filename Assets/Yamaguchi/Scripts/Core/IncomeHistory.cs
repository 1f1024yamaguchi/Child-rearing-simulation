using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 直近12か月に実際に受け取った給料（母＋父・額面）の記録（support-apply.md 4.2.1）。
/// 年収の判定に使う。シーンに置かなくても IncomeHistory.I で使える。セーブ対象（ToSave / Restore）。
/// </summary>
public class IncomeHistory
{
    public const int MonthsKept = 12;

    private static IncomeHistory instance;
    public static IncomeHistory I => instance ?? (instance = new IncomeHistory());

    // プレイ開始のたびに前回の記録を持ち越さない（Domain Reload を切っている場合の対策）
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    private readonly List<int> months = new List<int>();   // 古い順

    /// <summary>記録した月数（最大12）。</summary>
    public int Count => months.Count;

    /// <summary>給料日に呼ぶ。その月の受取額（母＋父・額面）を記録する。</summary>
    public void Record(int monthlyIncome)
    {
        months.Add(Mathf.Max(0, monthlyIncome));
        while (months.Count > MonthsKept) months.RemoveAt(0);
    }

    /// <summary>
    /// 世帯の年収。12か月たまっていれば直近12か月の合計。
    /// 12か月未満は記録した月の平均 × 12。1か月も無いときは両親の MonthlySalary の合計 × 12。
    /// </summary>
    public int AnnualIncome
    {
        get
        {
            if (months.Count == 0)
            {
                int salary = Household.I != null ? Household.I.TotalMonthlySalary : 0;
                return salary * 12;
            }

            long sum = 0;
            foreach (int m in months) sum += m;
            if (months.Count >= MonthsKept) return (int)sum;
            return Mathf.RoundToInt((float)sum / months.Count * 12f);
        }
    }

    /// <summary>セーブ用。古い順の月収リスト。</summary>
    public List<int> ToSave() => new List<int>(months);

    /// <summary>ロード用。</summary>
    public void Restore(List<int> saved)
    {
        months.Clear();
        if (saved == null) return;
        foreach (int m in saved) months.Add(Mathf.Max(0, m));
        while (months.Count > MonthsKept) months.RemoveAt(0);
    }
}
