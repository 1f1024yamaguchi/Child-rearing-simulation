using System.Collections.Generic;
using System.Linq;
using Maruyama.Events;
using UnityEditor;
using UnityEngine;

/// <summary>編集ウィンドウの「すべてのイベント」一覧。タブ（メリット／デメリット／決まった日）、年齢順。</summary>
public static class EventListSection
{
    private static readonly string[] TabNames = { "メリット", "デメリット", "決まった日" };
    private static int tab;

    /// <summary>タブ名に件数を付けたもの。</summary>
    private static string[] TabLabels(List<EventData> all)
    {
        var labels = new string[TabNames.Length];
        for (int i = 0; i < labels.Length; i++)
            labels[i] = $"{TabNames[i]} {all.Count(e => InTab(e, i))}";
        return labels;
    }

    // メリット／デメリットは種類で、決まった日は出方で分ける（決まった日のイベントはメリット／デメリットにも出る）
    private static bool InTab(EventData e, int t)
    {
        switch (t)
        {
            case 0: return e.category == EventCategory.Merit;
            case 1: return e.category == EventCategory.Demerit;
            default: return e.trigger == EventTrigger.Fixed;
        }
    }

    /// <summary>年齢順に並べたイベント（同じ年齢はファイル名順）。</summary>
    public static List<EventData> Sorted(List<EventData> all)
    {
        return all.Where(e => e != null)
                  .OrderBy(e => EventDescriber.MinAgeOf(e))
                  .ThenBy(e => e.name, System.StringComparer.Ordinal)
                  .ToList();
    }

    /// <summary>押された行のイベントを返す（押されなければ null）。</summary>
    public static EventData Draw(EventData selected)
    {
        var all = EventRegistry.All();
        tab = GUILayout.Toolbar(tab, TabLabels(all));

        var header = EditorStyles.miniBoldLabel;
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("ファイル名", header, GUILayout.Width(190));
            GUILayout.Label("ID", header, GUILayout.Width(150));
            GUILayout.Label("年齢", header, GUILayout.Width(56));
            GUILayout.Label("選択肢", header, GUILayout.Width(44));
            GUILayout.Label("画像", header, GUILayout.Width(36));
            GUILayout.Label("✕ ⚠", header, GUILayout.Width(60));
        }

        EventData picked = null;
        foreach (var e in Sorted(all))
        {
            if (!InTab(e, tab)) continue;
            if (DrawRow(e, e == selected)) picked = e;
        }
        return picked;
    }

    private static bool DrawRow(EventData e, bool isSelected)
    {
        EventValidator.Count(e, out int errors, out int warnings);
        var mini = EditorStyles.miniLabel;

        var rect = EditorGUILayout.BeginHorizontal();
        if (isSelected && Event.current.type == EventType.Repaint)
            EditorGUI.DrawRect(rect, new Color(0.25f, 0.5f, 0.9f, 0.25f));

        GUILayout.Label(e.name, mini, GUILayout.Width(190));
        GUILayout.Label(e.id, mini, GUILayout.Width(150));
        GUILayout.Label(AgeLabel(e), mini, GUILayout.Width(56));
        GUILayout.Label((e.choices == null ? 0 : e.choices.Count).ToString(), mini, GUILayout.Width(44));
        GUILayout.Label(e.iconSprite != null ? "あり" : "なし", mini, GUILayout.Width(36));
        GUILayout.Label($"✕{errors} ⚠{warnings}", mini, GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();

        // 行のどこを押しても選択する
        var ev = Event.current;
        if (ev.type == EventType.MouseDown && ev.button == 0 && rect.Contains(ev.mousePosition))
        {
            ev.Use();
            return true;
        }
        return false;
    }

    private static string AgeLabel(EventData e)
    {
        if (e.trigger == EventTrigger.Fixed)
            return e.fixedMinAgeYears == e.fixedMaxAgeYears ? e.fixedMinAgeYears + "歳" : e.fixedMinAgeYears + "〜" + e.fixedMaxAgeYears + "歳";
        return EventDescriber.TryGetAgeRange(e, out int lo, out int hi) ? (lo == hi ? lo + "歳" : lo + "〜" + hi + "歳") : "—";
    }
}

