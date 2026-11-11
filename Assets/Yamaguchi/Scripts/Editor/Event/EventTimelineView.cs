using Maruyama.Events;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 編集ウィンドウの年表。横軸 0〜22歳。決まった日は点（1回）または点の並び（毎年）、
/// 抽選は AgeRangeCondition の範囲を横棒で描く（条件が無ければ全体）。メリット緑・デメリット赤。IMGUI で描く。
/// </summary>
public static class EventTimelineView
{
    private const int MaxAge = 22;        // 軸の右端（22歳の1年ぶんまで描く）
    private const float RowHeight = 16f;
    private const float LabelWidth = 190f;
    private const float DotSize = 8f;

    private static readonly Color MeritColor = new Color(0.25f, 0.7f, 0.3f, 1f);
    private static readonly Color DemeritColor = new Color(0.85f, 0.3f, 0.3f, 1f);

    /// <summary>押された行のイベントを返す（押されなければ null）。</summary>
    public static EventData Draw(EventData selected)
    {
        var rows = EventListSection.Sorted(EventRegistry.All());

        DrawAxis();

        EventData picked = null;
        foreach (var e in rows)
        {
            if (DrawRow(e, e == selected)) picked = e;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("● 決まった日（点）　▬ 抽選（年齢の範囲。条件なしは全体）　緑＝メリット　赤＝デメリット", EditorStyles.miniLabel);
        }
        return picked;
    }

    private static void DrawAxis()
    {
        var rect = GUILayoutUtility.GetRect(10, RowHeight, GUILayout.ExpandWidth(true));
        var chart = ChartRect(rect);
        for (int a = 0; a <= MaxAge; a++)
        {
            float x = chart.x + chart.width * a / (MaxAge + 1);
            GUI.Label(new Rect(x - 2, rect.y, 24, rect.height), a.ToString(), EditorStyles.miniLabel);
        }
    }

    private static Rect ChartRect(Rect row) => new Rect(row.x + LabelWidth, row.y, Mathf.Max(10f, row.width - LabelWidth - 4f), row.height);

    private static bool DrawRow(EventData e, bool isSelected)
    {
        var rect = GUILayoutUtility.GetRect(10, RowHeight, GUILayout.ExpandWidth(true));
        var chart = ChartRect(rect);
        float unit = chart.width / (MaxAge + 1); // 1歳ぶんの幅
        var color = e.category == EventCategory.Merit ? MeritColor : DemeritColor;

        if (Event.current.type == EventType.Repaint)
        {
            if (isSelected) EditorGUI.DrawRect(rect, new Color(0.25f, 0.5f, 0.9f, 0.25f));
            EditorGUI.DrawRect(new Rect(chart.x, rect.y + rect.height - 1, chart.width, 1), new Color(0.5f, 0.5f, 0.5f, 0.2f));
        }
        GUI.Label(new Rect(rect.x, rect.y, LabelWidth - 4f, rect.height), e.name, EditorStyles.miniLabel);

        if (Event.current.type == EventType.Repaint)
        {
            if (e.trigger == EventTrigger.Fixed) DrawFixed(e, chart, unit, color);
            else DrawRandom(e, chart, unit, color);
        }

        var ev = Event.current;
        if (ev.type == EventType.MouseDown && ev.button == 0 && rect.Contains(ev.mousePosition))
        {
            ev.Use();
            return true;
        }
        return false;
    }

    private static void DrawFixed(EventData e, Rect chart, float unit, Color color)
    {
        int lo = Mathf.Min(e.fixedMinAgeYears, e.fixedMaxAgeYears);
        int hi = Mathf.Max(e.fixedMinAgeYears, e.fixedMaxAgeYears);
        // 1年の中の位置（月日から）
        float inYear = GameDate.ToAgeDays(0, Mathf.Clamp(e.fixedMonth, 1, 12), Mathf.Max(1, e.fixedDay)) / 365f;
        for (int age = lo; age <= hi && age <= MaxAge; age++)
        {
            float x = chart.x + unit * (age + Mathf.Clamp01(inYear));
            EditorGUI.DrawRect(new Rect(x - DotSize / 2f, chart.y + (chart.height - DotSize) / 2f, DotSize, DotSize), color);
        }
    }

    private static void DrawRandom(EventData e, Rect chart, float unit, Color color)
    {
        int lo = 0, hi = MaxAge;
        bool hasRange = EventDescriber.TryGetAgeRange(e, out int a, out int b);
        if (hasRange) { lo = a; hi = Mathf.Min(b, MaxAge); }

        // 条件が無いときは全体を薄く描く
        if (!hasRange) color.a = 0.35f;
        float x0 = chart.x + unit * lo;
        float x1 = chart.x + unit * (hi + 1);
        EditorGUI.DrawRect(new Rect(x0, chart.y + 4f, Mathf.Max(2f, x1 - x0), chart.height - 8f), color);
    }
}

