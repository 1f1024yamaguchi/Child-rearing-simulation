using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 制度カードに出す文字列を作る static 関数だけを持つ（support-apply.md 7.2）。
/// 状態は SupportManager から読む。日付の表示は GameDate.Format を使い、日付計算は自前でしない。
/// </summary>
public static class SupportScheduleText
{
    // ------------------------------------------------------------
    // 状態バッジ
    // ------------------------------------------------------------

    public static string BadgeLabel(SupportScheme s)
    {
        var mgr = SupportManager.I;
        if (s == null || mgr == null) return "";

        var state = mgr.GetState(s);
        switch (state)
        {
            case SupportState.Locked:    return Today() < s.StartAgeDays ? "まだ対象外" : "条件を満たしていません";
            case SupportState.Available: return "申請できる";
            case SupportState.Received:  return "受け取り済み";
            case SupportState.Active:    return "受給中";
            case SupportState.Suspended: return "支給停止";
            case SupportState.Expired:   return "期限切れ";
            case SupportState.Lost:      return "受給資格なし";
            case SupportState.Ended:     return "受給終了";
            default:                     return "";
        }
    }

    // ------------------------------------------------------------
    // 期限の行
    // ------------------------------------------------------------

    public static string DeadlineLine(SupportScheme s)
    {
        var mgr = SupportManager.I;
        if (s == null || mgr == null) return "";

        var rt = mgr.GetRuntime(s);
        int today = Today();

        switch (rt.state)
        {
            case SupportState.Locked:
                if (today < s.StartAgeDays) return $"{GameDate.Format(s.StartAgeDays)}から申請できます";
                return UnmetLine(mgr, s);

            case SupportState.Available:
            {
                int deadline = mgr.NextDeadlineAgeDays(s);
                if (deadline < 0) return "いつでも申請できます";
                return $"申請期限：{GameDate.Format(deadline)}まで（{RemainText(deadline - today)}）";
            }

            case SupportState.Received:
            {
                if (rt.wageLinkedPendingAmount > 0f)
                    return $"{GameDate.Format(rt.appliedAgeDays)}に申請しました（翌月に{Mathf.RoundToInt(rt.wageLinkedPendingAmount):#,0}円）";
                string amount = rt.lastPaidAmount != 0 ? $"（{rt.lastPaidAmount:+#,0;-#,0}円）" : "";
                return $"{GameDate.Format(rt.appliedAgeDays)}に受け取りました{amount}";
            }

            case SupportState.Active:
                return ActiveLine(mgr, s, rt, today);

            case SupportState.Suspended:
            {
                int loseDay = (rt.suspendedAgeDays - 1) + s.lapseYears * GameDate.DaysPerYear;
                int remain = Mathf.Max(0, loseDay - today);
                return $"手続きすると再開（止まっている分 {rt.owedTierIndexes.Count}か月）。資格がなくなるまで あと{DurationText(remain)}";
            }

            case SupportState.Expired:
                return $"{GameDate.Format(s.DeadlineAgeDays)}で締め切られました";

            case SupportState.Lost:
                return "手続きがなかったため受給資格を失いました";

            case SupportState.Ended:
            {
                int end = LastTierEndAgeDays(s);
                return end >= 0 ? $"{GameDate.Format(end)}で受給が終わりました" : "受給が終わりました";
            }

            default:
                return "";
        }
    }

    /// <summary>期限の行を赤字にするか（申請できる状態で、期限まであと urgentDays 日以下）。</summary>
    public static bool IsUrgent(SupportScheme s, int urgentDays)
    {
        var mgr = SupportManager.I;
        if (s == null || mgr == null) return false;
        if (mgr.GetState(s) != SupportState.Available) return false;
        int days = mgr.DaysUntilDeadline(s);
        return days >= 0 && days <= urgentDays;
    }

    // ------------------------------------------------------------
    // 支給表の行
    // ------------------------------------------------------------

    /// <summary>「3歳未満 月15,000 → 3歳〜18歳 月10,000」。支給表が無ければ空。</summary>
    public static string TierLine(SupportScheme s)
    {
        if (s == null) return "";
        if (s.wageLinkedTiers != null && s.wageLinkedTiers.Count > 0)
        {
            var linked = new StringBuilder();
            foreach (var tier in s.wageLinkedTiers)
            {
                if (tier == null) continue;
                if (linked.Length > 0) linked.Append(" → ");
                string period = tier.toDay < 0 ? $"{tier.fromDay}日目以降" :
                    tier.fromDay == tier.toDay ? $"{tier.fromDay}日目" : $"{tier.fromDay}〜{tier.toDay}日目";
                string basis = tier.basis == WageBase.SalaryBeforeLeave ? "休業前月給" : "現在の給料";
                linked.Append($"{period} {basis}の{tier.rate:P0}");
            }
            return linked.ToString();
        }
        if (s.monthlyTiers == null || s.monthlyTiers.Count == 0) return "";

        var sb = new StringBuilder();
        foreach (var t in s.monthlyTiers)
        {
            if (t == null) continue;
            if (sb.Length > 0) sb.Append(" → ");
            sb.Append(TierText(t));
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------
    // 内部
    // ------------------------------------------------------------

    private static string ActiveLine(SupportManager mgr, SupportScheme s, SupportSchemeRuntime rt, int today)
    {
        var parts = new List<string>();

        var wageTier = mgr.CurrentWageLinkedTier(s);
        if (wageTier != null && mgr.IsWageLinkedEligible(s))
        {
            int amount = mgr.CurrentWageLinkedDailyAmount(s);
            int day = mgr.CurrentWageLinkedDay(s);
            parts.Add($"{wageTier.rate:P0}・日額{amount:#,0}円（{day}日目）／次回 {GameDate.Format(NextMonthStart(today))}");
        }
        else if (s.wageLinkedTiers != null && s.wageLinkedTiers.Count > 0)
        {
            parts.Add($"給付対象外（休業・時短中ではありません）");
        }
        else if (mgr.CurrentTier(s) is PaymentTier cur)
        {
            parts.Add($"今：{TierAmount(cur)}（次回 {GameDate.Format(NextMonthStart(today))}）");
        }
        else if ((s.monthlyTiers == null || s.monthlyTiers.Count == 0) && s.covers != null && s.covers.Count > 0)
        {
            // covers だけの制度（無償化など）：お金は入らず、支出が減る（申請の翌月から）
            parts.Add("支出を肩代わり中（申請した翌月から）");
        }
        else
        {
            parts.Add($"今月は対象外{UnmetTierReasons(s, today)}");
        }

        // 次の段・受給の終わりは「支給表の行」（TierLine）に出ているので、ここでは繰り返さない（1行に収めるため）

        if (s.deadlineType == DeadlineType.Yearly)
        {
            int d = mgr.NextDeadlineAgeDays(s);
            if (d >= 0) parts.Add($"次の手続き期限 {GameDate.Format(d)}（{RemainText(d - today)}）");
        }
        if (s.changesWorkMode)
            parts.Add($"復帰予定 {GameDate.Format(s.workModeUntil.ToAgeDays() + 1)}");
        else if (s.wageBenefitUntil.ToAgeDays() > 0 && s.wageLinkedTiers != null && s.wageLinkedTiers.Count > 0)
            parts.Add($"給付期限 {GameDate.Format(s.wageBenefitUntil.ToAgeDays())}");

        return string.Join("／", parts);
    }

    /// <summary>満たしていない申請条件を「条件：A／B」で並べる。</summary>
    private static string UnmetLine(SupportManager mgr, SupportScheme s)
    {
        var list = new List<string>();
        foreach (var r in mgr.UnmetRequirements(s)) list.Add(r.Describe());
        if (Household.I != null && (s.changesWorkMode || s.requiresWorkMode))
        {
            var parent = Household.I.Get(s.workTarget);
            if (parent != null && s.changesWorkMode && parent.WorkMode != WorkMode.Normal)
                list.Add($"{parent.DisplayName}の現在の勤務が終わるまで申請できません");
            else if (parent != null && s.requiresWorkMode && parent.WorkMode != s.requiredWorkMode)
                list.Add($"{parent.DisplayName}が{s.requiredWorkMode}のとき申請できます");
        }
        return list.Count > 0 ? "条件：" + string.Join("／", list) : "";
    }

    /// <summary>今は期間内なのに追加条件を満たしていない段の条件。「（年収910万円以下）」の形。</summary>
    private static string UnmetTierReasons(SupportScheme s, int today)
    {
        var ctx = GameContext.Current();
        var list = new List<string>();
        foreach (var t in s.monthlyTiers)
        {
            if (t == null || !t.Contains(today) || t.requirements == null) continue;
            foreach (var r in t.requirements)
            {
                if (r != null && !r.IsSatisfied(ctx)) list.Add(r.Describe());
            }
        }
        return list.Count > 0 ? "（" + string.Join("／", list) + "）" : "";
    }

    private static string TierText(PaymentTier t)
    {
        int money = t.MoneyAmount();
        return money != 0 ? $"{t.DisplayLabel()} 月{money:#,0}" : t.DisplayLabel();
    }

    private static string TierAmount(PaymentTier t)
    {
        int money = t.MoneyAmount();
        return money != 0 ? $"月{money:#,0}" : t.DisplayLabel();
    }

    /// <summary>支給表の最後の toAge（通算日数）。無ければ -1。</summary>
    private static int LastTierEndAgeDays(SupportScheme s)
    {
        if (s.monthlyTiers == null) return -1;
        int last = -1;
        foreach (var t in s.monthlyTiers)
        {
            if (t != null) last = Mathf.Max(last, t.toAge.ToAgeDays());
        }
        return last;
    }

    /// <summary>来月1日（毎月の支給の日）の通算日数。</summary>
    private static int NextMonthStart(int today)
    {
        GameDate.FromAgeDays(today, out int y, out int m, out int _);
        return m >= 12 ? GameDate.ToAgeDays(y + 1, 1, 1) : GameDate.ToAgeDays(y, m + 1, 1);
    }

    private static string RemainText(int days) => days <= 0 ? "今日まで" : $"あと{days}日";

    private static string DurationText(int days)
    {
        int years = days / GameDate.DaysPerYear;
        int rest = days % GameDate.DaysPerYear;
        return years > 0 ? $"{years}年{rest}日" : $"{rest}日";
    }

    private static int Today()
    {
        return GameState.I != null ? Mathf.FloorToInt(GameState.I.AgeDays) : 0;
    }
}
