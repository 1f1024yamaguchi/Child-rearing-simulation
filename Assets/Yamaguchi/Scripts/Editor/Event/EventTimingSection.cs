using Maruyama.Events;
using UnityEditor;
using UnityEngine;

/// <summary>編集ウィンドウの「いつ出るか」欄：抽選（重み・クールダウン）／決まった日（1回だけ・毎年）。</summary>
public static class EventTimingSection
{
    private const int HalfYearDays = 182;
    private const int YearDays = 365;
    private const int MaxAge = 19; // GameDate のユーティリティ（SupportEditorGUI.DrawGameDay）と同じ上限

    public static void Draw(SerializedObject so)
    {
        using (new SupportEditorGUI.BoxScope("いつ出るか"))
        {
            var e = (EventData)so.targetObject;
            var trigger = so.FindProperty("trigger");
            int cur = trigger.intValue;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("出方");
                DrawTriggerRadio(trigger, (int)EventTrigger.Random, "抽選", 55);
                DrawTriggerRadio(trigger, (int)EventTrigger.Fixed, "決まった日", 75);
                DrawTriggerRadio(trigger, (int)EventTrigger.FollowUp, "続き", 55);
                DrawTriggerRadio(trigger, (int)EventTrigger.OnCondition, "条件を満たしたら", 110);
                DrawTriggerRadio(trigger, (int)EventTrigger.Manual, "コードから", 75);
                GUILayout.FlexibleSpace();
            }

            switch ((EventTrigger)trigger.intValue)
            {
                case EventTrigger.Random:
                    DrawRandom(so, e);
                    break;
                case EventTrigger.Fixed:
                    DrawFixed(so, e);
                    break;
                case EventTrigger.FollowUp:
                    DrawFollowUp(so, e);
                    break;
                case EventTrigger.OnCondition:
                    DrawOnCondition(so, e);
                    break;
                case EventTrigger.Manual:
                    DrawManual(so, e);
                    break;
            }
        }
    }

    private static void DrawTriggerRadio(SerializedProperty trigger, int val, string label, float width)
    {
        bool on = trigger.intValue == val;
        if (GUILayout.Toggle(on, label, EditorStyles.radioButton, GUILayout.Width(width)) && !on)
        {
            trigger.intValue = val;
        }
    }

    // ─── 抽選 ────────────────────────────────────────────────────
    private static void DrawRandom(SerializedObject so, EventData e)
    {
        var weight = so.FindProperty("weight");
        float w = EditorGUILayout.FloatField(new GUIContent("重み", "大きいほど選ばれやすい。0 なら選ばれない。"), weight.floatValue);
        if (w != weight.floatValue) weight.floatValue = Mathf.Max(0f, w);

        var cooldown = so.FindProperty("cooldownDays");
        using (new EditorGUILayout.HorizontalScope())
        {
            int c = EditorGUILayout.IntField(new GUIContent("クールダウン（日）", "一度起きたら、この日数のあいだは同じイベントを選ばない。0 なら制限なし。"), cooldown.intValue);
            if (c != cooldown.intValue) cooldown.intValue = Mathf.Max(0, c);
            if (GUILayout.Button("半年", GUILayout.Width(44))) cooldown.intValue = HalfYearDays;
            if (GUILayout.Button("1年", GUILayout.Width(44))) cooldown.intValue = YearDays;
        }
        EditorGUILayout.LabelField(" ", "＝ " + EventDescriber.CooldownText(cooldown.intValue), EditorStyles.miniLabel);

        DrawOnceInLifetime(so, e);
    }

    private static void DrawOnceInLifetime(SerializedObject so, EventData e)
    {
        ChoiceHistoryCondition cond = FindOnceInLifetimeCondition(e);
        bool current = cond != null;

        EditorGUI.BeginChangeCheck();
        bool next = EditorGUILayout.Toggle(new GUIContent("一生に1回だけ", "ON にすると「このイベントが起きていない」条件を自動で追加し、一度発生した後は抽選されなくなります。"), current);
        if (EditorGUI.EndChangeCheck() && next != current)
        {
            if (next)
            {
                var instance = ScriptableObject.CreateInstance<ChoiceHistoryCondition>();
                instance.name = "Cond_OnceInLifetime";
                instance.targetEvent = e;
                instance.checkType = HistoryCheckType.起きていない;
                AssetDatabase.AddObjectToAsset(instance, e);

                var list = so.FindProperty("conditions");
                int n = list.arraySize;
                list.arraySize = n + 1;
                list.GetArrayElementAtIndex(n).objectReferenceValue = instance;
                so.ApplyModifiedProperties();

                EditorUtility.SetDirty(e);
                AssetDatabase.SaveAssets();
                EventRegistry.Invalidate();
            }
            else
            {
                var list = so.FindProperty("conditions");
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == cond)
                    {
                        list.GetArrayElementAtIndex(i).objectReferenceValue = null;
                        list.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }
                so.ApplyModifiedProperties();

                if (cond != null && AssetDatabase.IsSubAsset(cond))
                {
                    AssetDatabase.RemoveObjectFromAsset(cond);
                    UnityEngine.Object.DestroyImmediate(cond, true);
                }
                EditorUtility.SetDirty(e);
                AssetDatabase.SaveAssets();
                EventRegistry.Invalidate();
            }
        }
    }

    private static ChoiceHistoryCondition FindOnceInLifetimeCondition(EventData e)
    {
        if (e == null || e.conditions == null) return null;
        foreach (var c in e.conditions)
        {
            if (c is ChoiceHistoryCondition hist && hist.targetEvent == e && hist.checkType == HistoryCheckType.起きていない)
                return hist;
        }
        return null;
    }

    // ─── 決まった日 ──────────────────────────────────────────────
    private static void DrawFixed(SerializedObject so, EventData e)
    {
        var min = so.FindProperty("fixedMinAgeYears");
        var max = so.FindProperty("fixedMaxAgeYears");

        // どちらの形かは「最小と最大が同じか」で決める（データには持たない）
        bool once = min.intValue == max.intValue;

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel("回数");
            if (GUILayout.Toggle(once, "1回だけ", EditorStyles.radioButton, GUILayout.Width(70)) && !once)
                max.intValue = min.intValue;
            if (GUILayout.Toggle(!once, "毎年", EditorStyles.radioButton, GUILayout.Width(60)) && once)
                max.intValue = Mathf.Min(MaxAge, min.intValue + 1);
            GUILayout.FlexibleSpace();
        }

        if (once)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("何歳のとき");
                int age = EditorGUILayout.IntField(min.intValue, GUILayout.Width(44));
                GUILayout.Label("歳", GUILayout.Width(18));
                if (age != min.intValue)
                {
                    age = Mathf.Clamp(age, 0, MaxAge);
                    min.intValue = age;
                    max.intValue = age;
                }
                GUILayout.FlexibleSpace();
            }
        }
        else
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("毎年の範囲");
                int lo = EditorGUILayout.IntField(min.intValue, GUILayout.Width(44));
                GUILayout.Label("歳〜", GUILayout.Width(30));
                int hi = EditorGUILayout.IntField(max.intValue, GUILayout.Width(44));
                GUILayout.Label("歳", GUILayout.Width(18));
                if (lo != min.intValue) min.intValue = Mathf.Clamp(lo, 0, MaxAge);
                if (hi != max.intValue) max.intValue = Mathf.Clamp(hi, 0, MaxAge);
                GUILayout.Label("（同じ歳にすると「1回だけ」になります）", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
            }
        }

        // 日付（月を変えたとき、その月に無い日は日数の上限に直る）
        SupportEditorGUI.DrawMonthDay(so.FindProperty("fixedMonth"), so.FindProperty("fixedDay"), "月日");

        EditorGUILayout.LabelField(" ", "→ " + EventDescriber.TimingText(e), EditorStyles.miniLabel);
    }

    // ─── 続き ────────────────────────────────────────────────────
    private static void DrawFollowUp(SerializedObject so, EventData e)
    {
        EditorGUILayout.LabelField("このイベントは別のイベントの選択肢から「続きを予約」されたときに出ます。", EditorStyles.wordWrappedMiniLabel);

        var callers = EventRegistry.FindCallersOf(e);
        if (callers.Count == 0)
        {
            EditorGUILayout.HelpBox("⚠ このイベントを予約している選択肢がありません（どこからも発生しません）。別のイベントの選択肢から「続きを予約」で繋いでください。", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.LabelField($"予約している選択肢（{callers.Count}件）:", EditorStyles.miniBoldLabel);
            foreach (var caller in callers)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    string label = string.IsNullOrEmpty(caller.choice.label) ? "（ラベルなし）" : caller.choice.label;
                    GUILayout.Label($"・「{caller.sourceEvent.title}」の選択肢「{label}」 （{caller.days}日後）", EditorStyles.miniLabel);
                    if (GUILayout.Button("移動", EditorStyles.miniButton, GUILayout.Width(44)))
                    {
                        EventEditorWindow.SelectEvent(caller.sourceEvent);
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }
    }

    // ─── 条件を満たしたら ────────────────────────────────────────
    private static void DrawOnCondition(SerializedObject so, EventData e)
    {
        EditorGUILayout.LabelField("条件を満たした最初の日に1回だけ出ます（一生に1回）。", EditorStyles.wordWrappedMiniLabel);
        DrawOnceInLifetime(so, e);
    }

    // ─── コードから ──────────────────────────────────────────────
    private static void DrawManual(SerializedObject so, EventData e)
    {
        EditorGUILayout.LabelField("自動では出ません。コードから EventManager.Instance.Enqueue を呼んだときだけ出ます。", EditorStyles.wordWrappedMiniLabel);
    }
}

