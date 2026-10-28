using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>子どもの年齢の1年ぶんの合計（誕生日のまとめ用）。</summary>
[Serializable]
public class LedgerYearSummary
{
    public int age;        // 子の年齢（歳）
    public int income;     // 入ってきた額の合計（正の数）
    public int expense;    // 出ていった額の合計（正の数）
    public int support;    // 支援で助かった額（支援制度の入金 ＋ covered）
}

/// <summary>Ledger のセーブ内容。</summary>
[Serializable]
public class LedgerSaveData
{
    [Serializable]
    public class CategoryTotal
    {
        public int category;   // ExpenseCategory の数値
        public int total;      // 入金 +／出金 − の合計
    }

    public List<LedgerLine> lines = new List<LedgerLine>();
    public List<CategoryTotal> categoryTotals = new List<CategoryTotal>();
    public int totalCovered;
    public List<LedgerYearSummary> years = new List<LedgerYearSummary>();
}

/// <summary>
/// 家計簿の本体（expenses.md 5章）。シーンに置かなくても Ledger.I で使える。セーブ対象。
/// お金が動いたら Record を1回呼ぶ。同じ月・同じ名前・同じ種類の行は足し合わせる。
/// 行は直近24か月ぶんだけ持ち、種類ごとの合計と年ごとの合計は残す。
/// </summary>
public class Ledger
{
    public const int MonthsKept = 24;

    private static Ledger instance;
    public static Ledger I => instance ?? (instance = new Ledger());

    // プレイ開始のたびに前回の記録を持ち越さない（Domain Reload を切っている場合の対策）
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    private readonly List<LedgerLine> lines = new List<LedgerLine>();
    private readonly Dictionary<ExpenseCategory, int> categoryTotals = new Dictionary<ExpenseCategory, int>();
    private readonly Dictionary<int, LedgerYearSummary> years = new Dictionary<int, LedgerYearSummary>();
    private int totalCovered;

    public event Action<LedgerLine> OnRecorded;

    // ------------------------------------------------------------
    // 記録
    // ------------------------------------------------------------

    /// <summary>
    /// 今の月に1行足す。amount は入金 +／出金 −。covered は支援で肩代わりされた額（0以上）。
    /// </summary>
    public void Record(LedgerSource source, int amount, int covered = 0)
    {
        if (source == null) source = LedgerSource.Unknown;
        covered = Mathf.Max(0, covered);
        if (amount == 0 && covered == 0) return;

        int month = CurrentMonthIndex();
        string name = string.IsNullOrEmpty(source.name) ? LedgerSource.Unknown.name : source.name;

        LedgerLine line = null;
        foreach (var l in lines)
        {
            if (l.monthIndex == month && l.source == name && l.category == source.category)
            {
                line = l;
                break;
            }
        }
        if (line == null)
        {
            line = new LedgerLine { monthIndex = month, source = name, category = source.category };
            lines.Add(line);
        }
        line.amount += amount;
        line.covered += covered;

        // 合計
        categoryTotals.TryGetValue(source.category, out int cur);
        categoryTotals[source.category] = cur + amount;
        totalCovered += covered;

        // 年ごとの合計
        int age = month / 12;
        if (!years.TryGetValue(age, out var y))
        {
            y = new LedgerYearSummary { age = age };
            years[age] = y;
        }
        if (amount > 0) y.income += amount; else y.expense += -amount;
        y.support += covered;
        if (source.category == ExpenseCategory.支援制度 && amount > 0) y.support += amount;

        // 古い行を消す（合計は残す）
        lines.RemoveAll(l => l.monthIndex <= month - MonthsKept);

        OnRecorded?.Invoke(line);
    }

    // ------------------------------------------------------------
    // 読み取り
    // ------------------------------------------------------------

    /// <summary>指定した月（歳*12+月-1）の行。コピーを返す。</summary>
    public List<LedgerLine> LinesOfMonth(int monthIndex)
    {
        var result = new List<LedgerLine>();
        foreach (var l in lines)
        {
            if (l.monthIndex == monthIndex) result.Add(l);
        }
        return result;
    }

    /// <summary>種類ごとのゲーム全体の合計（入金 +／出金 −）。</summary>
    public int TotalOf(ExpenseCategory category)
    {
        categoryTotals.TryGetValue(category, out int v);
        return v;
    }

    /// <summary>「子育てにかかったお金」の合計（IsChildCost の種類の支出）。正の数。</summary>
    public int TotalChildCost
    {
        get
        {
            long sum = 0;
            foreach (var kv in categoryTotals)
            {
                if (IsChildCost(kv.Key)) sum += kv.Value;
            }
            return (int)Math.Max(0L, -sum);
        }
    }

    /// <summary>受け取った支援の合計（支援制度の入金 ＋ 肩代わりされた額）。</summary>
    public int TotalSupport => Mathf.Max(0, TotalOf(ExpenseCategory.支援制度)) + totalCovered;

    /// <summary>子の年齢（歳）の1年ぶんの合計。記録が無ければ 0 のもの。</summary>
    public LedgerYearSummary YearSummary(int age)
    {
        if (years.TryGetValue(age, out var y))
        {
            return new LedgerYearSummary { age = y.age, income = y.income, expense = y.expense, support = y.support };
        }
        return new LedgerYearSummary { age = age };
    }

    /// <summary>「子育てにかかったお金」に数える種類。判定はここにまとめる。</summary>
    public static bool IsChildCost(ExpenseCategory c)
    {
        switch (c)
        {
            case ExpenseCategory.保育:
            case ExpenseCategory.学校:
            case ExpenseCategory.塾習い事:
            case ExpenseCategory.食費:
            case ExpenseCategory.医療:
                return true;
            default:
                return false;
        }
    }

    // ------------------------------------------------------------
    // セーブ・ロード・クリア
    // ------------------------------------------------------------

    public LedgerSaveData ToSave()
    {
        var data = new LedgerSaveData { totalCovered = totalCovered };
        foreach (var l in lines)
        {
            data.lines.Add(new LedgerLine
            {
                monthIndex = l.monthIndex, source = l.source, category = l.category,
                amount = l.amount, covered = l.covered
            });
        }
        foreach (var kv in categoryTotals)
        {
            data.categoryTotals.Add(new LedgerSaveData.CategoryTotal { category = (int)kv.Key, total = kv.Value });
        }
        foreach (var y in years.Values)
        {
            data.years.Add(new LedgerYearSummary { age = y.age, income = y.income, expense = y.expense, support = y.support });
        }
        return data;
    }

    public void Restore(LedgerSaveData data)
    {
        Clear();
        if (data == null) return;

        if (data.lines != null)
        {
            foreach (var l in data.lines)
            {
                if (l == null) continue;
                lines.Add(new LedgerLine
                {
                    monthIndex = l.monthIndex, source = l.source, category = l.category,
                    amount = l.amount, covered = l.covered
                });
            }
        }
        if (data.categoryTotals != null)
        {
            foreach (var t in data.categoryTotals)
            {
                if (t != null) categoryTotals[(ExpenseCategory)t.category] = t.total;
            }
        }
        if (data.years != null)
        {
            foreach (var y in data.years)
            {
                if (y != null) years[y.age] = new LedgerYearSummary { age = y.age, income = y.income, expense = y.expense, support = y.support };
            }
        }
        totalCovered = data.totalCovered;
    }

    /// <summary>最初から始めるとき用。全部消す。</summary>
    public void Clear()
    {
        lines.Clear();
        categoryTotals.Clear();
        years.Clear();
        totalCovered = 0;
    }

    // ------------------------------------------------------------

    private static int CurrentMonthIndex()
    {
        if (GameState.I == null) return 0;
        int y, m, d;
        GameDate.FromAgeDays(GameState.I.AgeDays, out y, out m, out d);
        return y * 12 + m - 1;
    }
}
