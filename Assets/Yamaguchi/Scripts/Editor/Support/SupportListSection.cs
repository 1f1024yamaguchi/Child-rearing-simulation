using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>「すべての制度」の一覧。カテゴリのタブ（件数つき）・登録状態・種類・✕⚠の数・表示順の入れ替え。</summary>
public static class SupportListSection
{
    private static bool show = true;
    private static int tab;   // 0＝すべて、1〜＝SupportCategory の順

    /// <summary>名前ボタンが押された制度を返す（押されなければ null）。</summary>
    public static SupportScheme Draw(SupportScheme current)
    {
        show = EditorGUILayout.Foldout(show, "すべての制度", true, EditorStyles.foldoutHeader);
        if (!show) return null;

        var all = SupportRegistry.AllSchemes();
        var categories = (SupportCategory[])Enum.GetValues(typeof(SupportCategory));

        var labels = new string[categories.Length + 1];
        labels[0] = $"すべて（{CountOf(all, null)}）";
        for (int i = 0; i < categories.Length; i++)
            labels[i + 1] = $"{SupportSchemeDescriber.CategoryName(categories[i])}（{CountOf(all, categories[i])}）";
        tab = GUILayout.Toolbar(Mathf.Clamp(tab, 0, categories.Length), labels);

        SupportCategory? filter = tab == 0 ? (SupportCategory?)null : categories[tab - 1];

        var rows = new List<SupportScheme>();
        foreach (var s in all)
        {
            if (s != null && (filter == null || s.category == filter.Value)) rows.Add(s);
        }
        // 登録済みは表示順どおり、未登録は後ろに名前順
        rows.Sort((a, b) =>
        {
            int c = SupportRegistry.OrderOf(a).CompareTo(SupportRegistry.OrderOf(b));
            return c != 0 ? c : string.CompareOrdinal(a.name, b.name);
        });

        SupportScheme picked = null;
        bool hasManager = SupportRegistry.FindManager() != null;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (rows.Count == 0) EditorGUILayout.LabelField("まだありません。");

            SupportScheme moveTarget = null;
            bool moveUp = false;

            foreach (var s in rows)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var style = s == current ? EditorStyles.boldLabel : EditorStyles.label;
                    if (GUILayout.Button(s.name, style, GUILayout.Width(190))) picked = s;

                    string reg = !hasManager ? "—" : SupportRegistry.IsRegistered(s) ? "✓ 登録" : "⚠ 未登録";
                    GUILayout.Label(reg, GUILayout.Width(64));

                    GUILayout.Label(SupportSchemeDescriber.KindName(SupportSchemeDescriber.KindOf(s)), GUILayout.Width(90));

                    var issues = SupportSchemeValidator.Check(s);
                    SupportSchemeValidator.Count(issues, out int e, out int w);
                    GUILayout.Label(e == 0 && w == 0 ? "✓" : $"✕{e} ⚠{w}", GUILayout.Width(60));

                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(!hasManager || !SupportRegistry.CanMove(s, true)))
                        if (GUILayout.Button("↑", GUILayout.Width(26))) { moveTarget = s; moveUp = true; }
                    using (new EditorGUI.DisabledScope(!hasManager || !SupportRegistry.CanMove(s, false)))
                        if (GUILayout.Button("↓", GUILayout.Width(26))) { moveTarget = s; moveUp = false; }
                }
            }

            // 入れ替えは、描画が終わってから1回だけ
            if (moveTarget != null) SupportRegistry.Move(moveTarget, moveUp);
        }

        EditorGUILayout.LabelField("↑↓は SupportManager の並び（＝サポート画面の表示順）を、同じカテゴリの前後と入れ替えます。", EditorStyles.wordWrappedMiniLabel);
        return picked;
    }

    private static int CountOf(List<SupportScheme> all, SupportCategory? c)
    {
        int n = 0;
        foreach (var s in all)
        {
            if (s != null && (c == null || s.category == c.Value)) n++;
        }
        return n;
    }
}
