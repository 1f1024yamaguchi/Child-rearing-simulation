using System.Collections.Generic;
using Maruyama.Events;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ストーリーウィンドウの「再生中の状態」タブ（story-tool.md 2.4）。
/// 立っているフラグの付け外し・選んだ履歴・予約中の続き・将来の夢を見る／変える。
/// 変更は必ず StoryState / DreamSystem の窓口を通す。
/// </summary>
public class StoryPlayModeTab
{
    private Vector2 scroll;
    private string flagSearch = "";

    public void Draw()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("再生中だけ使えます。本編シーンを再生すると、フラグ・選んだ履歴・予約中の続き・将来の夢を見たり変えたりできます。", MessageType.Info);
            return;
        }
        var state = StoryState.I;
        if (state == null)
        {
            EditorGUILayout.HelpBox("StoryState が見つかりません（本編シーンではない？）。", MessageType.Warning);
            return;
        }

        EditorGUILayout.HelpBox("ここで変えたフラグ・夢はセーブに残ります。効果（お金など）は戻りません。", MessageType.None);
        int today = GameState.I != null ? Mathf.FloorToInt(GameState.I.AgeDays) : 0;
        EditorGUILayout.LabelField("今日", GameDate.Format(today));

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawDream();
        EditorGUILayout.Space(6);
        DrawFlags(state);
        EditorGUILayout.Space(6);
        DrawScheduled(state, today);
        EditorGUILayout.Space(6);
        DrawChoices(state);
        EditorGUILayout.EndScrollView();
    }

    private static void DrawDream()
    {
        EditorGUILayout.LabelField("将来の夢", EditorStyles.boldLabel);
        var ds = DreamSystem.I;
        if (ds == null) { EditorGUILayout.LabelField("　DreamSystem がありません", EditorStyles.miniLabel); return; }

        string now = ds.HasDream ? $"{ds.Dream.DisplayNameOrName}（{GameDate.Format(ds.DecidedAgeDay)} に決定）" : $"まだ（{GameDate.Format(ds.DecideAgeDays)} に決まる）";
        EditorGUILayout.LabelField("　いまの夢", now);

        var careers = new List<CareerDefinition>(ds.AllCareers);
        var names = new string[careers.Count + 1];
        names[0] = "（まだ決まっていない）";
        int cur = 0;
        for (int i = 0; i < careers.Count; i++)
        {
            names[i + 1] = careers[i].DisplayNameOrName;
            if (careers[i] == ds.Dream) cur = i + 1;
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            int pick = EditorGUILayout.Popup("　夢を変える", cur, names);
            if (pick != cur) ds.SetDream(pick == 0 ? null : careers[pick - 1]);
            if (GUILayout.Button("今のステータスで決め直す", EditorStyles.miniButton, GUILayout.Width(150))) ds.DecideDreamNow();
        }
        var child = Household.I != null ? Household.I.Child : null;
        EditorGUILayout.LabelField(CareerJudge.Explain(careers, CareerJudge.Input.From(child, GameContext.Current())), EditorStyles.wordWrappedMiniLabel);
    }

    private void DrawFlags(StoryState state)
    {
        EditorGUILayout.LabelField($"フラグ（立っている {state.ActiveFlagOrder.Count}）", EditorStyles.boldLabel);
        flagSearch = EditorGUILayout.TextField("　検索", flagSearch);
        var flags = EventRegistry.AllFlags();
        if (flags.Count == 0) EditorGUILayout.LabelField("　フラグのアセットがありません", EditorStyles.miniLabel);
        foreach (var f in flags)
        {
            if (f == null) continue;
            string n = (string.IsNullOrEmpty(f.displayName) ? f.name : f.displayName) + $"（{f.id}）";
            if (!string.IsNullOrEmpty(flagSearch) && !n.Contains(flagSearch)) continue;
            bool on = state.HasFlag(f);
            int day = state.GetFlagSetDay(f.id);
            string label = on && day >= 0 ? $"{n}　{GameDate.Format(day)} に立った" : n;
            bool next = EditorGUILayout.ToggleLeft("　" + label, on);
            if (next != on)
            {
                if (next) state.SetFlag(f); else state.ClearFlag(f);
            }
        }
        // アセットの無い id（消したフラグなど）
        foreach (var id in new List<string>(state.ActiveFlagOrder))
        {
            if (flags.Exists(f => f != null && f.id == id)) continue;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"　⚠ {id}（アセットが無い）");
                if (GUILayout.Button("消す", EditorStyles.miniButton, GUILayout.Width(50))) state.ClearFlag(id);
            }
        }
    }

    private static void DrawScheduled(StoryState state, int today)
    {
        var list = new List<StoryState.ScheduledEventEntry>(state.ScheduledEvents);
        EditorGUILayout.LabelField($"予約中の続き（{list.Count}）", EditorStyles.boldLabel);
        if (list.Count == 0) { EditorGUILayout.LabelField("　なし", EditorStyles.miniLabel); return; }
        foreach (var s in list)
        {
            var e = EventRegistry.All().Find(x => x != null && x.id == s.eventId);
            int remain = s.scheduledAgeDay - today;
            string when = remain > 0 ? $"あと{remain}日" : "出番待ち（選ばれたら消える）";
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"　{(e != null ? e.title : s.eventId)}　{GameDate.Format(s.scheduledAgeDay)}（{when}）");
                using (new EditorGUI.DisabledScope(e == null || EventManager.Instance == null))
                {
                    if (GUILayout.Button("今すぐ出す", EditorStyles.miniButton, GUILayout.Width(70)))
                    {
                        state.RemoveScheduled(s.eventId);
                        EventManager.Instance.Enqueue(e);
                    }
                }
                if (GUILayout.Button("予約を消す", EditorStyles.miniButton, GUILayout.Width(70))) state.RemoveScheduled(s.eventId);
            }
        }
    }

    private static void DrawChoices(StoryState state)
    {
        var data = state.CaptureSaveData();
        int n = data.choices != null ? data.choices.Count : 0;
        EditorGUILayout.LabelField($"選んだ選択肢（{n}）", EditorStyles.boldLabel);
        if (n == 0) { EditorGUILayout.LabelField("　なし", EditorStyles.miniLabel); return; }
        data.choices.Sort((a, b) => a.ageDay.CompareTo(b.ageDay));
        foreach (var c in data.choices)
        {
            var e = EventRegistry.All().Find(x => x != null && x.id == c.eventId);
            string label = c.choiceId;
            if (e != null && e.choices != null)
            {
                var ch = e.choices.Find(x => x != null && x.id == c.choiceId);
                if (ch != null) label = StoryTextFormatter.Format(ch.label);
            }
            EditorGUILayout.LabelField($"　{GameDate.Format(c.ageDay)}　{(e != null ? e.title : c.eventId)} → {label}", EditorStyles.miniLabel);
        }
    }
}
