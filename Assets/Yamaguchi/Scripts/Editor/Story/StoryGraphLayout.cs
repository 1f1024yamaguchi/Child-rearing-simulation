using System.Collections.Generic;
using Maruyama.Events;
using UnityEngine;

/// <summary>
/// 流れ図のデータ作りと箱の並べ方（story-tool.md 2.2）。画面には触らない。
/// - つながり（線）：続きの予約・前の選択を条件・フラグを立てる／消す／見る・将来の夢を見る・ストーリーを出す
/// - 横は年齢（イベントの条件・決まった日から出る一番早い年齢）。年齢の決まらない続きは元のイベントの右。
/// - 縦は重ならないように、左から順に空いている段へ入れる（ドラッグでの並べ替えはしない）。
/// </summary>
public static class StoryGraphLayout
{
    public enum NodeKind { Event, Flag, Career, AnyDream, Tutorial }
    public enum EdgeKind { Schedule, History, SetFlag, ClearFlag, LookFlag, Dream, ShowStory }
    public enum Mode { All, Flag, Event, Career }

    public class Node
    {
        public string key;
        public NodeKind kind;
        public Object asset;
        public string label;
        public float age;          // 横の位置（歳）
        public bool hasOwnAge;     // 条件・決まった日から年齢が出たか
        public Rect rect;
        public readonly List<Edge> outEdges = new List<Edge>();
        public readonly List<Edge> inEdges = new List<Edge>();
    }

    public class Edge
    {
        public Node from, to;
        public EdgeKind kind;
        public string label;
    }

    public class Filter
    {
        public Mode mode = Mode.Career;
        public Object seed;            // フラグ・イベント・職業（null = 未選択）
        public float minAge = 0, maxAge = 22;
        public bool linkedOnly = true; // 「全部」のとき、つながりのあるイベントだけ
        public int maxEvents = 80;     // 「全部」で出すイベントの上限（たたき台）
    }

    public class Graph
    {
        public readonly List<Node> nodes = new List<Node>();
        public readonly List<Edge> edges = new List<Edge>();
        public Vector2 size;
        public int hiddenEvents;       // 上限で出さなかったイベントの数
    }

    public const float EventW = 160f, EventH = 42f, SmallW = 140f, SmallH = 26f;
    public const float Left = 20f, Top = 34f, GapX = 24f, GapY = 14f;
    /// <summary>年齢の決まらない箱を元の箱の右に出すときのすき間（線のラベルが入るように広め）。</summary>
    public const float PushGap = 110f;
    public const float DreamAge = 12.25f; // 夢が決まる 12歳4月1日（DreamSystem の既定）

    // ─── 作る ─────────────────────────────────
    public static Graph Build(Filter filter, float pxPerYear)
    {
        var all = BuildAll();
        ComputeAges(all);   // 年齢はイベントそのものの性質なので、全部のつながりで決める
        var keep = Select(all, filter, out int hidden);

        var g = new Graph { hiddenEvents = hidden };
        foreach (var n in all.nodes) if (keep.Contains(n)) g.nodes.Add(n);
        foreach (var e in all.edges) if (keep.Contains(e.from) && keep.Contains(e.to)) g.edges.Add(e);

        Place(g, pxPerYear);
        return g;
    }

    /// <summary>プロジェクトの全部のつながり。</summary>
    private static Graph BuildAll()
    {
        var g = new Graph();
        var map = new Dictionary<Object, Node>();
        Node anyDream = null;

        Node Get(Object o, NodeKind kind, string label)
        {
            if (o == null) return null;
            if (map.TryGetValue(o, out var n)) return n;
            n = new Node { key = kind + ":" + o.GetInstanceID(), kind = kind, asset = o, label = label };
            map[o] = n;
            g.nodes.Add(n);
            return n;
        }
        void Link(Node a, Node b, EdgeKind k, string label)
        {
            if (a == null || b == null || a == b) return;
            var e = new Edge { from = a, to = b, kind = k, label = label };
            g.edges.Add(e);
            a.outEdges.Add(e);
            b.inEdges.Add(e);
        }

        var flags = EventRegistry.AllFlags();
        StoryFlag FlagOf(Object o)
        {
            if (o is StoryFlag sf) return flags.Find(f => StoryIndex.Same(f, sf)) ?? sf;
            return null;
        }
        Node FlagNode(StoryFlag f) => f == null ? null : Get(f, NodeKind.Flag, string.IsNullOrEmpty(f.displayName) ? f.id : f.displayName);
        Node CareerNode(CareerDefinition c) => c == null ? null : Get(c, NodeKind.Career, "夢：" + c.DisplayNameOrName);
        Node TutNode(TutorialData t) => t == null ? null : Get(t, NodeKind.Tutorial, t.name);

        var events = EventRegistry.All();
        foreach (var ev in events)
        {
            if (ev == null) continue;
            Get(ev, NodeKind.Event, EventLabel(ev));
        }

        foreach (var ev in events)
        {
            if (ev == null) continue;
            var en = map[ev];

            if (ev.choices != null)
            {
                foreach (var ch in ev.choices)
                {
                    if (ch == null || ch.effects == null) continue;
                    string cl = string.IsNullOrEmpty(ch.label) ? "（選択肢）" : StoryTextFormatter.Format(ch.label);
                    foreach (var ef in ch.effects)
                    {
                        if (ef == null) continue;
                        switch (ef.kind)
                        {
                            case EffectKind.SetFlag: Link(en, FlagNode(FlagOf(ef.asset)), EdgeKind.SetFlag, cl); break;
                            case EffectKind.ClearFlag: Link(en, FlagNode(FlagOf(ef.asset)), EdgeKind.ClearFlag, cl + "（消す）"); break;
                            case EffectKind.ScheduleEvent:
                                if (ef.asset is EventData target && map.TryGetValue(target, out var tn))
                                    Link(en, tn, EdgeKind.Schedule, $"{cl}・{ef.days}日後");
                                break;
                            case EffectKind.ShowStory: Link(en, TutNode(ef.asset as TutorialData), EdgeKind.ShowStory, cl); break;
                        }
                    }
                }
            }

            if (ev.conditions != null)
            {
                foreach (var c in ev.conditions)
                {
                    if (c is StoryFlagCondition sfc)
                        Link(FlagNode(FlagOf(sfc.flag)), en, EdgeKind.LookFlag, sfc.isSet ? "" : "立っていない");
                    else if (c is ChoiceHistoryCondition h && h.targetEvent != null && h.targetEvent != ev && map.TryGetValue(h.targetEvent, out var src))
                        Link(src, en, EdgeKind.History, HistoryLabel(h));
                    else if (c is DreamCondition dc)
                    {
                        Node from;
                        if (dc.career != null) from = CareerNode(dc.career);
                        else
                        {
                            if (anyDream == null)
                            {
                                anyDream = new Node { key = "anyDream", kind = NodeKind.AnyDream, label = "夢が決まった" };
                                g.nodes.Add(anyDream);
                            }
                            from = anyDream;
                        }
                        Link(from, en, EdgeKind.Dream, dc.isMatch ? "" : "ではない");
                    }
                }
            }
        }

        foreach (var t in StoryIndex.Tutorials())
        {
            if (t == null) continue;
            if (t.Timing != null && t.Timing.kind == TutorialTimingKind.FlagSet && t.Timing.targetFlag != null)
                Link(FlagNode(FlagOf(t.Timing.targetFlag)), TutNode(t), EdgeKind.LookFlag, t.Timing.offsetDays != 0 ? $"{t.Timing.offsetDays:+0;-0}日" : "立った日");
            foreach (var p in t.Pages)
            {
                if (p == null || p.showConditions == null) continue;
                foreach (var c in p.showConditions)
                    if (c is StoryFlagCondition sfc) Link(FlagNode(FlagOf(sfc.flag)), TutNode(t), EdgeKind.LookFlag, "ページ");
            }
        }

        foreach (var c in StoryIndex.Careers())
        {
            if (c == null || c.requirements == null) continue;
            foreach (var r in c.requirements)
                if (r is StoryFlagRequirementSO fr && fr.flag != null)
                    Link(FlagNode(FlagOf(fr.flag)), Get(c, NodeKind.Career, "職業：" + c.DisplayNameOrName), EdgeKind.LookFlag, fr.mustBeSet ? "職業の条件" : "立っていない");
        }

        // 職業の箱：夢でも職業の条件でも同じアセットなら1つ。名前は「夢・職業」どちらで呼ばれたかで最初のもの
        return g;
    }

    private static HashSet<Node> Select(Graph all, Filter f, out int hidden)
    {
        hidden = 0;
        var keep = new HashSet<Node>();

        if (f.mode == Mode.All)
        {
            var evs = new List<Node>();
            foreach (var n in all.nodes)
            {
                if (n.kind != NodeKind.Event) continue;
                if (f.linkedOnly && n.inEdges.Count == 0 && n.outEdges.Count == 0) continue;
                evs.Add(n);
            }
            if (evs.Count > f.maxEvents) { hidden = evs.Count - f.maxEvents; evs.RemoveRange(f.maxEvents, hidden); }
            foreach (var n in evs) keep.Add(n);
            foreach (var n in all.nodes)
            {
                if (n.kind == NodeKind.Event) continue;
                // フラグ・夢・チュートリアル：残ったイベントか、ほかの非イベントとつながっているもの
                if (n.inEdges.Count + n.outEdges.Count > 0) keep.Add(n);
            }
        }
        else
        {
            Node seed = null;
            if (f.seed != null) seed = all.nodes.Find(n => n.asset == f.seed);
            if (seed == null) return keep;
            // 辿れる分だけ（向きは問わない）
            var queue = new Queue<Node>();
            queue.Enqueue(seed);
            keep.Add(seed);
            while (queue.Count > 0)
            {
                var n = queue.Dequeue();
                foreach (var e in n.outEdges) if (keep.Add(e.to)) queue.Enqueue(e.to);
                foreach (var e in n.inEdges) if (keep.Add(e.from)) queue.Enqueue(e.from);
            }
        }

        // 年齢の範囲（イベントだけ。決まっている年齢で判定）
        if (f.minAge > 0 || f.maxAge < 22)
        {
            keep.RemoveWhere(n => n.kind == NodeKind.Event && (n.age < f.minAge || n.age >= f.maxAge + 1));
        }
        return keep;
    }

    // ─── 年齢 ─────────────────────────────────
    private static void ComputeAges(Graph g)
    {
        foreach (var n in g.nodes)
        {
            n.hasOwnAge = false;
            n.age = 0f;
            if (n.kind == NodeKind.Event && n.asset is EventData ev)
            {
                if (ev.trigger == EventTrigger.Fixed)
                {
                    n.age = Mathf.Min(ev.fixedMinAgeYears, ev.fixedMaxAgeYears) + (Mathf.Clamp(ev.fixedMonth, 1, 12) - 1) / 12f;
                    n.hasOwnAge = true;
                }
                else if (EventDescriber.TryGetAgeRange(ev, out int lo, out _))
                {
                    n.age = lo;
                    n.hasOwnAge = true;
                }
            }
            else if (n.kind == NodeKind.Career || n.kind == NodeKind.AnyDream)
            {
                n.age = DreamAge;
                n.hasOwnAge = true;
            }
        }

        // 年齢の決まらないものは、前のもの（線の元）から決める。何回か繰り返して伝える
        for (int iter = 0; iter < 12; iter++)
        {
            bool changed = false;
            foreach (var n in g.nodes)
            {
                if (n.hasOwnAge) continue;
                float best = float.NaN;
                foreach (var e in n.inEdges)
                {
                    float a = e.from.age;
                    if (e.kind == EdgeKind.Schedule && e.from.asset is EventData && TryDays(e.label, out int days)) a += days / 365f;
                    // 続き・フラグ：一番早い元（どれか1つから来れば出る）。条件：一番遅い元（そろってから出る）
                    bool earliest = e.kind == EdgeKind.Schedule || e.kind == EdgeKind.SetFlag || e.kind == EdgeKind.ClearFlag || n.kind == NodeKind.Flag;
                    if (float.IsNaN(best)) best = a;
                    else best = earliest ? Mathf.Min(best, a) : Mathf.Max(best, a);
                }
                if (float.IsNaN(best))
                {
                    // 元が無い：フラグなら見ている先の少し前に置く
                    if (n.kind == NodeKind.Flag && n.outEdges.Count > 0)
                    {
                        float m = float.MaxValue;
                        foreach (var e in n.outEdges) m = Mathf.Min(m, e.to.age);
                        best = Mathf.Max(0f, m - 0.5f);
                    }
                    else continue;
                }
                if (Mathf.Abs(best - n.age) > 0.0001f) { n.age = best; changed = true; }
            }
            if (!changed) break;
        }
    }

    private static bool TryDays(string label, out int days)
    {
        days = 0;
        if (string.IsNullOrEmpty(label)) return false;
        int dot = label.LastIndexOf('・');
        int end = label.LastIndexOf("日後");
        if (dot < 0 || end <= dot) return false;
        return int.TryParse(label.Substring(dot + 1, end - dot - 1), out days);
    }

    // ─── 並べる ─────────────────────────────────
    private static void Place(Graph g, float pxPerYear)
    {
        foreach (var n in g.nodes)
        {
            float w = n.kind == NodeKind.Event ? EventW : SmallW;
            float h = n.kind == NodeKind.Event ? EventH : SmallH;
            n.rect = new Rect(Left + n.age * pxPerYear, 0, w, h);
        }

        // 年齢の決まらないものは、元の箱の右に出す
        var inGraph = new HashSet<Node>(g.nodes);
        for (int iter = 0; iter < 8; iter++)
        {
            bool changed = false;
            foreach (var n in g.nodes)
            {
                if (n.hasOwnAge) continue;
                foreach (var e in n.inEdges)
                {
                    if (!inGraph.Contains(e.from)) continue;
                    float minX = e.from.rect.xMax + PushGap;
                    if (n.rect.x < minX) { n.rect.x = minX; changed = true; }
                }
            }
            if (!changed) break;
        }

        // 縦：左から順に、空いている段に入れる
        var order = new List<Node>(g.nodes);
        order.Sort((a, b) => a.rect.x != b.rect.x ? a.rect.x.CompareTo(b.rect.x) : string.CompareOrdinal(a.label, b.label));
        var laneRight = new List<float>();
        const float laneH = EventH + GapY;
        foreach (var n in order)
        {
            int lane = laneRight.FindIndex(r => r + GapX <= n.rect.x);
            if (lane < 0) { lane = laneRight.Count; laneRight.Add(0); }
            laneRight[lane] = n.rect.xMax;
            n.rect.y = Top + lane * laneH + (EventH - n.rect.height) / 2f;
        }

        float maxX = Left + 23 * pxPerYear;
        foreach (var n in g.nodes) maxX = Mathf.Max(maxX, n.rect.xMax);
        g.size = new Vector2(maxX + 40f, Top + Mathf.Max(1, laneRight.Count) * laneH + 20f);
    }

    // ─── 文字 ─────────────────────────────────
    public static string EventLabel(EventData e)
    {
        string t = string.IsNullOrEmpty(e.title) ? e.name : StoryTextFormatter.Format(e.title);
        return (e.trigger == EventTrigger.Fixed ? "◆" : "") + t;
    }

    private static string HistoryLabel(ChoiceHistoryCondition h)
    {
        string choice = h.choiceId;
        if (h.targetEvent != null && h.targetEvent.choices != null && !string.IsNullOrEmpty(h.choiceId))
        {
            var c = h.targetEvent.choices.Find(x => x != null && x.id == h.choiceId);
            if (c != null) choice = StoryTextFormatter.Format(c.label);
        }
        string kind = h.checkType.ToString();
        return string.IsNullOrEmpty(choice) ? kind : $"{kind}：{choice}";
    }
}
