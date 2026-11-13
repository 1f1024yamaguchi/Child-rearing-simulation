using Maruyama.Events;
using UnityEditor;
using UnityEngine;

/// <summary>編集ウィンドウの「いつ出るか」欄：抽選（重み・クールダウン）／決まった日（1回だけ・毎年）。</summary>
public static class EventTimingSection
{
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
    private static readonly int[] CooldownPresetDays = { 0, 30, 91, 182, 365, 730 };
    private static readonly string[] CooldownPresetNames =
    {
        "なし（制限しない）", "1ヶ月", "3ヶ月", "半年", "1年", "2年", "日数を自分で決める",
    };
    private static readonly System.Collections.Generic.HashSet<int> customCooldown = new System.Collections.Generic.HashSet<int>();

    private static void DrawRandom(SerializedObject so, EventData e)
    {
        EditorGUILayout.LabelField("毎月の抽選で、条件を満たす候補の中から「重み」に比例して1つ選ばれます。", EditorStyles.wordWrappedMiniLabel);

        DrawWeight(so, e);
        EditorGUILayout.Space(2);
        DrawCooldown(so, e);
        EditorGUILayout.Space(2);
        DrawOnceInLifetime(so, e);
    }

    private static void DrawWeight(SerializedObject so, EventData e)
    {
        var weight = so.FindProperty("weight");
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel(new GUIContent("出やすさ（重み）", "同じ種類（良い／悪い）の候補どうしで比べた、選ばれやすさ。大きいほど選ばれやすい。0 なら選ばれない。ふつうは 1。"));
            float w = EditorGUILayout.FloatField(weight.floatValue, GUILayout.Width(60));
            if (w != weight.floatValue) weight.floatValue = Mathf.Max(0f, w);
            if (GUILayout.Button("めったに出ない", EditorStyles.miniButton)) weight.floatValue = 0.3f;
            if (GUILayout.Button("ふつう", EditorStyles.miniButton)) weight.floatValue = 1f;
            if (GUILayout.Button("よく出る", EditorStyles.miniButton)) weight.floatValue = 3f;
        }
        EditorGUILayout.LabelField(" ", WeightShareText(e, weight.floatValue), EditorStyles.wordWrappedMiniLabel);
    }

    /// <summary>同じ種類の抽選イベントの重みを足して、このイベントの割合を出す（条件は見ない、おおよその目安）。</summary>
    private static string WeightShareText(EventData e, float w)
    {
        if (w <= 0f) return "重みが 0 なので、抽選では選ばれません。";

        float total = 0f;
        int count = 0;
        bool hasSelf = false;
        foreach (var ev in EventRegistry.All())
        {
            if (ev == null || ev.trigger != EventTrigger.Random || ev.category != e.category) continue;
            float ew = ev == e ? w : ev.weight;
            if (ew <= 0f) continue;
            total += ew;
            count++;
            if (ev == e) hasSelf = true;
        }
        if (!hasSelf) { total += w; count++; }

        string kind = e.category == EventCategory.Demerit ? "デメリット" : "メリット";
        if (count <= 1) return $"同じ{kind}の抽選イベントは、これだけです（条件を満たせば必ず選ばれます）。";
        int pct = Mathf.RoundToInt(w / total * 100f);
        return $"目安：{kind}の抽選イベント {count} 件が全部出られるとき、約 {pct}%。条件で出られないイベントが多いほど、この数字は上がります。";
    }

    private static void DrawCooldown(SerializedObject so, EventData e)
    {
        var cooldown = so.FindProperty("cooldownDays");
        int id = e.GetInstanceID();
        int days = cooldown.intValue;

        int preset = System.Array.IndexOf(CooldownPresetDays, days);
        bool custom = customCooldown.Contains(id) || preset < 0;
        int popupIndex = custom ? CooldownPresetNames.Length - 1 : preset;

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel(new GUIContent("出たあと、しばらく出さない", "一度起きたら、この期間は同じイベントを選ばない。「なし」なら毎月の候補に毎回入る。"));
            int next = EditorGUILayout.Popup(popupIndex, CooldownPresetNames);
            if (next != popupIndex)
            {
                if (next == CooldownPresetNames.Length - 1)
                {
                    customCooldown.Add(id);
                    if (days <= 0) cooldown.intValue = 30;
                }
                else
                {
                    customCooldown.Remove(id);
                    cooldown.intValue = CooldownPresetDays[next];
                }
            }
        }

        if (custom)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("日数");
                int c = EditorGUILayout.IntField(cooldown.intValue, GUILayout.Width(60));
                if (c != cooldown.intValue) cooldown.intValue = Mathf.Max(0, c);
                GUILayout.Label("日（30日 ≒ 1ヶ月）", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
            }
        }

        int d = cooldown.intValue;
        string note = d <= 0
            ? "制限なし：毎月の抽選に毎回入ります（ただし直前に出たものは、他に候補があれば外されます）。"
            : $"一度出たら、{EventDescriber.CooldownText(d)}のあいだは抽選に入りません。";
        EditorGUILayout.LabelField(" ", note, EditorStyles.wordWrappedMiniLabel);
    }

    private static void DrawOnceInLifetime(SerializedObject so, EventData e)
    {
        ChoiceHistoryCondition cond = FindOnceInLifetimeCondition(e);
        bool current = cond != null;

        EditorGUI.BeginChangeCheck();
        bool next = EditorGUILayout.Toggle(new GUIContent("一生に1回だけ", "ON にすると、一度起きたあとは二度と出ません（「このイベントが起きていない」条件を自動で足します）。"), current);
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

