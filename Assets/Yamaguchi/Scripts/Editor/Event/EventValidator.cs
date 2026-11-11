using System.Collections.Generic;
using Maruyama.Events;

/// <summary>
/// イベントのチェック（✕・⚠）。リストを返すだけで、表示はウィンドウ側がする。
/// 保存はできるが目立たせるためのもの。
/// </summary>
public static class EventValidator
{
    /// <summary>選択肢が4つのとき、ラベルがこの文字数を超えたら ⚠（たたき台）。</summary>
    public const int LongLabelLength = 10;

    /// <summary>選択肢の数の上限（これを超えたら ✕）。</summary>
    public const int MaxChoices = 4;

    public struct Issue
    {
        public bool isError;     // true = ✕ / false = ⚠
        public string message;

        public Issue(bool isError, string message)
        {
            this.isError = isError;
            this.message = message;
        }
    }

    public static List<Issue> Validate(EventData e)
    {
        var list = new List<Issue>();
        if (e == null) return list;

        // ─── ✕ ───
        if (string.IsNullOrEmpty(e.id)) list.Add(Error("ID が空です"));
        else
        {
            if (!EventRegistry.IsValidIdChars(e.id)) list.Add(Error("ID に使えるのは英数字と _ だけです"));
            if (EventRegistry.IdCount(e.id, e) > 0) list.Add(Error($"ID「{e.id}」がほかのイベントと重なっています"));
        }

        if (string.IsNullOrWhiteSpace(e.title)) list.Add(Error("タイトルが空です"));

        int n = e.choices == null ? 0 : e.choices.Count;
        if (n == 0) list.Add(Error("選択肢が0個です"));
        else if (n > MaxChoices) list.Add(Error($"選択肢が{n}個です（{MaxChoices}個までです）"));

        var seenChoiceIds = new HashSet<string>();
        for (int i = 0; i < n; i++)
        {
            var c = e.choices[i];
            if (c == null) continue;
            if (string.IsNullOrWhiteSpace(c.label)) list.Add(Error($"選択肢{i + 1}のラベルが空です"));

            if (string.IsNullOrWhiteSpace(c.id))
            {
                list.Add(Error($"選択肢{i + 1}の ID が空です"));
            }
            else if (!seenChoiceIds.Add(c.id))
            {
                list.Add(Error($"選択肢{i + 1}の ID「{c.id}」が重複しています"));
            }
        }

        if (e.trigger == EventTrigger.Fixed)
        {
            bool monthOk = e.fixedMonth >= 1 && e.fixedMonth <= 12;
            if (!monthOk || e.fixedDay < 1 || e.fixedDay > GameDate.DaysInMonth(e.fixedMonth))
                list.Add(Error($"決まった日の{e.fixedMonth}月{e.fixedDay}日は暦にありません"));
        }

        if (e.conditions != null)
        {
            for (int i = 0; i < e.conditions.Count; i++)
            {
                var cond = e.conditions[i];
                if (cond == null)
                {
                    list.Add(Error($"条件{i + 1}行目が空（None）です。アセットを入れるか ✕ で消してください"));
                    continue;
                }

                if (cond is DateRangeCondition d)
                {
                    if (d.fromDay.ToAgeDays() > d.toDay.ToAgeDays())
                        list.Add(Error($"条件「{cond.name}」：開始日（{d.fromDay.year}歳{d.fromDay.month}月{d.fromDay.day}日）が終了日より後です"));
                }
                else if (cond is ChoiceHistoryCondition chc)
                {
                    if (chc.targetEvent == null)
                    {
                        list.Add(Error($"条件「{cond.name}」：参照しているイベントが消えているか空（None）です"));
                    }
                    else if (chc.checkType == HistoryCheckType.選んだ || chc.checkType == HistoryCheckType.選ばなかった)
                    {
                        if (string.IsNullOrEmpty(chc.choiceId))
                        {
                            list.Add(Error($"条件「{cond.name}」：判定対象の選択肢IDが空です"));
                        }
                        else
                        {
                            bool foundChoice = false;
                            if (chc.targetEvent.choices != null)
                            {
                                for (int ci = 0; ci < chc.targetEvent.choices.Count; ci++)
                                {
                                    if (chc.targetEvent.choices[ci]?.GetOrFallbackId(ci) == chc.choiceId)
                                    {
                                        foundChoice = true;
                                        break;
                                    }
                                }
                            }
                            if (!foundChoice)
                            {
                                list.Add(Error($"条件「{cond.name}」：イベント「{chc.targetEvent.title}」に選択肢ID「{chc.choiceId}」が存在しません"));
                            }
                        }
                    }
                }
                else if (cond is StoryFlagCondition sfc)
                {
                    if (sfc.flag == null)
                    {
                        list.Add(Error($"条件「{cond.name}」：参照しているフラグが消えているか空（None）です"));
                    }
                }
            }
        }

        // 選択肢の効果の参照チェック
        if (e.choices != null)
        {
            for (int i = 0; i < e.choices.Count; i++)
            {
                var c = e.choices[i];
                if (c == null || c.effects == null) continue;
                for (int ei = 0; ei < c.effects.Count; ei++)
                {
                    var ef = c.effects[ei];
                    if (ef == null) continue;

                    if (ef.kind == EffectKind.ScheduleEvent)
                    {
                        if (ef.asset == null)
                            list.Add(Error($"選択肢{i + 1}の効果「続きを予約」で参照しているイベントが空（None）です"));
                        else if (!(ef.asset is EventData))
                            list.Add(Error($"選択肢{i + 1}の効果「続きを予約」で参照しているアセットの型が EventData ではありません"));
                    }
                    else if (ef.kind == EffectKind.SetFlag || ef.kind == EffectKind.ClearFlag)
                    {
                        if (ef.asset == null)
                            list.Add(Error($"選択肢{i + 1}の効果「フラグ」で参照しているフラグが空（None）です"));
                        else if (!(ef.asset is StoryFlag))
                            list.Add(Error($"選択肢{i + 1}の効果「フラグ」で参照しているアセットの型が StoryFlag ではありません"));
                    }
                    else if (ef.kind == EffectKind.AddTrait)
                    {
                        if (ef.asset == null)
                            list.Add(Error($"選択肢{i + 1}の効果「特性を付ける」で参照している特性が空（None）です"));
                        else if (!(ef.asset is TraitDefinition))
                            list.Add(Error($"選択肢{i + 1}の効果「特性を付ける」で参照しているアセットの型が TraitDefinition ではありません"));
                    }
                    else if (ef.kind == EffectKind.ShowStory)
                    {
                        if (ef.asset == null)
                            list.Add(Error($"選択肢{i + 1}の効果「ストーリーを出す」で参照しているチュートリアルが空（None）です"));
                        else if (!(ef.asset is TutorialData))
                            list.Add(Error($"選択肢{i + 1}の効果「ストーリーを出す」で参照しているアセットの型が TutorialData ではありません"));
                    }
                }
            }
        }

        // ─── ⚠ ───
        if (e.trigger == EventTrigger.Random && e.weight <= 0f) list.Add(Warning("抽選なのに重みが0です（選ばれません）"));

        if (e.trigger == EventTrigger.FollowUp)
        {
            var callers = EventRegistry.FindCallersOf(e);
            if (callers.Count == 0)
                list.Add(Warning("「続き」出方ですが、どこからも予約されていません（出ません）"));
        }

        // フラグを見ている条件があるのに、立てる場所がどこにも無い
        if (e.conditions != null)
        {
            foreach (var cond in e.conditions)
            {
                if (cond is StoryFlagCondition sfc && sfc.flag != null)
                {
                    var usage = EventRegistry.FindFlagUsers(sfc.flag);
                    if (usage.setters.Count == 0)
                    {
                        list.Add(Warning($"条件で見ているフラグ「{sfc.flag.displayName}」を立てる場所がどこにもありません"));
                    }
                }
            }
        }

        for (int i = 0; i < n; i++)
        {
            var c = e.choices[i];
            if (c == null) continue;
            if (c.effects == null || c.effects.Count == 0)
                list.Add(Warning($"選択肢{i + 1}は効果が0個です（「何もしない」でよいなら無視してください）"));
            if (n == MaxChoices && c.label != null && c.label.Length > LongLabelLength)
                list.Add(Warning($"選択肢{i + 1}のラベルが{LongLabelLength}文字より長いです（4つのときは 2×2 で入りきらないかもしれません）"));
        }

        // 全部の選択肢に出す条件が付いている場合
        if (n > 0)
        {
            bool allHaveConditions = true;
            for (int i = 0; i < n; i++)
            {
                var c = e.choices[i];
                if (c == null || c.showConditions == null || c.showConditions.Count == 0)
                {
                    allHaveConditions = false;
                    break;
                }
            }
            if (allHaveConditions)
            {
                list.Add(Warning("すべての選択肢に出す条件が付いています（全部押せない日がありえます）"));
            }
        }

        if (!EventRegistry.IsInResources(e)) list.Add(Warning("Resources/Events の外にあります（EventManager が読み込みません）"));
        if (e.iconSprite == null)
        {
            if (string.IsNullOrWhiteSpace(e.imageNote))
                list.Add(Warning("画像がありません（絵の指示メモも空です）"));
        }

        return list;
    }

    /// <summary>✕ と ⚠ の数。</summary>
    public static void Count(EventData e, out int errors, out int warnings)
    {
        errors = 0; warnings = 0;
        foreach (var issue in Validate(e))
        {
            if (issue.isError) errors++; else warnings++;
        }
    }

    private static Issue Error(string m) => new Issue(true, m);
    private static Issue Warning(string m) => new Issue(false, m);
}

