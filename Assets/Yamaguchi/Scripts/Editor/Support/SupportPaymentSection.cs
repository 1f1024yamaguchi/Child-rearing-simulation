using Maruyama.Core;
using UnityEditor;
using UnityEngine;

/// <summary>お金・効果の欄：申請したときの効果（effects）・毎月の支給表（monthlyTiers）・支給月（payoutMonths）。</summary>
public static class SupportPaymentSection
{
    // ─── もらい方 ────────────────────────────────────────────────
    public static void DrawTiming(SerializedObject so)
    {
        var p = so.FindProperty("effectTiming");
        using (new SupportEditorGUI.BoxScope("もらい方"))
        {
            int cur = p.intValue;
            bool onApply = GUILayout.Toggle(cur == (int)EffectTiming.OnApply, "申請したとき1回（OnApply）", EditorStyles.radioButton);
            bool monthly = GUILayout.Toggle(cur == (int)EffectTiming.Monthly, "受給中ずっと（Monthly）", EditorStyles.radioButton);
            if (onApply && cur != (int)EffectTiming.OnApply) p.intValue = (int)EffectTiming.OnApply;
            else if (monthly && cur != (int)EffectTiming.Monthly) p.intValue = (int)EffectTiming.Monthly;
        }
    }

    // ─── お金・効果 ──────────────────────────────────────────────
    public static void Draw(SerializedObject so, SupportFeatures f, SupportScheme owner)
    {
        using (new SupportEditorGUI.BoxScope("お金・効果"))
        {
            bool onApply = so.FindProperty("effectTiming").intValue == (int)EffectTiming.OnApply;
            EditorGUILayout.LabelField(
                onApply ? "申請したときに起きること" : "申請したとき1回だけ起きること（手間のストレスなど・空でよい）",
                EditorStyles.boldLabel);
            DrawEffects(so.FindProperty("effects"), -1, EffectKind.Money);

            if (f.tiers)
            {
                EditorGUILayout.Space(6);
                DrawTiers(so, owner);
            }

            if (f.payout)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("支給月", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("全部OFF＝毎月支給（下の月は最低1つ。全部なしにしたいときは上の「使う項目」をOFFに）。ONにした月に、それまでの対象月分をまとめて支給します。", EditorStyles.wordWrappedMiniLabel);
                SupportEditorGUI.DrawMonthToggles(so.FindProperty("payoutMonths"), true);
            }
        }
    }

    // ─── 効果のリスト ────────────────────────────────────────────
    /// <summary>1行＝kind / target / value / duration（Days のときだけ days）。skipIndex の行は描かない。</summary>
    public static void DrawEffects(SerializedProperty list, int skipIndex, EffectKind addKind)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            if (i == skipIndex) continue;
            var e = list.GetArrayElementAtIndex(i);
            var kind = e.FindPropertyRelative("kind");
            var value = e.FindPropertyRelative("value");
            var duration = e.FindPropertyRelative("duration");

            bool remove;
            using (new EditorGUILayout.HorizontalScope())
            {
                SupportEditorGUI.EnumPopup(kind, null, SupportEditorGUI.KindNames, GUILayout.Width(120));
                SupportEditorGUI.EnumPopup(e.FindPropertyRelative("target"), null, SupportEditorGUI.TargetNames, GUILayout.Width(80));
                EditorGUILayout.PropertyField(value, GUIContent.none, GUILayout.Width(80));
                if (kind.intValue == (int)EffectKind.Money)
                    GUILayout.Label(SupportEditorGUI.Yen(value.floatValue), GUILayout.Width(90));
                SupportEditorGUI.EnumPopup(duration, null, SupportEditorGUI.DurationNames, GUILayout.Width(100));
                if (duration.intValue == (int)EffectDuration.Days)
                {
                    EditorGUILayout.PropertyField(e.FindPropertyRelative("days"), GUIContent.none, GUILayout.Width(40));
                    GUILayout.Label("日", GUILayout.Width(18));
                }
                GUILayout.FlexibleSpace();
                remove = GUILayout.Button("✕", GUILayout.Width(24));
            }
            if (remove)
            {
                list.DeleteArrayElementAtIndex(i);
                break;
            }
        }

        if (GUILayout.Button("＋ 効果を追加", GUILayout.Width(110)))
            AppendEffect(list, addKind, EffectTarget.Household, 0f);
    }

    /// <summary>効果を1行足す（足すと前の行の中身が複製されるので、全部入れ直す）。</summary>
    public static SerializedProperty AppendEffect(SerializedProperty list, EffectKind kind, EffectTarget target, float value)
    {
        int n = list.arraySize;
        list.arraySize = n + 1;
        var e = list.GetArrayElementAtIndex(n);
        e.FindPropertyRelative("kind").intValue = (int)kind;
        e.FindPropertyRelative("target").intValue = (int)target;
        e.FindPropertyRelative("value").floatValue = value;
        e.FindPropertyRelative("duration").intValue = (int)EffectDuration.Instant;
        e.FindPropertyRelative("days").intValue = 0;
        return e;
    }

    // ─── 毎月の支給表 ────────────────────────────────────────────
    private static void DrawTiers(SerializedObject so, SupportScheme owner)
    {
        EditorGUILayout.LabelField("毎月の支給表", EditorStyles.boldLabel);
        var tiers = so.FindProperty("monthlyTiers");
        for (int i = 0; i < tiers.arraySize; i++)
        {
            if (DrawTier(tiers, i, owner)) break;   // 並べ替え・削除をしたら、この回の描画はここまで
        }
        if (GUILayout.Button("＋ 段を追加", GUILayout.Height(24))) AppendTier(tiers);

        // 年齢ごとの月額の表
        var scheme = so.targetObject as SupportScheme;
        if (scheme != null)
        {
            var lines = SupportSchemeDescriber.AgeTable(scheme);
            if (lines.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "年齢ごとの月額（各歳の初日に当てはまる段・追加条件は見ない）\n" + string.Join("\n", lines),
                    MessageType.None);
            }
        }
    }

    private static bool DrawTier(SerializedProperty tiers, int i, SupportScheme owner)
    {
        var t = tiers.GetArrayElementAtIndex(i);
        var effects = t.FindPropertyRelative("monthlyEffects");

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            bool changed = false;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"段 {i + 1}", EditorStyles.boldLabel);
                changed = SupportEditorGUI.ArrayButtons(tiers, i, "段");
            }
            if (changed) return true;

            EditorGUILayout.PropertyField(t.FindPropertyRelative("label"), new GUIContent("名前（空なら年齢から自動）"));
            SupportEditorGUI.DrawGameDay(t.FindPropertyRelative("fromAge"), "何歳から");
            SupportEditorGUI.DrawGameDay(t.FindPropertyRelative("toAge"), "何歳まで");

            // 毎月の金額：monthlyEffects の kind=Money・target=Household の最初の行を直接書き換える（無ければ、変えたときに足す）
            int mi = FindMainMoney(effects);
            using (new EditorGUILayout.HorizontalScope())
            {
                int cur = mi >= 0 ? Mathf.RoundToInt(effects.GetArrayElementAtIndex(mi).FindPropertyRelative("value").floatValue) : 0;
                int now = EditorGUILayout.IntField("毎月の金額（円）", cur);
                GUILayout.Label("円", GUILayout.Width(18));
                if (now != cur)
                {
                    if (mi < 0)
                    {
                        AppendEffect(effects, EffectKind.Money, EffectTarget.Household, 0f);
                        mi = effects.arraySize - 1;
                    }
                    effects.GetArrayElementAtIndex(mi).FindPropertyRelative("value").floatValue = now;
                }
            }

            int others = effects.arraySize - (mi >= 0 ? 1 : 0);
            effects.isExpanded = EditorGUILayout.Foldout(effects.isExpanded, $"お金以外の効果（ストレスなど）　{others}件", true);
            if (effects.isExpanded)
            {
                EditorGUI.indentLevel++;
                DrawEffects(effects, mi, EffectKind.Stress);
                EditorGUI.indentLevel--;
            }

            SupportRequirementList.Draw(t.FindPropertyRelative("requirements"), owner, "この段だけの条件");
        }
        return false;
    }

    private static int FindMainMoney(SerializedProperty effects)
    {
        for (int i = 0; i < effects.arraySize; i++)
        {
            var e = effects.GetArrayElementAtIndex(i);
            if (e.FindPropertyRelative("kind").intValue == (int)EffectKind.Money &&
                e.FindPropertyRelative("target").intValue == (int)EffectTarget.Household)
                return i;
        }
        return -1;
    }

    /// <summary>支給表に空の1段を足す（0歳1月1日〜0歳12月31日・お金 0）。</summary>
    public static SerializedProperty AppendTier(SerializedProperty tiers)
    {
        int n = tiers.arraySize;
        tiers.arraySize = n + 1;
        var t = tiers.GetArrayElementAtIndex(n);
        t.FindPropertyRelative("label").stringValue = "";
        SupportEditorGUI.SetDay(t.FindPropertyRelative("fromAge"), 0, 1, 1);
        SupportEditorGUI.SetDay(t.FindPropertyRelative("toAge"), 0, 12, 31);
        t.FindPropertyRelative("monthlyEffects").arraySize = 0;
        t.FindPropertyRelative("requirements").arraySize = 0;
        return t;
    }
}
