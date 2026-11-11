using System;
using System.Collections.Generic;
using System.Text;
using Maruyama.Core;
using Maruyama.Events;

/// <summary>
/// イベントを文章にする（まとめ・出方・条件・効果チップ）。表示用で、データは書き換えない。
/// 今ある条件の部品（AgeRange / ChildHealth / StressLevel）には Describe が無いので、型ごとの説明文はここで作る。
/// </summary>
public static class EventDescriber
{
    private static readonly string[] StressSourceNames = { "子", "母", "父", "親の平均" };

    // ─── まとめ ──────────────────────────────────────────────────
    /// <summary>「メリット・抽選。3〜5歳。重み 1・クールダウン 1年。選択肢2つ：…」</summary>
    public static string Summary(EventData e)
    {
        if (e == null) return "";
        var sb = new StringBuilder();
        sb.Append(CategoryName(e)).Append('・').Append(TriggerName(e)).Append("。");

        if (e.trigger == EventTrigger.Fixed)
        {
            sb.Append(TimingText(e)).Append("。");
        }
        else
        {
            sb.Append(AgeRangeText(e)).Append("。");
            if (IsOnceInLifetime(e)) sb.Append("一生に1回。");
            sb.Append("重み ").Append(e.weight.ToString("0.##"))
              .Append("・クールダウン ").Append(CooldownText(e.cooldownDays)).Append("。");
        }

        string others = OtherConditionsText(e);
        if (others.Length > 0) sb.Append("ほかの条件：").Append(others).Append("。");

        int n = e.choices == null ? 0 : e.choices.Count;
        sb.Append("選択肢").Append(n).Append("つ");
        if (n > 0)
        {
            sb.Append("：");
            for (int i = 0; i < n; i++)
            {
                if (i > 0) sb.Append("／");
                sb.Append(ChoiceShort(e.choices[i]));
            }
        }
        return sb.ToString();
    }

    private static string ChoiceShort(EventChoice c)
    {
        if (c == null) return "（空）";
        string label = string.IsNullOrEmpty(c.label) ? "（ラベルなし）" : StoryTextFormatter.Format(c.label);
        if (c.effects == null || c.effects.Count == 0) return label;

        var parts = new List<string>();
        foreach (var ef in c.effects)
        {
            if (ef != null) parts.Add(EffectShort(ef));
        }
        return parts.Count == 0 ? label : label + "（" + string.Join("・", parts) + "）";
    }

    // ─── 名前 ────────────────────────────────────────────────────
    public static string CategoryName(EventData e) => e.category == EventCategory.Merit ? "メリット" : "デメリット";

    public static string TriggerName(EventData e)
    {
        switch (e.trigger)
        {
            case EventTrigger.Fixed: return "決まった日";
            case EventTrigger.FollowUp: return "続き";
            case EventTrigger.OnCondition: return "条件";
            case EventTrigger.Manual: return "コードから";
            default: return "抽選";
        }
    }

    // ─── 出方 ────────────────────────────────────────────────────
    /// <summary>決まった日の文章。「6歳4月1日に1回」「毎年12月25日（0歳〜5歳）」。</summary>
    public static string TimingText(EventData e)
    {
        if (e.fixedMinAgeYears == e.fixedMaxAgeYears)
            return $"{e.fixedMinAgeYears}歳{e.fixedMonth}月{e.fixedDay}日に1回";
        return $"毎年{e.fixedMonth}月{e.fixedDay}日（{e.fixedMinAgeYears}歳〜{e.fixedMaxAgeYears}歳）";
    }

    /// <summary>クールダウン（日）を「なし」「半年」「1年」「N日」にする。</summary>
    public static string CooldownText(int days)
    {
        if (days <= 0) return "なし";
        if (days == 182) return "半年";
        if (days % 365 == 0) return (days / 365) + "年";
        return days + "日";
    }

    // ─── 年齢 ────────────────────────────────────────────────────
    /// <summary>条件のリストにある最初の AgeRangeCondition / DateRangeCondition 等の範囲。無ければ false。</summary>
    public static bool TryGetAgeRange(EventData e, out int lo, out int hi)
    {
        lo = 0; hi = 0;
        if (e == null || e.conditions == null) return false;
        foreach (var c in e.conditions)
        {
            if (c is AgeRangeCondition a)
            {
                lo = Math.Min(a.minAgeYears, a.maxAgeYears);
                hi = Math.Max(a.minAgeYears, a.maxAgeYears);
                return true;
            }
            if (c is DateRangeCondition d)
            {
                lo = Math.Min(d.fromDay.year, d.toDay.year);
                hi = Math.Max(d.fromDay.year, d.toDay.year);
                return true;
            }
            if (c is SchoolGradeCondition g)
            {
                lo = GradeToApproxAge(Math.Min((int)g.minGrade, (int)g.maxGrade));
                hi = GradeToApproxAge(Math.Max((int)g.minGrade, (int)g.maxGrade));
                return true;
            }
        }
        return false;
    }

    private static int GradeToApproxAge(int gradeIdx)
    {
        if (gradeIdx <= (int)SchoolGrade.未就園) return 0;
        if (gradeIdx <= (int)SchoolGrade.年長) return gradeIdx + 2; // 年少=3, 年中=4, 年長=5
        if (gradeIdx <= (int)SchoolGrade.小6) return gradeIdx + 2; // 小1=6 〜 小6=11
        if (gradeIdx <= (int)SchoolGrade.中3) return gradeIdx + 2; // 中1=12 〜 中3=14
        if (gradeIdx <= (int)SchoolGrade.高3) return gradeIdx + 2; // 高1=15 〜 高3=17
        return 18;
    }

    public static string AgeRangeText(EventData e)
    {
        return TryGetAgeRange(e, out int lo, out int hi) ? RangeText(lo, hi) + "歳" : "年齢の指定なし";
    }

    /// <summary>並べ替え用の年齢。決まった日はその年齢、抽選は AgeRangeCondition の最小。無ければ 0。</summary>
    public static int MinAgeOf(EventData e)
    {
        if (e.trigger == EventTrigger.Fixed) return Math.Min(e.fixedMinAgeYears, e.fixedMaxAgeYears);
        return TryGetAgeRange(e, out int lo, out _) ? lo : 0;
    }

    private static string RangeText(int lo, int hi) => lo == hi ? lo.ToString() : lo + "〜" + hi;

    /// <summary>このイベントに「一生に1回だけ（起きていない）」条件が付いているか。</summary>
    public static bool IsOnceInLifetime(EventData e)
    {
        if (e == null || e.conditions == null) return false;
        foreach (var c in e.conditions)
        {
            if (c is ChoiceHistoryCondition h && h.targetEvent == e && h.checkType == HistoryCheckType.起きていない)
                return true;
        }
        return false;
    }

    private static string OtherConditionsText(EventData e)
    {
        if (e.conditions == null) return "";
        var parts = new List<string>();
        foreach (var c in e.conditions)
        {
            if (c == null || c is AgeRangeCondition) continue;
            if (c is ChoiceHistoryCondition h && h.targetEvent == e && h.checkType == HistoryCheckType.起きていない) continue;
            parts.Add(ConditionText(c));
        }
        return string.Join("、", parts);
    }

    // ─── 条件 ────────────────────────────────────────────────────
    /// <summary>条件1つの説明文。IDescribable を持つ部品ならそれを使う。</summary>
    public static string ConditionText(EventConditionSO c)
    {
        if (c == null) return "（空）";
        if (c is IDescribable d) return d.Describe();

        switch (c)
        {
            case AgeRangeCondition a:
                return RangeText(Math.Min(a.minAgeYears, a.maxAgeYears), Math.Max(a.minAgeYears, a.maxAgeYears)) + "歳";
            case ChildHealthCondition h:
                return $"体調 {Math.Min(h.minHealth, h.maxHealth)}〜{Math.Max(h.minHealth, h.maxHealth)}";
            case StressLevelCondition s:
            {
                int lo = Math.Min((int)s.minLevel, (int)s.maxLevel);
                int hi = Math.Max((int)s.minLevel, (int)s.maxLevel);
                string who = (int)s.source >= 0 && (int)s.source < StressSourceNames.Length ? StressSourceNames[(int)s.source] : s.source.ToString();
                string range = lo == hi ? ((StressLevel)lo).ToString() : ((StressLevel)lo) + "〜" + ((StressLevel)hi);
                return $"{who}のストレスが{range}";
            }
            default:
                return c.GetType().Name;
        }
    }

    /// <summary>条件の種類の名前（リストの左に出す）。</summary>
    public static string ConditionTypeName(Type t)
    {
        if (t == typeof(AgeRangeCondition)) return "年齢";
        if (t == typeof(ChildHealthCondition)) return "子の体調";
        if (t == typeof(StressLevelCondition)) return "ストレス段階";
        if (t == typeof(DateRangeCondition)) return "日付の範囲";
        if (t == typeof(MonthCondition)) return "月";
        if (t == typeof(SchoolGradeCondition)) return "学年";
        if (t == typeof(ChildStatCondition)) return "子どもの値";
        if (t == typeof(MoneyCondition)) return "所持金";
        if (t == typeof(SupportCondition)) return "支援制度";
        if (t == typeof(TraitCondition)) return "特性・状態";
        if (t == typeof(RequirementCondition)) return "支援条件の流用";
        if (t == typeof(ChoiceHistoryCondition)) return "前の選択・履歴";
        if (t == typeof(StoryFlagCondition)) return "フラグ";
        return t.Name;
    }

    /// <summary>サブアセットの名前に使う短い名前（AgeRangeCondition → AgeRange）。</summary>
    public static string ConditionShortName(Type t)
    {
        string n = t.Name;
        if (n.EndsWith("SO")) n = n.Substring(0, n.Length - 2);
        if (n.EndsWith("Condition") && n.Length > "Condition".Length) n = n.Substring(0, n.Length - "Condition".Length);
        return n;
    }

    // ─── 効果・チップ ────────────────────────────────────────────
    /// <summary>EffectKind の日本語名。足りない種類は enum の名前を出す。</summary>
    public static string KindName(EffectKind kind)
    {
        int i = (int)kind;
        return i >= 0 && i < SupportEditorGUI.EventKindNames.Length ? SupportEditorGUI.EventKindNames[i] : kind.ToString();
    }

    /// <summary>まとめ用の短い効果の文字。「−8,000円」「体調+15」「子ストレス−5」。</summary>
    public static string EffectShort(Effect ef)
    {
        if (ef == null) return "（空）";
        if (ef.kind == EffectKind.Money) return ef.value.ToString("+#,0;−#,0;0") + "円";
        if (ef.kind == EffectKind.SetFlag)
        {
            var f = ef.asset as StoryFlag;
            return f != null ? $"フラグ「{f.displayName}」を立てる" : "フラグを立てる";
        }
        if (ef.kind == EffectKind.ClearFlag)
        {
            var f = ef.asset as StoryFlag;
            return f != null ? $"フラグ「{f.displayName}」を消す" : "フラグを消す";
        }
        if (ef.kind == EffectKind.SetHobby)
        {
            var h = (HobbyKind)UnityEngine.Mathf.RoundToInt(ef.value);
            return $"趣味：{HobbyDisplayName.Get(h)}";
        }
        if (ef.kind == EffectKind.AddTrait)
        {
            var t = ef.asset as TraitDefinition;
            return t != null ? $"特性「{t.displayName}」" : "特性を付ける";
        }
        if (ef.kind == EffectKind.ScheduleEvent)
        {
            var ev = ef.asset as EventData;
            return ev != null ? $"{ef.days}日後に「{ev.title}」" : $"{ef.days}日後に続き";
        }
        if (ef.kind == EffectKind.ShowStory)
        {
            var tut = ef.asset as TutorialData;
            return tut != null ? $"ストーリー「{tut.name}」" : "ストーリーを出す";
        }
        return ChipTarget(ef) + KindName(ef.kind) + ef.value.ToString("+0.##;−0.##;0");
    }

    /// <summary>EffectChipUI が出す文字と同じ規則（「体調 +15」「子ストレス -5」）。</summary>
    public static string ChipText(Effect ef)
    {
        if (ef == null) return "";
        if (ef.kind == EffectKind.SetHobby)
        {
            var h = (HobbyKind)UnityEngine.Mathf.RoundToInt(ef.value);
            return $"趣味：{HobbyDisplayName.Get(h)}";
        }
        if (ef.kind == EffectKind.AddTrait)
        {
            var t = ef.asset as TraitDefinition;
            return $"特性：{(t != null ? t.displayName : "特性")}";
        }
        string sign = ef.value > 0 ? "+" : "";
        return $"{ChipTarget(ef)}{ChipKind(ef.kind)} {sign}{ef.value:0}";
    }

    // EffectChipUI.GetTargetName と同じ規則（あちらは private なのでここに写している。変えたら両方直す）
    private static string ChipTarget(Effect ef)
    {
        if (ef.kind == EffectKind.Health) return "";
        switch (ef.target)
        {
            case EffectTarget.Mother: return "母";
            case EffectTarget.Father: return "父";
            case EffectTarget.Parents: return "親";
            case EffectTarget.Child: return "子";
            case EffectTarget.Everyone: return "家族";
            default: return "";
        }
    }

    // EffectChipUI.GetKindName と同じ規則
    private static string ChipKind(EffectKind kind)
    {
        switch (kind)
        {
            case EffectKind.Money: return "お金";
            case EffectKind.Stress: return "ストレス";
            case EffectKind.Health: return "体調";
            case EffectKind.Grades: return "成績";
            case EffectKind.Hobby: return "趣味";
            default: return kind.ToString();
        }
    }

    /// <summary>選択肢のカードに出るチップの文字（お金やストーリー等は除く）。無ければ空の配列。</summary>
    public static List<string> ChipTexts(EventChoice c)
    {
        var list = new List<string>();
        if (c == null || c.effects == null) return list;
        foreach (var ef in c.effects)
        {
            if (ef == null) continue;
            if (ef.kind == EffectKind.Money ||
                ef.kind == EffectKind.SetFlag ||
                ef.kind == EffectKind.ClearFlag ||
                ef.kind == EffectKind.ScheduleEvent ||
                ef.kind == EffectKind.ShowStory)
            {
                continue;
            }
            list.Add(ChipText(ef));
        }
        return list;
    }

    /// <summary>お金の効果があるとき、ボタンの文字に付く部分を含めた全文（EventChoiceButtonUI と同じ規則）。無ければ null。</summary>
    public static string MoneyButtonText(EventChoice c)
    {
        if (c == null || c.effects == null) return null;
        var money = c.effects.Find(e => e != null && e.kind == EffectKind.Money);
        if (money == null) return null;

        string label = c.label ?? "";
        if (label.Contains("円")) return label;
        return label + (c.insuredMedicalExpense && money.value < 0f
            ? $" (医療費 {Math.Abs(money.value):#,0}円・助成適用前)"
            : $" ({money.value:+#,0;-#,0;0}円)");
    }
}

