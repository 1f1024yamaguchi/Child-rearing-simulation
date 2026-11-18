using System;
using System.Collections.Generic;
using Maruyama.Events;
using UnityEditor;
using UnityEngine;
using Node = StoryGraphLayout.Node;
using Edge = StoryGraphLayout.Edge;
using NodeKind = StoryGraphLayout.NodeKind;
using EdgeKind = StoryGraphLayout.EdgeKind;
using Mode = StoryGraphLayout.Mode;

/// <summary>
/// ストーリーウィンドウの「流れ図」タブ（story-tool.md 2.2）。IMGUI で箱と線を描くだけ（GraphView は使わない）。
/// 箱を1回押すと選ぶ（つながる線が太くなる）、2回押すと編集ウィンドウで開く。
/// データ作りと並べ方は StoryGraphLayout。
/// </summary>
public class StoryGraphTab
{
    private static readonly string[] ModeNames = { "全部", "フラグから", "イベントから", "夢の職業から" };

    private readonly StoryGraphLayout.Filter filter = new StoryGraphLayout.Filter();
    private float pxPerYear = 120f;
    private StoryGraphLayout.Graph graph;
    private int builtVersion = -1;
    private bool dirty = true;
    private Vector2 scroll;
    private Node selected;

    /// <summary>フラグの箱を開くとき（フラグのタブへ移る）。</summary>
    public Action<StoryFlag> openFlag;

    private static readonly Color MeritColor = new Color(0.25f, 0.65f, 0.3f);
    private static readonly Color DemeritColor = new Color(0.8f, 0.3f, 0.3f);
    private static readonly Color FlagColor = new Color(0.85f, 0.65f, 0.15f);
    private static readonly Color DreamColor = new Color(0.3f, 0.55f, 0.9f);
    private static readonly Color TutorialColor = new Color(0.6f, 0.4f, 0.8f);
    private static readonly Color LineColor = new Color(0.75f, 0.75f, 0.75f);

    public void Draw(Rect windowRect)
    {
        DrawFilter();

        if (graph == null || dirty || builtVersion != StoryIndex.Version)
        {
            graph = StoryGraphLayout.Build(filter, pxPerYear);
            builtVersion = StoryIndex.Version;
            dirty = false;
            if (selected != null && !graph.nodes.Contains(selected)) selected = null;
        }

        if (filter.mode != Mode.All && filter.seed == null)
        {
            EditorGUILayout.HelpBox("上で起点（フラグ・イベント・夢の職業）を選ぶと、そこから辿れる分だけ出します。", MessageType.Info);
            DrawLegend();
            return;
        }
        if (graph.nodes.Count == 0)
        {
            EditorGUILayout.HelpBox(filter.mode == Mode.All
                ? "つながりのあるイベントがまだありません（続きの予約・前の選択の条件・フラグ・将来の夢のどれかでつながると出ます）。"
                : "この条件では何も出ません。", MessageType.Info);
            DrawLegend();
            return;
        }
        if (graph.hiddenEvents > 0)
            EditorGUILayout.HelpBox($"イベントが多いので {filter.maxEvents} 個まで出しています（{graph.hiddenEvents} 個は省略）。起点か年齢で絞ってください。", MessageType.Warning);

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
        var canvas = GUILayoutUtility.GetRect(graph.size.x, graph.size.y, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
        DrawCanvas(canvas);
        EditorGUILayout.EndScrollView();

        DrawSelection();
        DrawLegend();
    }

    // ─── 絞り込み ─────────────────────────────────
    private void DrawFilter()
    {
        EditorGUI.BeginChangeCheck();
        using (new EditorGUILayout.HorizontalScope())
        {
            var mode = (Mode)EditorGUILayout.Popup((int)filter.mode, ModeNames, GUILayout.Width(110));
            if (mode != filter.mode) { filter.mode = mode; filter.seed = null; }

            switch (filter.mode)
            {
                case Mode.Flag: filter.seed = Pick(filter.seed, EventRegistry.AllFlags(), f => (string.IsNullOrEmpty(f.displayName) ? f.id : f.displayName)); break;
                case Mode.Event:
                    var evs = new List<EventData>(EventRegistry.All());
                    evs.RemoveAll(e => e == null);
                    evs.Sort((a, b) => EventDescriber.MinAgeOf(a) != EventDescriber.MinAgeOf(b) ? EventDescriber.MinAgeOf(a).CompareTo(EventDescriber.MinAgeOf(b)) : string.CompareOrdinal(a.name, b.name));
                    filter.seed = Pick(filter.seed, evs, e => StoryGraphLayout.EventLabel(e));
                    break;
                case Mode.Career:
                    var cs = new List<CareerDefinition>(StoryIndex.Careers());
                    cs.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                    filter.seed = Pick(filter.seed, cs, c => c.DisplayNameOrName);
                    break;
                default:
                    filter.linkedOnly = EditorGUILayout.ToggleLeft("つながりのあるイベントだけ", filter.linkedOnly, GUILayout.Width(170));
                    break;
            }
            GUILayout.FlexibleSpace();
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"年齢 {filter.minAge:0}〜{filter.maxAge:0}歳", GUILayout.Width(90));
            EditorGUILayout.MinMaxSlider(ref filter.minAge, ref filter.maxAge, 0, 22, GUILayout.MinWidth(120));
            filter.minAge = Mathf.Round(filter.minAge);
            filter.maxAge = Mathf.Round(filter.maxAge);
            GUILayout.Space(10);
            EditorGUILayout.LabelField("横の幅", GUILayout.Width(40));
            pxPerYear = GUILayout.HorizontalSlider(pxPerYear, 60f, 320f, GUILayout.Width(100));
            if (GUILayout.Button("再読み込み", EditorStyles.miniButton, GUILayout.Width(70))) StoryIndex.Invalidate();
        }
        if (EditorGUI.EndChangeCheck()) dirty = true;
    }

    private static UnityEngine.Object Pick<T>(UnityEngine.Object cur, List<T> list, Func<T, string> name) where T : UnityEngine.Object
    {
        var names = new string[list.Count + 1];
        names[0] = list.Count == 0 ? "（ありません）" : "（選んでください）";
        int idx = 0;
        for (int i = 0; i < list.Count; i++)
        {
            names[i + 1] = name(list[i]).Replace('/', '／');
            if (list[i] == cur) idx = i + 1;
        }
        int pick = EditorGUILayout.Popup(idx, names, GUILayout.MinWidth(160));
        return pick <= 0 ? null : list[pick - 1];
    }

    // ─── 描く ─────────────────────────────────
    private void DrawCanvas(Rect canvas)
    {
        var o = canvas.position;
        var ev = Event.current;

        if (ev.type == EventType.Repaint)
        {
            // 年齢の目盛り
            for (int a = 0; a <= 22; a++)
            {
                float x = o.x + StoryGraphLayout.Left + a * pxPerYear;
                EditorGUI.DrawRect(new Rect(x, o.y + 18, 1, canvas.height - 18), new Color(0.5f, 0.5f, 0.5f, a % 6 == 0 ? 0.35f : 0.15f));
                GUI.Label(new Rect(x + 2, o.y + 2, 40, 16), a + "歳", EditorStyles.miniLabel);
            }
            foreach (var e in graph.edges) DrawEdge(e, o);
        }

        foreach (var n in graph.nodes) DrawNode(n, o);

        // 線のラベルは箱の上に重ねて読めるように最後
        if (ev.type == EventType.Repaint)
            foreach (var e in graph.edges) DrawEdgeLabel(e, o);

        if (ev.type == EventType.MouseDown && ev.button == 0 && canvas.Contains(ev.mousePosition))
        {
            Node hit = null;
            foreach (var n in graph.nodes) if (Offset(n.rect, o).Contains(ev.mousePosition)) hit = n;
            selected = hit;
            if (hit != null && ev.clickCount >= 2) Open(hit);
            ev.Use();
            GUI.changed = true;
        }
    }

    private void DrawNode(Node n, Vector2 o)
    {
        var r = Offset(n.rect, o);
        if (Event.current.type != EventType.Repaint) return;

        bool sel = n == selected;
        bool related = selected != null && IsNeighbor(selected, n);
        var bg = EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.93f, 0.93f, 0.93f);
        Color accent;
        switch (n.kind)
        {
            case NodeKind.Event: accent = (n.asset as EventData) != null && ((EventData)n.asset).category == EventCategory.Merit ? MeritColor : DemeritColor; break;
            case NodeKind.Flag: accent = FlagColor; break;
            case NodeKind.Tutorial: accent = TutorialColor; break;
            default: accent = DreamColor; break;
        }

        var border = sel ? new Color(0.3f, 0.6f, 1f) : related ? new Color(0.55f, 0.75f, 1f) : new Color(0.45f, 0.45f, 0.45f);
        float bw = sel ? 2f : 1f;
        EditorGUI.DrawRect(new Rect(r.x - bw, r.y - bw, r.width + bw * 2, r.height + bw * 2), border);
        EditorGUI.DrawRect(r, bg);

        string text = n.label;
        switch (n.kind)
        {
            case NodeKind.Event:
                EditorGUI.DrawRect(new Rect(r.x, r.y, 4, r.height), accent);
                var e = n.asset as EventData;
                string sub = e != null ? TriggerName(e.trigger) + (n.hasOwnAge ? $"・{Mathf.FloorToInt(n.age)}歳〜" : "") : "";
                GUI.Label(new Rect(r.x + 7, r.y + 2, r.width - 9, 18), text, EditorStyles.boldLabel);
                GUI.Label(new Rect(r.x + 7, r.y + 21, r.width - 9, 16), sub, EditorStyles.miniLabel);
                break;
            case NodeKind.Flag:
                EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 3), accent);
                GUI.Label(new Rect(r.x + 4, r.y + 4, r.width - 8, 20), "（" + text + "）", CenteredMini);
                break;
            default:
                EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 3), accent);
                GUI.Label(new Rect(r.x + 4, r.y + 4, r.width - 8, 20), "[" + text + "]", CenteredMini);
                break;
        }
    }

    private void DrawEdge(Edge e, Vector2 o)
    {
        var a = Offset(e.from.rect, o);
        var b = Offset(e.to.rect, o);
        Vector3 start = new Vector3(a.xMax, a.center.y);
        Vector3 end = new Vector3(b.xMin, b.center.y);
        float t = Mathf.Max(40f, Mathf.Abs(end.x - start.x) * 0.5f);
        Vector3 st = start + Vector3.right * t, et = end + Vector3.left * t;

        bool hi = selected != null && (e.from == selected || e.to == selected);
        var col = EdgeColor(e.kind);
        if (selected != null && !hi) col.a = 0.35f;
        float width = hi ? 3.5f : 2f;

        if (IsDashed(e.kind))
        {
            var pts = Handles.MakeBezierPoints(start, end, st, et, 24);
            Handles.color = col;
            for (int i = 0; i + 1 < pts.Length; i += 2) Handles.DrawAAPolyLine(width, pts[i], pts[i + 1]);
        }
        else
        {
            Handles.DrawBezier(start, end, st, et, col, null, width);
        }

        // 矢印
        Handles.color = col;
        Vector3 dir = (end - et).normalized;
        if (dir == Vector3.zero) dir = Vector3.right;
        Vector3 perp = new Vector3(-dir.y, dir.x);
        Handles.DrawAAConvexPolygon(end, end - dir * 9f + perp * 4.5f, end - dir * 9f - perp * 4.5f);
        Handles.color = Color.white;
    }

    private void DrawEdgeLabel(Edge e, Vector2 o)
    {
        if (string.IsNullOrEmpty(e.label)) return;
        bool hi = selected == null || e.from == selected || e.to == selected;
        if (!hi) return;
        var a = Offset(e.from.rect, o);
        var b = Offset(e.to.rect, o);
        Vector3 start = new Vector3(a.xMax, a.center.y), end = new Vector3(b.xMin, b.center.y);
        float t = Mathf.Max(40f, Mathf.Abs(end.x - start.x) * 0.5f);
        var pts = Handles.MakeBezierPoints(start, end, start + Vector3.right * t, end + Vector3.left * t, 9);
        var mid = pts[4];
        var size = EditorStyles.miniLabel.CalcSize(new GUIContent(e.label));
        var r = new Rect(mid.x - size.x / 2f, mid.y - size.y - 1, size.x + 2, size.y);
        EditorGUI.DrawRect(r, EditorGUIUtility.isProSkin ? new Color(0.16f, 0.16f, 0.16f, 0.85f) : new Color(1f, 1f, 1f, 0.85f));
        GUI.Label(r, e.label, EditorStyles.miniLabel);
    }

    private void DrawSelection()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            if (selected == null)
            {
                EditorGUILayout.LabelField("箱を押すと選びます（2回押すと編集ウィンドウで開く）。", EditorStyles.miniLabel);
                return;
            }
            string info = selected.label;
            if (selected.asset is EventData e) info += "　" + EventDescriber.Summary(e);
            EditorGUILayout.LabelField(info, EditorStyles.miniLabel);
            using (new EditorGUI.DisabledScope(selected.asset == null))
                if (GUILayout.Button("開く", EditorStyles.miniButton, GUILayout.Width(50))) Open(selected);
            if (GUILayout.Button("ここから辿る", EditorStyles.miniButton, GUILayout.Width(80)) && selected.asset != null)
            {
                filter.mode = selected.kind == NodeKind.Flag ? Mode.Flag : selected.kind == NodeKind.Career ? Mode.Career : Mode.Event;
                filter.seed = selected.asset;
                dirty = true;
            }
        }
    }

    private static void DrawLegend()
    {
        EditorGUILayout.LabelField("実線→ 続きの予約（○日後）／フラグを立てる・消す　点線→ 前の選択を条件・フラグや夢を見る　（ ）フラグ　[ ] 夢・職業・チュートリアル　◆ 決まった日　左の帯：緑＝メリット・赤＝デメリット", EditorStyles.wordWrappedMiniLabel);
    }

    // ─── 開く ─────────────────────────────────
    private void Open(Node n)
    {
        switch (n.asset)
        {
            case EventData e: EventEditorWindow.OpenWith(e); break;
            case StoryFlag f: openFlag?.Invoke(f); break;
            case TutorialData t: TutorialEditorWindow.OpenWith(t); break;
            case UnityEngine.Object obj: Selection.activeObject = obj; EditorGUIUtility.PingObject(obj); break;
        }
    }

    // ─── 小物 ─────────────────────────────────
    private static Rect Offset(Rect r, Vector2 o) => new Rect(r.x + o.x, r.y + o.y, r.width, r.height);

    private static bool IsNeighbor(Node a, Node b)
    {
        foreach (var e in a.outEdges) if (e.to == b) return true;
        foreach (var e in a.inEdges) if (e.from == b) return true;
        return false;
    }

    private static bool IsDashed(EdgeKind k) => k == EdgeKind.History || k == EdgeKind.LookFlag || k == EdgeKind.Dream;

    private static Color EdgeColor(EdgeKind k)
    {
        switch (k)
        {
            case EdgeKind.SetFlag:
            case EdgeKind.LookFlag: return FlagColor;
            case EdgeKind.ClearFlag: return DemeritColor;
            case EdgeKind.Dream: return DreamColor;
            case EdgeKind.ShowStory: return TutorialColor;
            default: return LineColor;
        }
    }

    private static string TriggerName(EventTrigger t)
    {
        switch (t)
        {
            case EventTrigger.Random: return "抽選";
            case EventTrigger.Fixed: return "決まった日";
            case EventTrigger.FollowUp: return "続き";
            case EventTrigger.OnCondition: return "条件";
            default: return "コードから";
        }
    }

    private static GUIStyle centeredMini;
    private static GUIStyle CenteredMini
    {
        get
        {
            if (centeredMini == null) centeredMini = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
            return centeredMini;
        }
    }
}
