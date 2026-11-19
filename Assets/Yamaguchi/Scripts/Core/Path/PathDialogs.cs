using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 進路の判断・お知らせを、専用パネル（PathPanelUI）で出す。イベントのダイアログは使わない（「[メリット]」の帯が出ない）。
/// 文章と画像は PathScreens（イベントツールで作る EventData）から取る。選択肢は進路のデータからここで作る。
/// 選ばれたら、カードに付けた処理（PathSystem の Decide / Retry）がその場で呼ばれる。
/// </summary>
public static class PathDialogs
{
    private static Dictionary<string, string> Tokens(PathDefinition p, string reason = "")
    {
        var ps = PathSystem.I;
        int ronin = ps != null ? ps.State.roninCount : 0;
        return new Dictionary<string, string>
        {
            { "path", p != null ? p.DisplayNameOrName : "" },
            { "ronin", ronin > 0 ? $"（浪人{ronin}年）" : "" },
            { "reason", reason ?? "" },
        };
    }

    /// <summary>お知らせ。ids は前から探す候補（例：path_pass_univ）。</summary>
    public static void Notice(PathDefinition p, string reason, params string[] ids)
    {
        PathScreens.Resolve(ids, Tokens(p, reason), out string title, out string desc, out Sprite image, out string button);
        PathPanelUI.ShowNotice(title, desc, image, button);
    }

    public static void ShowDecision(PathSystem ps)
    {
        var child = Household.I != null ? Household.I.Child : null;
        var academic = TermTestSystem.I != null ? TermTestSystem.I.Record : null;
        var wish = ps.Wish();

        var options = new List<PathPanelUI.Option>();
        foreach (var kind in new[] { PathKind.就職, PathKind.専門学校, PathKind.大学, PathKind.推薦 })
        {
            var p = ps.Find(kind);
            if (p == null) continue;
            var o = new PathPanelUI.Option { title = p.DisplayNameOrName, wish = wish == p };
            var target = p;
            o.onSelect = () => ps.Decide(target);

            switch (kind)
            {
                case PathKind.就職:
                    o.sub = "卒業後すぐ働く";
                    o.note = "高校を卒業したら巣立ち。";
                    break;
                case PathKind.専門学校:
                    o.sub = $"{p.years}年通う";
                    o.note = "2月の入試。落ちたら選び直し。";
                    break;
                case PathKind.大学:
                    o.sub = $"{p.years}年通う";
                    o.note = "2月の入試。落ちたら浪人などを選ぶ。";
                    break;
                case PathKind.推薦:
                    o.sub = $"{p.years}年通う";
                    if (!ps.RecommendationOpen) { o.enabled = false; o.note = "出願の時期（11月まで）を過ぎた。"; }
                    else if (!AdmissionCalc.CanApplyRecommendation(child, academic, p, out string why)) { o.enabled = false; o.note = why + "。"; }
                    else o.note = "11月に結果。落ちたら2月に一般入試。";
                    break;
            }
            if (kind != PathKind.就職 && child != null) o.rank = AdmissionCalc.MockRank(AdmissionCalc.PassRate(child.Grade, p));
            if (!o.enabled) o.rank = "";
            options.Add(o);
        }

        PathScreens.Resolve(new[] { "path_decide" }, Tokens(null), out string title, out string desc, out Sprite image, out _);
        PathPanelUI.Show(title, desc, options, image);
    }

    public static void ShowRetry(PathSystem ps, PathDefinition failed)
    {
        var child = Household.I != null ? Household.I.Child : null;
        var wish = ps.Wish();
        bool wishUniv = wish != null && (wish.kind == PathKind.大学 || wish.kind == PathKind.推薦);

        var options = new List<PathPanelUI.Option>
        {
            new PathPanelUI.Option
            {
                title = "浪人する", sub = "来年もう一度受ける", wish = wishUniv,
                note = "巣立ちが1年のびる。ストレスも少したまる。",
                onSelect = () => ps.Retry(RetryChoice.浪人),
            },
            new PathPanelUI.Option
            {
                title = "就職する", sub = "すぐ働く", wish = wish != null && wish.kind == PathKind.就職,
                note = "今年の春に巣立ち。",
                onSelect = () => ps.Retry(RetryChoice.就職),
            },
        };
        var senmon = ps.Find(PathKind.専門学校);
        if (senmon != null && senmon != failed)
        {
            var o = new PathPanelUI.Option
            {
                title = senmon.DisplayNameOrName, sub = $"{senmon.years}年通う", wish = wish == senmon,
                note = "すぐ入試を受ける。",
                onSelect = () => ps.Retry(RetryChoice.専門),
            };
            if (child != null) o.rank = AdmissionCalc.MockRank(AdmissionCalc.PassRate(child.Grade, senmon));
            options.Add(o);
        }
        PathScreens.Resolve(new[] { "path_retry" }, Tokens(failed), out string title, out string desc, out Sprite image, out _);
        PathPanelUI.Show(title, desc, options, image);
    }
}
