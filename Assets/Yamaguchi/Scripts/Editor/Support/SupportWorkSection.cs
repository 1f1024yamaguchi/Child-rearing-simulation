using UnityEditor;
using UnityEngine;

/// <summary>働き方（会社の制度）と給与連動の欄。</summary>
public static class SupportWorkSection
{
    public static void Draw(SerializedObject so, SupportFeatures f)
    {
        if (!f.work && !f.wage) return;

        using (new SupportEditorGUI.BoxScope("働き方・給与連動"))
        {
            // 対象の親は、働き方を変えるときも、給与連動だけのときも使う
            SupportEditorGUI.EnumPopup(so.FindProperty("workTarget"), "対象の親", SupportEditorGUI.ParentRoleNames);

            if (f.work)
            {
                SupportEditorGUI.EnumPopup(so.FindProperty("workMode"), "申請すると変わる働き方", SupportEditorGUI.WorkModeNames);
                SupportEditorGUI.DrawGameDay(so.FindProperty("workModeUntil"), "いつまで");
            }

            // いまの親の働き方を、申請の条件（給与連動の対象）にできる
            var requires = so.FindProperty("requiresWorkMode");
            bool req = EditorGUILayout.Toggle("働き方を条件にする", requires.boolValue);
            if (req != requires.boolValue) requires.boolValue = req;
            if (requires.boolValue)
                SupportEditorGUI.EnumPopup(so.FindProperty("requiredWorkMode"), "必要な働き方", SupportEditorGUI.WorkModeNames);

            if (f.wage)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("給与に連動", EditorStyles.boldLabel);
                SupportEditorGUI.EnumPopup(so.FindProperty("wageTarget"), "支給の対象", SupportEditorGUI.WageTargetNames);
                SupportEditorGUI.DrawGameDay(so.FindProperty("wageBenefitUntil"), "いつまで支給（0歳1月1日＝指定なし）");

                var tiers = so.FindProperty("wageLinkedTiers");
                for (int i = 0; i < tiers.arraySize; i++)
                {
                    if (DrawTier(tiers, i)) break;
                }
                if (GUILayout.Button("＋ 段を追加", GUILayout.Height(24))) AppendWageTier(tiers);
            }
        }
    }

    private static bool DrawTier(SerializedProperty tiers, int i)
    {
        var t = tiers.GetArrayElementAtIndex(i);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            bool changed;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"段 {i + 1}", EditorStyles.boldLabel);
                changed = SupportEditorGUI.ArrayButtons(tiers, i, "段");
            }
            if (changed) return true;

            EditorGUILayout.PropertyField(t.FindPropertyRelative("label"), new GUIContent("名前"));

            var fromDay = t.FindPropertyRelative("fromDay");
            int nf = EditorGUILayout.IntField("何日目から", fromDay.intValue);
            if (nf != fromDay.intValue) fromDay.intValue = Mathf.Max(1, nf);

            var toDay = t.FindPropertyRelative("toDay");
            int nt = EditorGUILayout.IntField("何日目まで（-1＝ずっと）", toDay.intValue);
            if (nt != toDay.intValue) toDay.intValue = Mathf.Max(-1, nt);

            SupportEditorGUI.DrawPercent(t.FindPropertyRelative("rate"), "給与の何％");
            SupportEditorGUI.EnumPopup(t.FindPropertyRelative("basis"), "何の給与を基準にするか", SupportEditorGUI.WageBaseNames);
        }
        return false;
    }

    /// <summary>給与連動に1段足す（1日目からずっと・67%・休業前の給与）。</summary>
    public static SerializedProperty AppendWageTier(SerializedProperty tiers)
    {
        int n = tiers.arraySize;
        tiers.arraySize = n + 1;
        var t = tiers.GetArrayElementAtIndex(n);
        t.FindPropertyRelative("label").stringValue = "";
        t.FindPropertyRelative("fromDay").intValue = 1;
        t.FindPropertyRelative("toDay").intValue = -1;
        t.FindPropertyRelative("rate").floatValue = 0.67f;
        t.FindPropertyRelative("basis").intValue = (int)WageBase.SalaryBeforeLeave;
        return t;
    }
}

