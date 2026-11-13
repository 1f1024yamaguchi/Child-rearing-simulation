using Maruyama.Events;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 編集ウィンドウの「再生中だけ」欄。条件の判定と［今すぐ出す（条件を無視）］。
/// EventManager.Enqueue(e) を通して出番待ちに追加する。
/// </summary>
public static class EventPlayModeSection
{
    private static EventManager manager;

    public static void Draw(EventData e)
    {
        // 再生しなくても見られる見た目プレビュー（選んだイベントに合わせて自動で切り替わる）
        EventCardPreview.DrawBox(e);

        if (!EditorApplication.isPlaying)
        {
            DrawEditModeBox(e);
            return;
        }

        if (Event.current.type == EventType.Layout)
        {
            manager = EventManager.Instance != null
                ? EventManager.Instance
                : Object.FindFirstObjectByType<EventManager>(FindObjectsInactive.Include);
        }

        using (new SupportEditorGUI.BoxScope("再生中だけ"))
        {
            DrawConditions(e);
            EditorGUILayout.Space(4);
            DrawShowButton(e);
            EditorGUILayout.Space(6);
            DrawAutoChoiceFix(e);
            EditorGUILayout.Space(6);
            DrawStoryState();
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox("再生中に変えた値は、アセットにそのまま残ります（再生を止めても戻りません）。気になるときは Ctrl+Z か git で戻してください。", MessageType.Warning);
        }
    }

    private static void DrawAutoChoiceFix(EventData e)
    {
        if (e == null || e.choices == null || e.choices.Count == 0) return;
        var pilot = DebugAutoPilot.I;
        if (pilot == null) return;

        EditorGUILayout.LabelField("オート時の選択肢固定", EditorStyles.boldLabel);
        string currentFixed = pilot.GetFixedChoice(e.id);

        for (int i = 0; i < e.choices.Count; i++)
        {
            var c = e.choices[i];
            if (c == null) continue;
            string cId = c.GetOrFallbackId(i);
            bool isFixed = currentFixed == cId;

            using (new EditorGUILayout.HorizontalScope())
            {
                string label = $"{c.label} [{cId}]";
                if (isFixed)
                {
                    GUILayout.Label($"★ {label}（固定中）", EditorStyles.boldLabel);
                    if (GUILayout.Button("解除", EditorStyles.miniButton, GUILayout.Width(50)))
                    {
                        pilot.ClearFixedChoice(e.id);
                    }
                }
                else
                {
                    GUILayout.Label($"  {label}", EditorStyles.miniLabel);
                    if (GUILayout.Button("固定する", EditorStyles.miniButton, GUILayout.Width(60)))
                    {
                        pilot.SetFixedChoice(e.id, cId);
                    }
                }
            }
        }
    }

    private static void DrawStoryState()
    {
        EditorGUILayout.LabelField("ストーリーの状態（見るだけ）", EditorStyles.boldLabel);

        if (StoryState.I == null)
        {
            EditorGUILayout.LabelField("StoryState が見つかりません。", EditorStyles.miniLabel);
            return;
        }

        var data = StoryState.I.CaptureSaveData();

        // 1. 立っているフラグ
        int flagCount = data.flags != null ? data.flags.Count : 0;
        EditorGUILayout.LabelField($"立っているフラグ（{flagCount}件）:", EditorStyles.miniBoldLabel);
        if (flagCount == 0)
        {
            EditorGUILayout.LabelField("  （なし）", EditorStyles.miniLabel);
        }
        else
        {
            var flags = EventRegistry.AllFlags();
            foreach (var fid in data.flags)
            {
                var sf = flags.Find(f => f != null && f.id == fid);
                string name = sf != null ? $"{sf.displayName} ({fid})" : fid;
                EditorGUILayout.LabelField($"  ・{name}", EditorStyles.miniLabel);
            }
        }

        // 2. 選んだ選択肢の履歴
        int choiceCount = data.choices != null ? data.choices.Count : 0;
        EditorGUILayout.LabelField($"選んだ選択肢の履歴（{choiceCount}件）:", EditorStyles.miniBoldLabel);
        if (choiceCount == 0)
        {
            EditorGUILayout.LabelField("  （なし）", EditorStyles.miniLabel);
        }
        else
        {
            foreach (var c in data.choices)
            {
                GameDate.FromAgeDays(c.ageDay, out int y, out int m, out int d);
                EditorGUILayout.LabelField($"  ・{c.eventId}: 選択肢「{c.choiceId}」（{y}歳{m}月{d}日）", EditorStyles.miniLabel);
            }
        }

        // 3. 予約中の続き
        int schedCount = data.scheduled != null ? data.scheduled.Count : 0;
        EditorGUILayout.LabelField($"予約中の続き（{schedCount}件）:", EditorStyles.miniBoldLabel);
        if (schedCount == 0)
        {
            EditorGUILayout.LabelField("  （なし）", EditorStyles.miniLabel);
        }
        else
        {
            int today = GameState.I != null ? Mathf.FloorToInt(GameState.I.AgeDays) : 0;
            foreach (var s in data.scheduled)
            {
                GameDate.FromAgeDays(s.scheduledAgeDay, out int y, out int m, out int d);
                int remain = s.scheduledAgeDay - today;
                string remainStr = remain >= 0 ? $"あと{remain}日" : $"{Mathf.Abs(remain)}日前";
                EditorGUILayout.LabelField($"  ・{s.eventId}（{y}歳{m}月{d}日 / {remainStr}）", EditorStyles.miniLabel);
            }
        }
    }

    private static void DrawConditions(EventData e)
    {
        EditorGUILayout.LabelField("今の条件の判定", EditorStyles.miniBoldLabel);
        if (e.conditions == null || e.conditions.Count == 0)
        {
            EditorGUILayout.LabelField("条件なし（いつでも出せます）", EditorStyles.miniLabel);
            return;
        }

        foreach (var c in e.conditions)
        {
            if (c == null)
            {
                EditorGUILayout.LabelField("✗ （空の行）", EditorStyles.miniLabel);
                continue;
            }
            bool ok;
            try { ok = c.IsSatisfied(); }
            catch (System.Exception ex) { Debug.LogException(ex); ok = false; }
            EditorGUILayout.LabelField((ok ? "✓ " : "✗ ") + EventDescriber.ConditionText(c), EditorStyles.miniLabel);
        }
    }

    private static void DrawShowButton(EventData e)
    {
        if (manager == null)
        {
            EditorGUILayout.HelpBox("シーンに EventManager が見つかりません。", MessageType.Info);
            return;
        }

        if (GUILayout.Button("今すぐ出す（条件を無視・すぐ画面に出す）", GUILayout.Height(26)))
        {
            manager.ShowNow(e);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("出番待ちに入れる（食事選択が閉じたあとに出る）", EditorStyles.miniButton))
            {
                manager.Enqueue(e);
            }
        }
        EditorGUILayout.LabelField("出したあとに選択肢を選ぶと、効果は実際に適用されます。", EditorStyles.miniLabel);
    }

    /// <summary>再生していないとき：再生を始めて、そのイベントを今すぐ画面に出す。</summary>
    private static void DrawEditModeBox(EventData e)
    {
        using (new SupportEditorGUI.BoxScope("実際にゲームで確認（再生が必要）"))
        {
            EditorGUILayout.HelpBox("ボタンの動きや効果の適用まで確かめたいときだけ使います。ゲームを再生して、このイベントを今すぐ画面に出します（条件は無視）。選択肢を選ぶと効果は実際に適用されます。再生を止めればゲームの状態は戻ります。", MessageType.Info);

            bool canRun = e != null && AssetDatabase.Contains(e);
            using (new EditorGUI.DisabledScope(!canRun))
            {
                if (GUILayout.Button("再生して今すぐ出す", GUILayout.Height(26)))
                {
                    EventPreviewLauncher.RequestAndPlay(e);
                }
            }
        }
    }
}
