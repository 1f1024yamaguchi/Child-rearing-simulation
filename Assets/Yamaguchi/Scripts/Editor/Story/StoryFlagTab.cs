using System.Collections.Generic;
using Maruyama.Events;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ストーリーウィンドウの「フラグ」タブ（story-tool.md 2.1）。
/// 一覧・作成・名前の変更・削除・逆引き（立てる／消す／見る）・チェック。
/// </summary>
public class StoryFlagTab
{
    private string search = "";
    private StoryFlag selected;
    private Vector2 listScroll, detailScroll;
    private bool showCreate;
    private string newId = "", newName = "";
    private SerializedObject so;

    public void Select(StoryFlag flag)
    {
        selected = flag;
        so = null;
    }

    public void Draw(Rect windowRect)
    {
        DrawHeader();
        if (showCreate) DrawCreate();

        var flags = EventRegistry.AllFlags();
        float listHeight = Mathf.Clamp(22f * Mathf.Max(flags.Count, 2) + 8f, 60f, windowRect.height * 0.4f);
        listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.Height(listHeight));
        if (flags.Count == 0)
            EditorGUILayout.LabelField("フラグはまだありません。［＋新しいフラグ］で作れます。", EditorStyles.miniLabel);
        foreach (var f in flags)
        {
            if (f == null || !Matches(f)) continue;
            DrawRow(f);
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(4);
        DrawLine();
        detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
        if (selected != null) DrawDetail(selected);
        else EditorGUILayout.LabelField("上の一覧からフラグを選んでください。", EditorStyles.miniLabel);
        EditorGUILayout.EndScrollView();
    }

    // ─── 一覧 ─────────────────────────────────
    private void DrawHeader()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(showCreate ? "閉じる" : "＋新しいフラグ", GUILayout.Width(110))) showCreate = !showCreate;
            GUILayout.Space(8);
            EditorGUILayout.LabelField("検索", GUILayout.Width(30));
            search = EditorGUILayout.TextField(search);
            if (GUILayout.Button("再読み込み", EditorStyles.miniButton, GUILayout.Width(70))) StoryIndex.Invalidate();
        }
    }

    private void DrawCreate()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            newId = EditorGUILayout.TextField(new GUIContent("ID", "英数字と _ のみ。セーブのキーなので、ゲームに出したあとは変えない"), newId);
            newName = EditorGUILayout.TextField(new GUIContent("表示名", "例：野球をやっている"), newName);
            string err = StoryFlagFactory.ValidateNewId(newId);
            EditorGUILayout.LabelField(err != null ? "✕ " + err : $"→ {StoryFlagFactory.PathOf(newId)} に作ります", EditorStyles.miniLabel);
            using (new EditorGUI.DisabledScope(err != null))
            {
                if (GUILayout.Button("作る", GUILayout.Width(80)))
                {
                    var f = StoryFlagFactory.Create(newId, newName);
                    if (f != null)
                    {
                        Select(f);
                        newId = newName = "";
                        showCreate = false;
                        GUI.FocusControl(null);
                    }
                }
            }
        }
    }

    private bool Matches(StoryFlag f)
    {
        if (string.IsNullOrEmpty(search)) return true;
        return (f.id != null && f.id.Contains(search)) || (f.displayName != null && f.displayName.Contains(search)) ||
               (f.customHobbyName != null && f.customHobbyName.Contains(search));
    }

    private void DrawRow(StoryFlag f)
    {
        var u = StoryIndex.UsageOf(f);
        var checks = StoryIndex.Check(f, out int errors);
        string mark = errors > 0 ? "✕" : checks.Count > 0 ? "⚠" : "　";
        string hobby = f.useCustomHobbyName ? $"　呼び名：{f.customHobbyName}（{f.hobbyKind}）" : "";
        string text = $"{mark} {Name(f)}{hobby}　　立てる{u.SetCount}・消す{u.ClearCount}・見る{u.LookCount}";

        var style = new GUIStyle(EditorStyles.label) { richText = false };
        if (f == selected) style.fontStyle = FontStyle.Bold;
        if (GUILayout.Button(text, style, GUILayout.Height(20))) Select(f);
    }

    // ─── 選んだフラグ ─────────────────────────────────
    private void DrawDetail(StoryFlag f)
    {
        if (so == null || so.targetObject != f) so = new SerializedObject(f);
        so.Update();

        EditorGUILayout.LabelField(Name(f), EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("ID", f.id, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("アセットを選ぶ", EditorStyles.miniButton, GUILayout.Width(90))) { Selection.activeObject = f; EditorGUIUtility.PingObject(f); }
        }
        EditorGUILayout.LabelField("　ID は作ったあと変えません（セーブのキー）。表示名は変えてよい。", EditorStyles.miniLabel);
        EditorGUILayout.PropertyField(so.FindProperty("displayName"), new GUIContent("表示名"));
        EditorGUILayout.PropertyField(so.FindProperty("description"), new GUIContent("説明・メモ"));

        var useHobby = so.FindProperty("useCustomHobbyName");
        EditorGUILayout.PropertyField(useHobby, new GUIContent("趣味の呼び名を変える", "立っている間、画面の趣味（例：スポーツ）をこの呼び名（例：野球）にする"));
        if (useHobby.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(so.FindProperty("customHobbyName"), new GUIContent("呼び名"));
            EditorGUILayout.PropertyField(so.FindProperty("hobbyKind"), new GUIContent("趣味の種類"));
            EditorGUI.indentLevel--;
        }
        if (so.ApplyModifiedProperties()) StoryIndex.Invalidate();

        EditorGUILayout.Space(6);
        var u = StoryIndex.UsageOf(f);
        DrawEvents("立てる", u.setters);
        DrawEvents("消す", u.clearers);
        DrawEvents("見る（イベントの条件）", u.eventConditions);
        DrawTutorials("見る（チュートリアルの出すタイミング）", u.tutorialTimings);
        DrawTutorials("見る（チュートリアルのページの条件）", u.tutorialPages);
        if (u.requirements.Count > 0)
        {
            EditorGUILayout.LabelField("見る（支援制度・職業の条件の部品）", EditorStyles.miniBoldLabel);
            foreach (var r in u.requirements) ObjectRow(r.name, r);
            foreach (var c in u.careers) ObjectRow("職業「" + c.DisplayNameOrName + "」", c);
        }

        EditorGUILayout.Space(6);
        var checks = StoryIndex.Check(f, out _);
        if (checks.Count == 0) EditorGUILayout.LabelField("チェック：問題なし", EditorStyles.miniLabel);
        foreach (var c in checks) EditorGUILayout.HelpBox(c, c.StartsWith("✕") ? MessageType.Error : MessageType.Warning);

        EditorGUILayout.Space(6);
        using (new EditorGUI.DisabledScope(!u.IsUnused))
        {
            if (GUILayout.Button(u.IsUnused ? "このフラグを消す" : "使われているので消せません", GUILayout.Width(200)))
            {
                if (EditorUtility.DisplayDialog("フラグを消す", $"「{Name(f)}」を消します。元に戻せません。", "消す", "やめる"))
                {
                    if (StoryFlagFactory.DeleteIfUnused(f)) Select(null);
                    GUIUtility.ExitGUI();
                }
            }
        }
    }

    private static void DrawEvents(string label, List<EventData> list)
    {
        EditorGUILayout.LabelField($"{label}（{list.Count}）", EditorStyles.miniBoldLabel);
        if (list.Count == 0) { EditorGUILayout.LabelField("　なし", EditorStyles.miniLabel); return; }
        foreach (var e in list)
        {
            if (e == null) continue;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("　" + (string.IsNullOrEmpty(e.title) ? e.name : e.title), GUILayout.ExpandWidth(true));
                if (GUILayout.Button("開く", EditorStyles.miniButton, GUILayout.Width(50))) EventEditorWindow.OpenWith(e);
            }
        }
    }

    private static void DrawTutorials(string label, List<TutorialData> list)
    {
        if (list.Count == 0) return;
        EditorGUILayout.LabelField($"{label}（{list.Count}）", EditorStyles.miniBoldLabel);
        foreach (var t in list)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("　" + t.name, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("開く", EditorStyles.miniButton, GUILayout.Width(50))) TutorialEditorWindow.OpenWith(t);
            }
        }
    }

    private static void ObjectRow(string label, Object o)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("　" + label, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("選ぶ", EditorStyles.miniButton, GUILayout.Width(50))) { Selection.activeObject = o; EditorGUIUtility.PingObject(o); }
        }
    }

    private static string Name(StoryFlag f)
    {
        string n = string.IsNullOrEmpty(f.displayName) ? f.name : f.displayName;
        return $"{n}（{f.id}）";
    }

    private static void DrawLine()
    {
        var r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.5f));
    }
}
