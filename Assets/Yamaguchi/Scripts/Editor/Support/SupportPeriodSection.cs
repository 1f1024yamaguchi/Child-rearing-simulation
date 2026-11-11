using UnityEditor;
using UnityEngine;

/// <summary>申請できる期間の欄：start / deadlineType と、種類に合う欄だけ。</summary>
public static class SupportPeriodSection
{
    public static void Draw(SerializedObject so)
    {
        using (new SupportEditorGUI.BoxScope("申請できる期間"))
        {
            SupportEditorGUI.DrawGameDay(so.FindProperty("start"), "いつから申請できる");

            var typeProp = so.FindProperty("deadlineType");
            SupportEditorGUI.EnumPopup(typeProp, "期限", SupportEditorGUI.DeadlineNames);

            switch ((DeadlineType)typeProp.intValue)
            {
                case DeadlineType.Once:
                    SupportEditorGUI.DrawGameDay(so.FindProperty("deadline"), "期限の日");
                    break;

                case DeadlineType.Yearly:
                    SupportEditorGUI.DrawMonthDay(so.FindProperty("yearly"), "毎年の期限");
                    EditorGUILayout.PropertyField(so.FindProperty("lapseYears"),
                        new GUIContent("資格を失うまでの年数", "期限から何年手続きしないと受給資格を失うか"));
                    EditorGUILayout.PropertyField(so.FindProperty("renewWindowDays"),
                        new GUIContent("更新できる日数（期限の何日前から）"));
                    break;
            }

            var scheme = so.targetObject as SupportScheme;
            if (scheme != null)
                EditorGUILayout.LabelField(SupportSchemeDescriber.PeriodText(scheme), EditorStyles.wordWrappedLabel);
        }
    }
}
