using System.Collections.Generic;
using Maruyama.Core;

/// <summary>制度の中身を日本語の文章にする（まとめ表示・一覧の「種類」判定に使う）。</summary>
public static class SupportSchemeDescriber
{
    public enum SchemeKind { LumpSum, MonthlyAllowance, Cover, MedicalAid, Leave, Other }

    public static string KindName(SchemeKind k)
    {
        switch (k)
        {
            case SchemeKind.LumpSum: return "一時金";
            case SchemeKind.MonthlyAllowance: return "毎月の手当";
            case SchemeKind.Cover: return "肩代わり";
            case SchemeKind.MedicalAid: return "医療費助成";
            case SchemeKind.Leave: return "育休/時短";
            default: return "その他";
        }
    }

    public static SchemeKind KindOf(SupportScheme s)
    {
        if (s == null) return SchemeKind.Other;
        if (s.changesWorkMode || Has(s.wageLinkedTiers)) return SchemeKind.Leave;
        if (Has(s.medicalExpenseCovers)) return SchemeKind.MedicalAid;
        if (Has(s.covers) && !Has(s.monthlyTiers)) return SchemeKind.Cover;
        if (s.effectTiming == EffectTiming.Monthly && Has(s.monthlyTiers)) return SchemeKind.MonthlyAllowance;
        if (s.effectTiming == EffectTiming.OnApply && HasMoneyEffect(s.effects)) return SchemeKind.LumpSum;
        return SchemeKind.Other;
    }

    public static string CategoryName(SupportCategory c)
    {
        int i = (int)c;
        return i >= 0 && i < SupportEditorGUI.CategoryNames.Length ? SupportEditorGUI.CategoryNames[i] : c.ToString();
    }

    public static string Day(GameDay d) => $"{d.year}歳{d.month}月{d.day}日";

    public static string Yen(int v) => v.ToString("#,0") + "円";

    public static string EffectText(Effect e)
    {
        if (e == null) return "（空）";
        int k = (int)e.kind;
        int t = (int)e.target;
        string kind = k >= 0 && k < SupportEditorGUI.EventKindNames.Length ? SupportEditorGUI.EventKindNames[k] : e.kind.ToString();
        string target = t >= 0 && t < SupportEditorGUI.TargetNames.Length ? SupportEditorGUI.TargetNames[t] : e.target.ToString();
        string value = e.kind == EffectKind.Money ? SupportEditorGUI.Yen(e.value) : e.value.ToString("+0.##;-0.##;0");
        string dur = e.duration == EffectDuration.Days ? $"（{e.days}日のあいだ）" : e.duration == EffectDuration.ThisMonth ? "（今月のあいだ）" : "";
        return $"{kind}（{target}） {value}{dur}";
    }

    // ─── 申請開始・期限 ─────────────────────────────────────────
    public static string StartText(SupportScheme s) => Day(s.start) + "から";

    public static string DeadlineText(SupportScheme s)
    {
        switch (s.deadlineType)
        {
            case DeadlineType.Once:
                return Day(s.deadline) + "まで（1回きり）";
            case DeadlineType.Yearly:
                return $"毎年{s.yearly.month}月{s.yearly.day}日まで";
            default:
                return "なし";
        }
    }

    /// <summary>「申請できる期間」の下に出す説明文。</summary>
    public static string PeriodText(SupportScheme s)
    {
        string text = $"{Day(s.start)}から申請できます。";
        switch (s.deadlineType)
        {
            case DeadlineType.Once:
                text += $"{Day(s.deadline)}（その日のうちは申請できる）を過ぎると、申請しないまま「期限切れ」になります。";
                break;
            case DeadlineType.Yearly:
                text += $"受給中は毎年{s.yearly.month}月{s.yearly.day}日までに手続き（更新）が必要です。" +
                        $"期限の{s.renewWindowDays}日前から［更新する］が押せます。" +
                        $"期限を過ぎると支給停止になり、そのまま{s.lapseYears}年たつと受給資格を失います。";
                break;
            default:
                text += "期限はありません。";
                break;
        }
        return text;
    }

    // ─── 支給表・条件 ───────────────────────────────────────────
    public static List<string> TierLines(SupportScheme s)
    {
        var lines = new List<string>();
        if (s.monthlyTiers == null) return lines;
        for (int i = 0; i < s.monthlyTiers.Count; i++)
        {
            var t = s.monthlyTiers[i];
            if (t == null) { lines.Add($"段{i + 1}：（空）"); continue; }
            string cond = HasRequirement(t.requirements) ? "　※この段だけの条件あり" : "";
            lines.Add($"段{i + 1} {t.DisplayLabel()}（{Day(t.fromAge)}〜{Day(t.toAge)}）：毎月 {Yen(t.MoneyAmount())}{cond}");
        }
        return lines;
    }

    public static List<string> RequirementLines(IEnumerable<RequirementSO> list)
    {
        var lines = new List<string>();
        if (list == null) return lines;
        foreach (var r in list)
        {
            if (r == null) { lines.Add("（空の行）"); continue; }
            lines.Add(SafeDescribe(r));
        }
        return lines;
    }

    public static string SafeDescribe(RequirementSO r)
    {
        if (r == null) return "（空）";
        try { return r.Describe(); }
        catch (System.Exception) { return "（説明を取れませんでした）"; }
    }

    /// <summary>各歳の初日に当てはまる段（条件は無視）の月額を、同じ額が続く範囲ごとにまとめる。</summary>
    public static List<string> AgeTable(SupportScheme s)
    {
        var lines = new List<string>();
        if (s.monthlyTiers == null || s.monthlyTiers.Count == 0) return lines;

        int groupStart = -1;
        int groupAmount = 0;
        for (int y = 0; y <= 20; y++)
        {
            int amount = -1;   // -1 ＝ 当てはまる段なし
            if (y < 20)
            {
                int day = y * GameDate.DaysPerYear;
                foreach (var t in s.monthlyTiers)
                {
                    if (t != null && t.Contains(day)) { amount = t.MoneyAmount(); break; }
                }
            }

            if (groupStart >= 0 && amount != groupAmount)
            {
                string range = groupStart == y - 1 ? groupStart + "歳" : groupStart + "〜" + (y - 1) + "歳";
                lines.Add(range + "：" + Yen(groupAmount));
                groupStart = -1;
            }
            if (groupStart < 0 && amount >= 0)
            {
                groupStart = y;
                groupAmount = amount;
            }
        }
        return lines;
    }

    /// <summary>合計の目安（円）。各段の MoneyAmount × 段の月数 ＋ 申請時のお金。条件は見ない。</summary>
    public static long TotalEstimate(SupportScheme s)
    {
        long total = 0;
        if (s.effects != null)
        {
            foreach (var e in s.effects)
            {
                if (e != null && e.kind == EffectKind.Money) total += (long)e.value;
            }
        }
        if (s.monthlyTiers != null)
        {
            foreach (var t in s.monthlyTiers)
            {
                if (t == null) continue;
                int months = (t.toAge.year * 12 + t.toAge.month) - (t.fromAge.year * 12 + t.fromAge.month) + 1;
                if (months > 0) total += (long)t.MoneyAmount() * months;
            }
        }
        return total;
    }

    /// <summary>画面上部の「まとめ」の文章（複数行）。</summary>
    public static string Summary(SupportScheme s)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"種類：{KindName(KindOf(s))}　／　カテゴリ：{CategoryName(s.category)}");
        sb.AppendLine($"申請開始：{StartText(s)}");
        sb.AppendLine($"期限：{DeadlineText(s)}");

        var tiers = TierLines(s);
        if (tiers.Count > 0)
        {
            sb.AppendLine("支給表：");
            foreach (var l in tiers) sb.AppendLine("　" + l);
        }

        var reqs = RequirementLines(s.applyRequirements);
        if (reqs.Count > 0)
        {
            sb.AppendLine("申請条件：");
            foreach (var l in reqs) sb.AppendLine("　・" + l);
        }
        else
        {
            sb.AppendLine("申請条件：なし");
        }

        if (HasMoneyEffect(s.effects) || tiers.Count > 0)
            sb.Append($"合計の目安：約 {TotalEstimate(s):#,0}円（条件を全部満たした場合。期間の最初から最後まで受け取ったとして計算）");
        else
            sb.Append("合計の目安：お金の支給はありません");
        return sb.ToString();
    }

    /// <summary>Inspector に出す1行のまとめ。</summary>
    public static string OneLine(SupportScheme s) =>
        $"{KindName(KindOf(s))}／{CategoryName(s.category)}／申請 {StartText(s)}／期限 {DeadlineText(s)}";

    // ─── 内部 ───────────────────────────────────────────────────
    private static bool Has<T>(List<T> l) => l != null && l.Count > 0;

    private static bool HasRequirement(List<RequirementSO> l)
    {
        if (l == null) return false;
        foreach (var r in l) if (r != null) return true;
        return false;
    }

    private static bool HasMoneyEffect(List<Effect> l)
    {
        if (l == null) return false;
        foreach (var e in l) if (e != null && e.kind == EffectKind.Money) return true;
        return false;
    }
}

