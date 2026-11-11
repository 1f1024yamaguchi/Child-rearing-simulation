using System.Collections.Generic;
using Maruyama.Events;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 編集ウィンドウの「ストーリーのつながり」欄（5.5）。
/// 来る道（予約元）・行く道（予約先）・このイベントを条件にしているもの・使っているフラグを自動逆引きして表示。
/// ボタンを押すとそのイベントへ移動する。
/// </summary>
public static class EventLinkSection
{
    private static bool isExpanded = true;

    public static void Draw(EventData e)
    {
        if (e == null) return;

        using (new SupportEditorGUI.BoxScope("ストーリーのつながり"))
        {
            isExpanded = EditorGUILayout.Foldout(isExpanded, "前後の展開・フラグの逆引き", true);
            if (!isExpanded) return;

            // 1. このイベントに来る道
            DrawIncomingLinks(e);

            EditorGUILayout.Space(4);

            // 2. このイベントから行く道
            DrawOutgoingLinks(e);

            EditorGUILayout.Space(4);

            // 3. このイベントを条件にしているもの
            DrawConditionUsers(e);

            EditorGUILayout.Space(4);

            // 4. 使っているフラグ
            DrawFlagsUsed(e);
        }
    }

    private static void DrawIncomingLinks(EventData e)
    {
        EditorGUILayout.LabelField("このイベントに来る道", EditorStyles.miniBoldLabel);
        var callers = EventRegistry.FindCallersOf(e);
        if (callers.Count == 0)
        {
            if (e.trigger == EventTrigger.FollowUp)
            {
                EditorGUILayout.HelpBox("⚠ どこからも予約されていません（このイベントは発生しません）。別のイベントの選択肢から「続きを予約」で繋いでください。", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField("（なし・抽選または決まった日）", EditorStyles.miniLabel);
            }
        }
        else
        {
            foreach (var c in callers)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    string label = string.IsNullOrEmpty(c.choice.label) ? "（ラベルなし）" : c.choice.label;
                    GUILayout.Label($"← 「{c.sourceEvent.title}」で「{label}」（{c.days}日後）", EditorStyles.miniLabel);
                    if (GUILayout.Button("移動", EditorStyles.miniButton, GUILayout.Width(44)))
                    {
                        EventEditorWindow.SelectEvent(c.sourceEvent);
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }
    }

    private static void DrawOutgoingLinks(EventData e)
    {
        EditorGUILayout.LabelField("このイベントから行く道", EditorStyles.miniBoldLabel);
        var followUps = EventRegistry.FindFollowUpsOf(e);
        if (followUps.Count == 0)
        {
            EditorGUILayout.LabelField("（なし）", EditorStyles.miniLabel);
        }
        else
        {
            foreach (var f in followUps)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    string label = string.IsNullOrEmpty(f.choice.label) ? "（ラベルなし）" : f.choice.label;
                    GUILayout.Label($"→ 「{f.targetEvent.title}」（{label}・{f.days}日後）", EditorStyles.miniLabel);
                    if (GUILayout.Button("移動", EditorStyles.miniButton, GUILayout.Width(44)))
                    {
                        EventEditorWindow.SelectEvent(f.targetEvent);
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }
    }

    private static void DrawConditionUsers(EventData e)
    {
        EditorGUILayout.LabelField("このイベントを条件にしているもの", EditorStyles.miniBoldLabel);
        var users = EventRegistry.FindConditionUsersOf(e);
        if (users.Count == 0)
        {
            EditorGUILayout.LabelField("（なし）", EditorStyles.miniLabel);
        }
        else
        {
            foreach (var u in users)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    string condDesc = u.condition != null ? u.condition.Describe() : "条件";
                    GUILayout.Label($"・「{u.userEvent.title}」（{condDesc}）", EditorStyles.miniLabel);
                    if (GUILayout.Button("移動", EditorStyles.miniButton, GUILayout.Width(44)))
                    {
                        EventEditorWindow.SelectEvent(u.userEvent);
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }
    }

    private static void DrawFlagsUsed(EventData e)
    {
        EditorGUILayout.LabelField("使っているフラグ", EditorStyles.miniBoldLabel);

        var flags = new HashSet<StoryFlag>();

        // 選択肢で立てる・消すフラグ
        if (e.choices != null)
        {
            foreach (var c in e.choices)
            {
                if (c == null || c.effects == null) continue;
                foreach (var ef in c.effects)
                {
                    if (ef != null && (ef.kind == EffectKind.SetFlag || ef.kind == EffectKind.ClearFlag))
                    {
                        if (ef.asset is StoryFlag sf) flags.Add(sf);
                    }
                }
            }
        }

        // 条件で見ているフラグ
        if (e.conditions != null)
        {
            foreach (var cond in e.conditions)
            {
                if (cond is StoryFlagCondition sfc && sfc.flag != null)
                {
                    flags.Add(sfc.flag);
                }
            }
        }

        if (flags.Count == 0)
        {
            EditorGUILayout.LabelField("（なし）", EditorStyles.miniLabel);
        }
        else
        {
            foreach (var flag in flags)
            {
                if (flag == null) continue;
                var usage = EventRegistry.FindFlagUsers(flag);

                var setNames = new List<string>();
                foreach (var s in usage.setters) setNames.Add(s == e ? "ここ" : $"「{s.title}」");

                var seeNames = new List<string>();
                foreach (var u in usage.conditionUsers) seeNames.Add(u == e ? "ここ" : $"「{u.title}」");

                string setStr = setNames.Count > 0 ? string.Join("・", setNames) : "（立てる場所なし⚠）";
                string seeStr = seeNames.Count > 0 ? string.Join("・", seeNames) : "（見る場所なし）";

                EditorGUILayout.LabelField($"「{flag.displayName}」({flag.id}): 立てる＝{setStr} ／ 見る＝{seeStr}", EditorStyles.miniLabel);
            }
        }
    }
}
