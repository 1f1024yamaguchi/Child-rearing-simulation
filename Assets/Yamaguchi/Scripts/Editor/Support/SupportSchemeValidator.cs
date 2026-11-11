using System.Collections.Generic;
using System.Text.RegularExpressions;
using Maruyama.Core;

/// <summary>制度のチェック。✕＝直さないと困るもの、⚠＝たぶん意図と違うもの。</summary>
public static class SupportSchemeValidator
{
    public class Issue
    {
        public bool error;
        public string text;
    }

    private static readonly Regex IdPattern = new Regex("^[a-z0-9_]+$");

    public static List<Issue> Check(SupportScheme s)
    {
        var list = new List<Issue>();
        if (s == null) return list;

        // ─── ✕ ───
        if (string.IsNullOrEmpty(s.id))
        {
            Error(list, "id が空です（セーブに使うので必須）");
        }
        else
        {
            if (!IdPattern.IsMatch(s.id))
                Error(list, $"id には英小文字・数字・_ だけ使えます：{s.id}");
            foreach (var other in SupportRegistry.AllSchemes())
            {
                if (other != null && other != s && other.id == s.id)
                    Error(list, $"id「{s.id}」が「{other.name}」と重複しています");
            }
        }
        if (string.IsNullOrWhiteSpace(s.title)) Error(list, "title（名前）が空です");

        if (s.monthlyTiers != null)
        {
            for (int i = 0; i < s.monthlyTiers.Count; i++)
            {
                var t = s.monthlyTiers[i];
                if (t != null && t.fromAge.ToAgeDays() > t.toAge.ToAgeDays())
                    Error(list, $"支給表の段{i + 1}：「何歳から」が「何歳まで」より後です");
            }
        }
        if (s.covers != null)
        {
            for (int i = 0; i < s.covers.Count; i++)
            {
                var c = s.covers[i];
                if (c != null && c.fromAge.ToAgeDays() > c.toAge.ToAgeDays())
                    Error(list, $"支出の肩代わり{i + 1}：「何歳から」が「何歳まで」より後です");
            }
        }
        if (s.medicalExpenseCovers != null)
        {
            for (int i = 0; i < s.medicalExpenseCovers.Count; i++)
            {
                var c = s.medicalExpenseCovers[i];
                if (c != null && c.fromAge.ToAgeDays() > c.toAge.ToAgeDays())
                    Error(list, $"医療費の助成{i + 1}：「何歳から」が「何歳まで」より後です");
            }
        }
        if (s.deadlineType == DeadlineType.Once && s.deadline.ToAgeDays() < s.start.ToAgeDays())
            Error(list, "期限の日が、申請できる日（start）より前です");

        // ─── ⚠ ───
        bool noTiers = s.monthlyTiers == null || s.monthlyTiers.Count == 0;
        bool noCovers = s.covers == null || s.covers.Count == 0;
        bool noMedical = s.medicalExpenseCovers == null || s.medicalExpenseCovers.Count == 0;
        bool noWage = s.wageLinkedTiers == null || s.wageLinkedTiers.Count == 0;

        if (s.effectTiming == EffectTiming.Monthly && noTiers && noCovers && noMedical && noWage)
            Warn(list, "「受給中ずっと（Monthly）」ですが、支給表・肩代わり・医療費の助成・給与連動がすべて空です（何も起きません）");
        if (s.effectTiming == EffectTiming.OnApply && !noTiers)
            Warn(list, "「申請したとき1回（OnApply）」なのに毎月の支給表があります（支給表は使われません）");

        if (s.monthlyTiers != null)
        {
            for (int i = 0; i < s.monthlyTiers.Count; i++)
            {
                for (int j = i + 1; j < s.monthlyTiers.Count; j++)
                {
                    var a = s.monthlyTiers[i];
                    var b = s.monthlyTiers[j];
                    if (a == null || b == null) continue;
                    // この段だけの条件で使い分けている段は、期間が重なっていて当然なので数えない
                    if (HasAnyRequirement(a.requirements) || HasAnyRequirement(b.requirements)) continue;
                    int a1 = a.fromAge.ToAgeDays(), a2 = a.toAge.ToAgeDays();
                    int b1 = b.fromAge.ToAgeDays(), b2 = b.toAge.ToAgeDays();
                    if (a1 <= a2 && b1 <= b2 && a1 <= b2 && b1 <= a2)
                        Warn(list, $"支給表の段{i + 1}と段{j + 1}の期間が重なっています（条件がなければ上の段が先に使われます）");
                }
            }
        }

        if (HasNullRow(s.applyRequirements))
            Warn(list, "申請条件に空（None）の行があります");
        if (s.monthlyTiers != null)
        {
            for (int i = 0; i < s.monthlyTiers.Count; i++)
            {
                var t = s.monthlyTiers[i];
                if (t != null && HasNullRow(t.requirements))
                    Warn(list, $"支給表の段{i + 1}の条件に空（None）の行があります");
            }
        }
        if (s.covers != null)
        {
            for (int i = 0; i < s.covers.Count; i++)
            {
                if (s.covers[i] != null && s.covers[i].expense == null)
                    Warn(list, $"支出の肩代わり{i + 1}：肩代わりする支出（expense）が None です");
            }
        }
        if (s.icon == null) Warn(list, "アイコン（icon）が入っていません");

        if (s.effects != null)
        {
            for (int i = 0; i < s.effects.Count; i++)
            {
                var ef = s.effects[i];
                if (ef != null && (int)ef.kind >= SupportEditorGUI.SupportKindNames.Length)
                {
                    string kName = (int)ef.kind < SupportEditorGUI.EventKindNames.Length ? SupportEditorGUI.EventKindNames[(int)ef.kind] : ef.kind.ToString();
                    Warn(list, $"申請時の効果{i + 1}：この種類「{kName}」は支援制度では使えません（効果は起きません）");
                }
            }
        }
        if (s.monthlyTiers != null)
        {
            for (int i = 0; i < s.monthlyTiers.Count; i++)
            {
                var t = s.monthlyTiers[i];
                if (t != null && t.monthlyEffects != null)
                {
                    for (int j = 0; j < t.monthlyEffects.Count; j++)
                    {
                        var ef = t.monthlyEffects[j];
                        if (ef != null && (int)ef.kind >= SupportEditorGUI.SupportKindNames.Length)
                        {
                            string kName = (int)ef.kind < SupportEditorGUI.EventKindNames.Length ? SupportEditorGUI.EventKindNames[(int)ef.kind] : ef.kind.ToString();
                            Warn(list, $"支給表の段{i + 1}の効果{j + 1}：この種類「{kName}」は支援制度では使えません（効果は起きません）");
                        }
                    }
                }
            }
        }

        if (SupportRegistry.FindManager() != null && !SupportRegistry.IsRegistered(s))
            Warn(list, "SupportManager に登録されていません（サポート画面に出ません）");

        return list;
    }

    public static void Count(List<Issue> issues, out int errors, out int warnings)
    {
        errors = 0;
        warnings = 0;
        foreach (var i in issues)
        {
            if (i.error) errors++; else warnings++;
        }
    }

    private static void Error(List<Issue> l, string text) => l.Add(new Issue { error = true, text = text });
    private static void Warn(List<Issue> l, string text) => l.Add(new Issue { error = false, text = text });

    private static bool HasNullRow(List<RequirementSO> l)
    {
        if (l == null) return false;
        foreach (var r in l) if (r == null) return true;
        return false;
    }

    private static bool HasAnyRequirement(List<RequirementSO> l)
    {
        if (l == null) return false;
        foreach (var r in l) if (r != null) return true;
        return false;
    }
}
