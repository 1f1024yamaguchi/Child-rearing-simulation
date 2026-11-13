using System;
using System.Collections.Generic;
using Maruyama.Core;
using Maruyama.Events;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 編集ウィンドウの「選択肢」欄。選択肢を折りたたみで並べ、通常の効果・ストーリー欄（フラグ・趣味・特性・続き）・家計簿の種類を編集する。
/// </summary>
public static class EventChoiceSection
{

    public static void Draw(SerializedObject so)
    {
        var choices = so.FindProperty("choices");
        using (new SupportEditorGUI.BoxScope($"選択肢（{choices.arraySize}）"))
        {
            for (int i = 0; i < choices.arraySize; i++)
            {
                if (DrawChoice(choices, i))
                {
                    // 並べ替え・削除をしたので、この回の描画はやめる。
                    // 削除した選択肢の「出す条件」がアセットの中に残らないよう、反映後に掃除する
                    EventConditionList.CleanupUnreferencedLater(so.targetObject);
                    break;
                }
            }

            if (GUILayout.Button("＋選択肢", GUILayout.Width(90)))
            {
                int n = choices.arraySize;
                choices.arraySize = n + 1;
                string newId = GetNextChoiceId(choices, n);
                ResetChoice(choices.GetArrayElementAtIndex(n), newId);
                choices.GetArrayElementAtIndex(n).isExpanded = true;
            }
        }
    }

    private static string GetNextChoiceId(SerializedProperty choices, int newIndex)
    {
        var used = new HashSet<string>();
        for (int i = 0; i < choices.arraySize; i++)
        {
            if (i == newIndex) continue;
            var idProp = choices.GetArrayElementAtIndex(i).FindPropertyRelative("id");
            if (idProp != null && !string.IsNullOrEmpty(idProp.stringValue))
                used.Add(idProp.stringValue);
        }

        for (int num = 1; num <= 100; num++)
        {
            string candidate = $"c{num}";
            if (!used.Contains(candidate)) return candidate;
        }
        return $"c{newIndex + 1}";
    }

    // arraySize を増やすと直前の要素の中身がコピーされるので、空の選択肢に戻す
    private static void ResetChoice(SerializedProperty choice, string newId = "")
    {
        choice.FindPropertyRelative("id").stringValue = newId;
        choice.FindPropertyRelative("label").stringValue = "";
        choice.FindPropertyRelative("effects").arraySize = 0;
        var conds = choice.FindPropertyRelative("showConditions");
        if (conds != null) conds.arraySize = 0;
        choice.FindPropertyRelative("ledgerCategory").intValue = (int)ExpenseCategory.その他;
        choice.FindPropertyRelative("insuredMedicalExpense").boolValue = false;
    }

    // ─── 選択肢1つ ───────────────────────────────────────────────
    private static bool DrawChoice(SerializedProperty choices, int i)
    {
        var choice = choices.GetArrayElementAtIndex(i);
        var label = choice.FindPropertyRelative("label");
        var id = choice.FindPropertyRelative("id");

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            bool changedArray;
            using (new EditorGUILayout.HorizontalScope())
            {
                string idText = string.IsNullOrEmpty(id.stringValue) ? "ID未設定" : id.stringValue;
                string title = string.IsNullOrEmpty(label.stringValue) ? "（ラベルなし）" : label.stringValue;
                choice.isExpanded = EditorGUILayout.Foldout(choice.isExpanded, $"選択肢{i + 1} [{idText}]：{title}", true);
                changedArray = SupportEditorGUI.ArrayButtons(choices, i, "選択肢");
            }
            if (changedArray) return true;

            if (!choice.isExpanded) return false;

            EditorGUILayout.PropertyField(id, new GUIContent("ID", "選択肢の識別ID（ストーリー分岐・履歴記録用）"));
            EditorGUILayout.PropertyField(label, new GUIContent("ラベル"));

            var showConds = choice.FindPropertyRelative("showConditions");
            if (showConds != null)
            {
                var host = choices.serializedObject.targetObject;
                EventConditionList.Draw(showConds, host, "出す条件（任意・すべて満たしたとき押せる）");
            }

            var effects = choice.FindPropertyRelative("effects");
            DrawEffects(effects);
            DrawStorySection(effects);
            DrawChipPreview(choice);

            EditorGUILayout.PropertyField(choice.FindPropertyRelative("ledgerCategory"), new GUIContent("家計簿の種類"));
            EditorGUILayout.PropertyField(choice.FindPropertyRelative("insuredMedicalExpense"), new GUIContent("保険診療の自己負担"));
        }
        return false;
    }

    private static readonly EffectKind[] NormalKinds = new[]
    {
        EffectKind.Money,
        EffectKind.Stress,
        EffectKind.Health,
        EffectKind.Grades,
        EffectKind.Hobby,
        EffectKind.GoodEventRate,
        EffectKind.BadEventRate,
        EffectKind.AddCondition,
    };

    private static readonly string[] NormalKindNames = new[]
    {
        "お金",
        "ストレス",
        "体調",
        "成績",
        "趣味",
        "いいイベント率(%)",
        "悪いイベント率(%)",
        "状態を付ける",
    };

    private static bool IsStoryKind(EffectKind kind)
    {
        return kind == EffectKind.SetFlag ||
               kind == EffectKind.ClearFlag ||
               kind == EffectKind.SetHobby ||
               kind == EffectKind.AddTrait ||
               kind == EffectKind.ScheduleEvent ||
               kind == EffectKind.ShowStory;
    }

    // ─── 効果のリスト（通常の効果のみ） ─────────────────────────
    private static void DrawEffects(SerializedProperty effects)
    {
        EditorGUILayout.LabelField("効果（種類・対象・量・長さ）", EditorStyles.miniBoldLabel);

        for (int k = 0; k < effects.arraySize; k++)
        {
            var ef = effects.GetArrayElementAtIndex(k);
            var kind = ef.FindPropertyRelative("kind");
            var kindVal = (EffectKind)kind.intValue;

            // ストーリー欄で扱う効果は通常リストには出さない
            if (IsStoryKind(kindVal)) continue;

            var duration = ef.FindPropertyRelative("duration");

            bool remove;
            using (new EditorGUILayout.HorizontalScope())
            {
                int currentKindIndex = Array.IndexOf(NormalKinds, kindVal);
                if (currentKindIndex < 0) currentKindIndex = 0;
                int newKindIndex = EditorGUILayout.Popup(currentKindIndex, NormalKindNames, GUILayout.Width(120));
                if (newKindIndex != currentKindIndex && newKindIndex >= 0 && newKindIndex < NormalKinds.Length)
                {
                    kind.intValue = (int)NormalKinds[newKindIndex];
                }

                SupportEditorGUI.EnumPopup(ef.FindPropertyRelative("target"), null, SupportEditorGUI.TargetNames, GUILayout.Width(76));

                // 量：お金のときは円で表示（ラベルの場所を取らない）
                var value = ef.FindPropertyRelative("value");
                if (kind.intValue == (int)EffectKind.Money)
                {
                    float oldWidth = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = 1f;
                    SupportEditorGUI.DrawYen(value, " ");
                    EditorGUIUtility.labelWidth = oldWidth;
                }
                else
                {
                    float v = EditorGUILayout.FloatField(value.floatValue);
                    if (v != value.floatValue) value.floatValue = v;
                }

                SupportEditorGUI.EnumPopup(duration, null, SupportEditorGUI.DurationNames, GUILayout.Width(96));
                if (duration.intValue == (int)EffectDuration.Days)
                {
                    var days = ef.FindPropertyRelative("days");
                    int d = EditorGUILayout.IntField(days.intValue, GUILayout.Width(44));
                    if (d != days.intValue) days.intValue = Mathf.Max(0, d);
                    GUILayout.Label("日", GUILayout.Width(18));
                }
                remove = GUILayout.Button("✕", GUILayout.Width(24));
            }

            if (remove)
            {
                effects.DeleteArrayElementAtIndex(k);
                break;
            }
        }

        if (GUILayout.Button("＋効果", GUILayout.Width(70)))
        {
            int n = effects.arraySize;
            effects.arraySize = n + 1;
            var ef = effects.GetArrayElementAtIndex(n);
            ef.FindPropertyRelative("kind").intValue = (int)EffectKind.Money;
            ef.FindPropertyRelative("target").intValue = (int)EffectTarget.Household;
            ef.FindPropertyRelative("value").floatValue = 0f;
            ef.FindPropertyRelative("duration").intValue = (int)EffectDuration.Instant;
            ef.FindPropertyRelative("days").intValue = 0;
            ef.FindPropertyRelative("asset").objectReferenceValue = null;
        }
    }

    // ─── 選択肢の「ストーリー」欄 ───────────────────────────────
    private static void DrawStorySection(SerializedProperty effects)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("ストーリー（フラグ・趣味・特性・続き）", EditorStyles.boldLabel);

            DrawFlagField(effects, EffectKind.SetFlag, "フラグを立てる");
            DrawFlagField(effects, EffectKind.ClearFlag, "フラグを消す");
            DrawHobbyField(effects);
            DrawTraitField(effects);
            DrawScheduleField(effects);
            DrawShowStoryField(effects);
        }
    }

    private static void DrawFlagField(SerializedProperty effects, EffectKind kind, string label)
    {
        int idx = FindEffectIndex(effects, kind);
        var curFlag = idx >= 0 ? effects.GetArrayElementAtIndex(idx).FindPropertyRelative("asset").objectReferenceValue as StoryFlag : null;
        var newFlag = EditorGUILayout.ObjectField(label, curFlag, typeof(StoryFlag), false) as StoryFlag;
        if (newFlag != curFlag)
        {
            if (newFlag != null)
            {
                if (idx < 0) idx = AppendEffectDirect(effects, kind, EffectTarget.Household);
                effects.GetArrayElementAtIndex(idx).FindPropertyRelative("asset").objectReferenceValue = newFlag;
            }
            else
            {
                if (idx >= 0) effects.DeleteArrayElementAtIndex(idx);
            }
        }
    }

    private static readonly string[] HobbyOptionNames = { "（変更しない）", "スポーツ", "音楽", "芸術", "ゲーム", "読書" };
    private static readonly HobbyKind[] HobbyOptionValues = { HobbyKind.なし, HobbyKind.スポーツ, HobbyKind.音楽, HobbyKind.芸術, HobbyKind.ゲーム, HobbyKind.読書 };

    private static void DrawHobbyField(SerializedProperty effects)
    {
        int idx = FindEffectIndex(effects, EffectKind.SetHobby);
        int currentOption = 0;
        if (idx >= 0)
        {
            var curHobby = (HobbyKind)Mathf.RoundToInt(effects.GetArrayElementAtIndex(idx).FindPropertyRelative("value").floatValue);
            for (int i = 1; i < HobbyOptionValues.Length; i++)
            {
                if (HobbyOptionValues[i] == curHobby) { currentOption = i; break; }
            }
        }

        int newOption = EditorGUILayout.Popup("趣味を決める", currentOption, HobbyOptionNames);
        if (newOption != currentOption)
        {
            if (newOption == 0)
            {
                if (idx >= 0) effects.DeleteArrayElementAtIndex(idx);
            }
            else
            {
                if (idx < 0) idx = AppendEffectDirect(effects, EffectKind.SetHobby, EffectTarget.Child);
                effects.GetArrayElementAtIndex(idx).FindPropertyRelative("value").floatValue = (float)HobbyOptionValues[newOption];
            }
        }
    }

    private static void DrawTraitField(SerializedProperty effects)
    {
        int idx = FindEffectIndex(effects, EffectKind.AddTrait);
        var curTrait = idx >= 0 ? effects.GetArrayElementAtIndex(idx).FindPropertyRelative("asset").objectReferenceValue as TraitDefinition : null;
        var newTrait = EditorGUILayout.ObjectField("特性を付ける", curTrait, typeof(TraitDefinition), false) as TraitDefinition;
        if (newTrait != curTrait)
        {
            if (newTrait != null)
            {
                if (idx < 0) idx = AppendEffectDirect(effects, EffectKind.AddTrait, EffectTarget.Child);
                effects.GetArrayElementAtIndex(idx).FindPropertyRelative("asset").objectReferenceValue = newTrait;
            }
            else
            {
                if (idx >= 0) effects.DeleteArrayElementAtIndex(idx);
            }
        }
    }

    private static void DrawScheduleField(SerializedProperty effects)
    {
        int idx = FindEffectIndex(effects, EffectKind.ScheduleEvent);
        var curEvent = idx >= 0 ? effects.GetArrayElementAtIndex(idx).FindPropertyRelative("asset").objectReferenceValue as EventData : null;
        int curDays = idx >= 0 ? effects.GetArrayElementAtIndex(idx).FindPropertyRelative("days").intValue : 180;
        if (curDays <= 0) curDays = 180;

        using (new EditorGUILayout.HorizontalScope())
        {
            var newEvent = EditorGUILayout.ObjectField("続きを予約", curEvent, typeof(EventData), false) as EventData;
            if (GUILayout.Button("＋新しく作る", EditorStyles.miniButton, GUILayout.Width(84)))
            {
                var created = EventFactory.Create(EventFactory.Template.FollowUp);
                if (created != null)
                {
                    newEvent = created;
                }
            }

            if (newEvent != null)
            {
                GUILayout.Label("日後:", GUILayout.Width(35));
                int newDays = EditorGUILayout.IntField(curDays, GUILayout.Width(44));
                if (newDays != curDays && idx >= 0)
                {
                    effects.GetArrayElementAtIndex(idx).FindPropertyRelative("days").intValue = Mathf.Max(1, newDays);
                }
            }

            if (newEvent != curEvent)
            {
                if (newEvent != null)
                {
                    if (idx < 0) idx = AppendEffectDirect(effects, EffectKind.ScheduleEvent, EffectTarget.Household);
                    effects.GetArrayElementAtIndex(idx).FindPropertyRelative("asset").objectReferenceValue = newEvent;
                    effects.GetArrayElementAtIndex(idx).FindPropertyRelative("days").intValue = curDays;
                }
                else
                {
                    if (idx >= 0) effects.DeleteArrayElementAtIndex(idx);
                }
            }
        }
    }

    private static void DrawShowStoryField(SerializedProperty effects)
    {
        int idx = FindEffectIndex(effects, EffectKind.ShowStory);
        var curTut = idx >= 0 ? effects.GetArrayElementAtIndex(idx).FindPropertyRelative("asset").objectReferenceValue as TutorialData : null;
        var newTut = EditorGUILayout.ObjectField("ストーリーを出す", curTut, typeof(TutorialData), false) as TutorialData;
        if (newTut != curTut)
        {
            if (newTut != null)
            {
                if (idx < 0) idx = AppendEffectDirect(effects, EffectKind.ShowStory, EffectTarget.Household);
                effects.GetArrayElementAtIndex(idx).FindPropertyRelative("asset").objectReferenceValue = newTut;
            }
            else
            {
                if (idx >= 0) effects.DeleteArrayElementAtIndex(idx);
            }
        }
    }

    private static int FindEffectIndex(SerializedProperty effects, EffectKind kind)
    {
        for (int i = 0; i < effects.arraySize; i++)
        {
            if (effects.GetArrayElementAtIndex(i).FindPropertyRelative("kind").intValue == (int)kind)
                return i;
        }
        return -1;
    }

    private static int AppendEffectDirect(SerializedProperty effects, EffectKind kind, EffectTarget target)
    {
        int n = effects.arraySize;
        effects.arraySize = n + 1;
        var ef = effects.GetArrayElementAtIndex(n);
        ef.FindPropertyRelative("kind").intValue = (int)kind;
        ef.FindPropertyRelative("target").intValue = (int)target;
        ef.FindPropertyRelative("value").floatValue = 0f;
        ef.FindPropertyRelative("duration").intValue = (int)EffectDuration.Instant;
        ef.FindPropertyRelative("days").intValue = 0;
        ef.FindPropertyRelative("asset").objectReferenceValue = null;
        return n;
    }

    // ─── カードに出るチップの見た目 ──────────────────────────────
    private static void DrawChipPreview(SerializedProperty choice)
    {
        // 文字は実データ（保存済みの値）から作る。入力した次の描画で追いつく。
        var e = (EventData)choice.serializedObject.targetObject;
        int idx = ChoiceIndex(choice);
        if (e == null || e.choices == null || idx < 0 || idx >= e.choices.Count) return;

        var c = e.choices[idx];
        var chips = EventDescriber.ChipTexts(c);
        string chipLine = chips.Count == 0 ? "（チップなし）" : string.Join(" ／ ", chips);
        EditorGUILayout.LabelField("カードのチップ", chipLine, EditorStyles.miniLabel);

        string money = EventDescriber.MoneyButtonText(c);
        if (money != null)
            EditorGUILayout.LabelField("お金はチップではなくボタンの文字に付きます", "「" + money + "」", EditorStyles.miniLabel);
    }

    private static int ChoiceIndex(SerializedProperty choice)
    {
        // propertyPath は choices.Array.data[N]
        string path = choice.propertyPath;
        int open = path.LastIndexOf('[');
        int close = path.LastIndexOf(']');
        if (open < 0 || close < open) return -1;
        return int.TryParse(path.Substring(open + 1, close - open - 1), out int n) ? n : -1;
    }
}

