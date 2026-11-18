using System.Collections.Generic;
using Maruyama.Events;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ストーリーウィンドウの「ストーリーの文章」タブ（story-tool.md 2.3）。
/// ストーリーとして使っているチュートリアル（出すタイミングが「フラグが立った日」、選択肢の効果「ストーリーを出す」から呼ばれる、
/// ページの条件にフラグ・将来の夢を使う、のどれか）を一覧し、フラグと夢の組み合わせでどのページが出るかを試す（再生しなくてよい）。
/// ここでは見るだけ。編集はチュートリアルの編集ウィンドウ。
/// </summary>
public class StoryTextTab
{
    private class Entry
    {
        public TutorialData data;
        public List<string> reasons = new List<string>();
    }

    private List<Entry> entries;
    private int builtVersion = -1;
    private bool showAll;
    private TutorialData selected;
    private Vector2 listScroll, detailScroll;

    // 試す組み合わせ
    private readonly HashSet<StoryFlag> testFlags = new HashSet<StoryFlag>();
    private CareerDefinition testDream;
    private bool testHasDream;

    public void Draw(Rect windowRect)
    {
        if (entries == null || builtVersion != StoryIndex.Version) Build();

        using (new EditorGUILayout.HorizontalScope())
        {
            bool all = EditorGUILayout.ToggleLeft("ストーリー以外のチュートリアルも出す", showAll, GUILayout.Width(240));
            if (all != showAll) { showAll = all; Build(); }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("再読み込み", EditorStyles.miniButton, GUILayout.Width(70))) StoryIndex.Invalidate();
        }

        float listHeight = Mathf.Clamp(22f * Mathf.Max(entries.Count, 2) + 8f, 60f, windowRect.height * 0.35f);
        listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.Height(listHeight));
        if (entries.Count == 0)
            EditorGUILayout.LabelField("ストーリーとして使っているチュートリアルはまだありません（フラグが立った日に出す・選択肢から出す・ページの条件にフラグや夢を使う、のどれかで出ます）。", EditorStyles.wordWrappedMiniLabel);
        foreach (var e in entries)
        {
            int pages = e.data.PageCount, conditional = 0;
            foreach (var p in e.data.Pages) if (p != null && p.showConditions != null && p.showConditions.Count > 0) conditional++;
            string text = $"{e.data.name}　出るとき：{string.Join("／", e.reasons)}　ページ{pages}（条件付き{conditional}）";
            var style = new GUIStyle(EditorStyles.label);
            if (e.data == selected) style.fontStyle = FontStyle.Bold;
            if (GUILayout.Button(text, style, GUILayout.Height(20))) selected = e.data;
        }
        EditorGUILayout.EndScrollView();

        var r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.5f));

        detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
        if (selected != null) DrawDetail(selected);
        else EditorGUILayout.LabelField("上の一覧から選んでください。", EditorStyles.miniLabel);
        EditorGUILayout.EndScrollView();
    }

    private void Build()
    {
        builtVersion = StoryIndex.Version;
        entries = new List<Entry>();

        // 選択肢の「ストーリーを出す」から呼ばれているもの
        var callers = new Dictionary<TutorialData, List<string>>();
        foreach (var ev in EventRegistry.All())
        {
            if (ev == null || ev.choices == null) continue;
            foreach (var ch in ev.choices)
            {
                if (ch == null || ch.effects == null) continue;
                foreach (var ef in ch.effects)
                {
                    if (ef == null || ef.kind != EffectKind.ShowStory || !(ef.asset is TutorialData t)) continue;
                    if (!callers.TryGetValue(t, out var l)) callers[t] = l = new List<string>();
                    l.Add($"選択肢「{(string.IsNullOrEmpty(ev.title) ? ev.name : ev.title)} → {StoryTextFormatter.Format(ch.label)}」");
                }
            }
        }

        foreach (var t in StoryIndex.Tutorials())
        {
            if (t == null) continue;
            var e = new Entry { data = t };
            if (t.Timing != null && t.Timing.kind == TutorialTimingKind.FlagSet) e.reasons.Add(t.Timing.Describe());
            if (callers.TryGetValue(t, out var cl)) e.reasons.AddRange(cl);
            bool storyPage = false;
            foreach (var p in t.Pages)
                if (p != null && p.showConditions != null)
                    foreach (var c in p.showConditions)
                        if (c is StoryFlagCondition || c is DreamCondition) storyPage = true;
            if (storyPage) e.reasons.Add("ページの条件にフラグ・夢");

            if (e.reasons.Count == 0)
            {
                if (!showAll) continue;
                e.reasons.Add(t.Timing != null ? t.Timing.Describe() : "—");
            }
            entries.Add(e);
        }
        entries.Sort((a, b) => string.CompareOrdinal(a.data.name, b.data.name));
        if (selected != null && !entries.Exists(x => x.data == selected)) selected = null;
    }

    private void DrawDetail(TutorialData t)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(t.name, EditorStyles.boldLabel);
            if (GUILayout.Button("チュートリアルの編集ウィンドウで開く", GUILayout.Width(230))) TutorialEditorWindow.OpenWith(t);
        }

        // 試す組み合わせ：このチュートリアルで使っているフラグと夢だけ出す
        var flags = new List<StoryFlag>();
        bool usesDream = false;
        var careers = new List<CareerDefinition>();
        foreach (var p in t.Pages)
        {
            if (p == null || p.showConditions == null) continue;
            foreach (var c in p.showConditions)
            {
                if (c is StoryFlagCondition sfc && sfc.flag != null && !flags.Contains(sfc.flag)) flags.Add(sfc.flag);
                if (c is DreamCondition dc) { usesDream = true; if (dc.career != null && !careers.Contains(dc.career)) careers.Add(dc.career); }
            }
        }

        if (flags.Count > 0 || usesDream)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("この組み合わせで試す（再生しなくてよい）", EditorStyles.miniBoldLabel);
                foreach (var f in flags)
                {
                    bool on = testFlags.Contains(f);
                    bool next = EditorGUILayout.ToggleLeft($"フラグ「{(string.IsNullOrEmpty(f.displayName) ? f.id : f.displayName)}」が立っている", on);
                    if (next && !on) testFlags.Add(f); else if (!next && on) testFlags.Remove(f);
                }
                if (usesDream)
                {
                    var all = new List<CareerDefinition>(StoryIndex.Careers());
                    all.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                    var names = new string[all.Count + 1];
                    names[0] = "（夢はまだ決まっていない）";
                    int cur = 0;
                    for (int i = 0; i < all.Count; i++) { names[i + 1] = all[i].DisplayNameOrName; if (testHasDream && all[i] == testDream) cur = i + 1; }
                    int pick = EditorGUILayout.Popup("将来の夢", cur, names);
                    testHasDream = pick > 0;
                    testDream = pick > 0 ? all[pick - 1] : null;
                }
            }
        }

        for (int i = 0; i < t.PageCount; i++)
        {
            var p = t.GetPage(i);
            if (p == null) continue;
            var result = Evaluate(p, out string why);
            var mark = result == true ? "○ 出る" : result == false ? "× 出ない" : "△ 試せない条件あり";
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"{i + 1}　「{StoryTextFormatter.Format(p.heading)}」　{mark}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(why, EditorStyles.wordWrappedMiniLabel);
                if (!string.IsNullOrEmpty(p.body))
                    EditorGUILayout.LabelField(StoryTextFormatter.Format(p.body), EditorStyles.wordWrappedMiniLabel);
            }
        }
    }

    /// <summary>試す組み合わせでページが出るか。フラグ・夢以外の条件は判定せず null（△）。</summary>
    private bool? Evaluate(TutorialPage p, out string why)
    {
        if (p.showConditions == null || p.showConditions.Count == 0) { why = "いつも出る"; return true; }
        bool? result = true;
        var parts = new List<string>();
        foreach (var c in p.showConditions)
        {
            if (c == null) continue;
            string text = EventDescriber.ConditionText(c);
            if (c is StoryFlagCondition sfc)
            {
                bool ok = sfc.flag != null && (testFlags.Contains(sfc.flag) == sfc.isSet);
                parts.Add((ok ? "✓ " : "✗ ") + text);
                if (!ok) result = false;
            }
            else if (c is DreamCondition dc)
            {
                bool match = testHasDream && (dc.career == null || dc.career == testDream);
                bool ok = dc.isMatch ? match : !match;
                parts.Add((ok ? "✓ " : "✗ ") + text);
                if (!ok) result = false;
            }
            else
            {
                parts.Add("？ " + text + "（ここでは試せない）");
                if (result == true) result = null;
            }
        }
        why = "条件：" + string.Join("　", parts);
        return result;
    }
}
