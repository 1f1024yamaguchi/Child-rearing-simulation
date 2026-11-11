using Maruyama.Events;
using UnityEditor;
using UnityEngine;

/// <summary>
/// メニュー「子育て → イベント → 編集ウィンドウ」。
/// メリット／デメリットイベント（EventData）を、Inspector ではなく専用のウィンドウで作る・直す・チェックする。
/// 各欄の中身は Event*Section.cs などに分けてある。ここは並べて呼ぶだけ。
/// </summary>
public class EventEditorWindow : EditorWindow
{
    private EventData eventData;
    private Vector2 scroll;
    private int listMode; // 0 = 一覧 / 1 = 年表

    [MenuItem("子育て/イベント/編集ウィンドウ", false, 4)]
    public static void OpenWindow() => GetWindow<EventEditorWindow>("イベント");

    /// <summary>
    /// プロジェクト内のすべてのイベントで、ID が空の選択肢に自動で番号（c1, c2 ...）を付与する。
    /// </summary>
    [MenuItem("子育て/イベント/選択肢に ID を付ける", false, 20)]
    public static void AssignChoiceIdsToAllEvents()
    {
        var allEvents = EventRegistry.All();
        int updatedEventsCount = 0;
        int updatedChoicesCount = 0;

        foreach (var evt in allEvents)
        {
            if (evt == null || evt.choices == null || evt.choices.Count == 0) continue;

            bool eventModified = false;
            var so = new SerializedObject(evt);
            so.Update();
            var choicesProp = so.FindProperty("choices");

            var used = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < choicesProp.arraySize; i++)
            {
                var idProp = choicesProp.GetArrayElementAtIndex(i).FindPropertyRelative("id");
                if (idProp != null && !string.IsNullOrWhiteSpace(idProp.stringValue))
                    used.Add(idProp.stringValue);
            }

            int nextNum = 1;
            for (int i = 0; i < choicesProp.arraySize; i++)
            {
                var idProp = choicesProp.GetArrayElementAtIndex(i).FindPropertyRelative("id");
                if (idProp != null && string.IsNullOrWhiteSpace(idProp.stringValue))
                {
                    while (used.Contains($"c{nextNum}"))
                    {
                        nextNum++;
                    }
                    string newId = $"c{nextNum}";
                    idProp.stringValue = newId;
                    used.Add(newId);
                    nextNum++;
                    eventModified = true;
                    updatedChoicesCount++;
                }
            }

            if (eventModified)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(evt);
                updatedEventsCount++;
            }
        }

        AssetDatabase.SaveAssets();
        EventRegistry.Invalidate();
        Debug.Log($"[EventEditor] 選択肢 ID の付与完了: {updatedEventsCount} 件のイベント、計 {updatedChoicesCount} 個の選択肢に ID を設定しました。");
        EditorUtility.DisplayDialog("選択肢に ID を付ける", $"{updatedEventsCount} 件のイベント、計 {updatedChoicesCount} 個の選択肢に ID を設定しました。", "OK");
    }

    /// <summary>指定のイベントを開いた状態でウィンドウを出す（EventData の Inspector から呼ぶ）。</summary>
    public static void OpenWith(EventData e)
    {
        var w = GetWindow<EventEditorWindow>("イベント");
        w.eventData = e;
        w.Repaint();
    }

    /// <summary>指定したイベントを編集対象として選択する（リンク等から呼ぶ）。</summary>
    public static void SelectEvent(EventData e) => OpenWith(e);

    private void OnEnable()
    {
        Undo.undoRedoPerformed += OnUndoRedo;
        if (eventData == null && Selection.activeObject is EventData selected) eventData = selected;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
        EventConditionList.ClearCache();
    }

    private void OnUndoRedo()
    {
        EventRegistry.Invalidate();
        Repaint();
    }

    private void OnSelectionChange()
    {
        if (Selection.activeObject is EventData selected && selected != eventData)
        {
            eventData = selected;
            Repaint();
        }
    }

    private void OnProjectChange()
    {
        EventRegistry.Invalidate();
        Repaint();
    }

    // 再生中は条件の判定が変わるので、定期的に描き直す
    private void OnInspectorUpdate()
    {
        if (EditorApplication.isPlaying) Repaint();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.HelpBox(
            "イベント1つ＝アセット1つ。Resources/Events に置くと、自動でゲームに出ます。\n" +
            "メリット／デメリットのイベントの中身を、ここで作って直します。いちばん下の「チェック」で、直すところが分かります。\n" +
            "値の書き換えは元に戻せます（Ctrl+Z）。",
            MessageType.Info);

        DrawPicker();

        if (eventData == null)
        {
            EditorGUILayout.HelpBox("イベントのアセットを選ぶか、「新しく作る」を押してください。下の一覧からも選べます。", MessageType.None);
        }
        else
        {
            DrawEvent();
        }

        EditorGUILayout.Space(8);
        DrawAll();

        EditorGUILayout.EndScrollView();
    }

    // ─── 選ぶ・作る ──────────────────────────────────────────────
    private void DrawPicker()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            var picked = (EventData)EditorGUILayout.ObjectField("イベント", eventData, typeof(EventData), false);
            if (picked != eventData) eventData = picked;

            if (GUILayout.Button("新しく作る ▼", GUILayout.Width(100))) ShowTemplateMenu();
            using (new EditorGUI.DisabledScope(eventData == null))
            {
                if (GUILayout.Button("複製して作る", GUILayout.Width(100)))
                {
                    var copy = EventFactory.Duplicate(eventData);
                    if (copy != null) Select(copy);
                }
            }
        }
    }

    private void ShowTemplateMenu()
    {
        var menu = new GenericMenu();
        var values = (EventFactory.Template[])System.Enum.GetValues(typeof(EventFactory.Template));
        foreach (var t in values)
        {
            var template = t;
            menu.AddItem(new GUIContent(EventFactory.TemplateNames[(int)template]), false, () =>
            {
                var created = EventFactory.Create(template);
                if (created != null) Select(created);
            });
        }
        menu.ShowAsContext();
    }

    private void Select(EventData e)
    {
        eventData = e;
        Selection.activeObject = e;
        GUI.FocusControl(null);
        Repaint();
    }

    // ─── 選んだイベント ──────────────────────────────────────────
    private void DrawEvent()
    {
        var so = new SerializedObject(eventData);
        so.Update();

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(EventDescriber.Summary(eventData), MessageType.None);

        EventBasicSection.Draw(so, this);
        EventTimingSection.Draw(so);
        EventConditionList.Draw(so.FindProperty("conditions"), eventData);
        EventChoiceSection.Draw(so);
        EventLinkSection.Draw(eventData);

        so.ApplyModifiedProperties();

        DrawChecks();
        DrawPlacement();
        EventPlayModeSection.Draw(eventData);
    }

    private void DrawChecks()
    {
        using (new SupportEditorGUI.BoxScope("チェック"))
        {
            var issues = EventValidator.Validate(eventData);
            if (issues.Count == 0)
            {
                EditorGUILayout.LabelField("✓ 問題は見つかりませんでした", EditorStyles.miniLabel);
                return;
            }
            foreach (var issue in issues)
                EditorGUILayout.HelpBox(issue.message, issue.isError ? MessageType.Error : MessageType.Warning);
        }
    }

    private void DrawPlacement()
    {
        using (new SupportEditorGUI.BoxScope("置き場所"))
        {
            if (EventRegistry.IsInResources(eventData))
            {
                EditorGUILayout.LabelField("✓ Resources/Events にあります（自動で読み込まれます）", EditorStyles.miniLabel);
                return;
            }

            EditorGUILayout.HelpBox("Resources/Events の外にあります。EventManager が読み込みません。", MessageType.Warning);
            if (GUILayout.Button("移動する（" + EventRegistry.DefaultDir + "）"))
            {
                string error = EventRegistry.MoveToResources(eventData);
                if (!string.IsNullOrEmpty(error)) EditorUtility.DisplayDialog("イベント", "移動に失敗しました。\n" + error, "OK");
            }
        }
    }

    // ─── すべてのイベント ────────────────────────────────────────
    private void DrawAll()
    {
        EditorGUILayout.LabelField("すべてのイベント", EditorStyles.boldLabel);
        listMode = GUILayout.Toolbar(listMode, new[] { "一覧", "年表" }, GUILayout.Width(140));

        var picked = listMode == 0 ? EventListSection.Draw(eventData) : EventTimelineView.Draw(eventData);
        if (picked != null) Select(picked);
    }
}

