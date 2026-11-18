using System.Collections.Generic;
using Maruyama.Events;
using Maruyama.Core;
using UnityEngine;

/// <summary>
/// 進路の判断・お知らせを、イベントのダイアログ（EventManager）で出す。専用の画面（ending.md 依頼6）ができるまでの代わり。
/// その場で EventData を作って順番待ちに入れる。選ばれた選択肢は PathSystem が OnEventResolved で受け取る。
/// 文章の {name} は子どもの名前になる。
/// </summary>
public static class PathDialogs
{
    public const string DecideId = "path_decide";
    public const string RetryId = "path_retry";
    public const string NoticeId = "path_notice";

    public static void ShowDecision(PathSystem ps)
    {
        var child = Household.I != null ? Household.I.Child : null;
        var academic = TermTestSystem.I != null ? TermTestSystem.I.Record : null;
        var wish = ps.Wish();
        var desc = new System.Text.StringBuilder();
        desc.AppendLine("{name}の進路を決める時期になった。");
        desc.AppendLine($"本人の希望：{(wish != null ? wish.DisplayNameOrName : "とくになし")}");

        var choices = new List<(string id, string label)>();
        foreach (var kind in new[] { PathKind.就職, PathKind.専門学校, PathKind.大学, PathKind.推薦 })
        {
            var p = ps.Find(kind);
            if (p == null) continue;
            if (kind == PathKind.推薦 && !AdmissionCalc.CanApplyRecommendation(child, academic, p, out string why))
            {
                desc.AppendLine($"{p.DisplayNameOrName}：選べない（{why}）");
                continue;
            }
            string label = p.DisplayNameOrName;
            if (kind != PathKind.就職 && child != null) label += $"（{AdmissionCalc.MockRank(AdmissionCalc.PassRate(child.Grade, p))}判定）";
            if (wish == p) label += "★希望";
            choices.Add((p.id, label));
        }
        Show(DecideId, "進路を決める", desc.ToString().TrimEnd(), EventCategory.Merit, choices);
    }

    public static void ShowRetry(PathSystem ps, PathDefinition failed)
    {
        string name = failed != null ? failed.DisplayNameOrName : "受験";
        var senmon = ps.Find(PathKind.専門学校);
        var choices = new List<(string id, string label)>
        {
            (RetryChoice.浪人.ToString(), "浪人してもう一度"),
            (RetryChoice.就職.ToString(), "就職する"),
        };
        if (senmon != null) choices.Add((RetryChoice.専門.ToString(), senmon.DisplayNameOrName + "を受ける"));
        Show(RetryId, $"{name}は不合格", "{name}は不合格だった。これからどうする？", EventCategory.Demerit, choices);
    }

    public static void ShowNotice(string title, string description, EventCategory category)
    {
        Show(NoticeId, title, description, category, new List<(string, string)> { ("ok", "わかった") });
    }

    private static void Show(string id, string title, string description, EventCategory category, List<(string id, string label)> choices)
    {
        if (EventManager.Instance == null)
        {
            Debug.LogWarning($"[PathDialogs] EventManager が無いので画面を出せません：{title}／{description}");
            return;
        }
        var e = ScriptableObject.CreateInstance<EventData>();
        e.name = id;
        e.id = id;
        e.title = title;
        e.description = description;
        e.category = category;
        e.trigger = EventTrigger.Manual;
        foreach (var c in choices)
            e.choices.Add(new EventChoice { id = c.id, label = c.label, effects = new List<Effect>() });
        EventManager.Instance.Enqueue(e);
    }
}
