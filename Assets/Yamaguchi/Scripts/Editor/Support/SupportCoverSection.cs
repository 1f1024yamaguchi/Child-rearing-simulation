using UnityEditor;
using UnityEngine;

/// <summary>支出の肩代わり（covers）と医療費の助成（medicalExpenseCovers）の欄。</summary>
public static class SupportCoverSection
{
    public static void Draw(SerializedObject so, SupportFeatures f)
    {
        if (f.covers)
        {
            using (new SupportEditorGUI.BoxScope("支出の肩代わり（受給中のあいだ、期間内の月だけ支出を減らす）"))
                DrawList(so.FindProperty("covers"), true, "肩代わり");
        }
        if (f.medical)
        {
            using (new SupportEditorGUI.BoxScope("医療費の助成（病気イベントの自己負担を助成）"))
                DrawList(so.FindProperty("medicalExpenseCovers"), false, "助成");
        }
    }

    private static void DrawList(SerializedProperty list, bool withExpense, string what)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            if (DrawRow(list, i, withExpense, what)) break;
        }
        if (GUILayout.Button($"＋ {what}を追加", GUILayout.Width(120))) AppendCover(list, withExpense);
    }

    private static bool DrawRow(SerializedProperty list, int i, bool withExpense, string what)
    {
        var c = list.GetArrayElementAtIndex(i);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            bool changed;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{what} {i + 1}", EditorStyles.boldLabel);
                changed = SupportEditorGUI.ArrayButtons(list, i, what);
            }
            if (changed) return true;

            if (withExpense)
                EditorGUILayout.PropertyField(c.FindPropertyRelative("expense"), new GUIContent("肩代わりする支出"));

            var type = c.FindPropertyRelative("type");
            var value = c.FindPropertyRelative("value");
            SupportEditorGUI.EnumPopup(type, "種類", SupportEditorGUI.CoverTypeNames);
            switch ((CoverType)type.intValue)
            {
                case CoverType.Rate:
                    SupportEditorGUI.DrawPercent(value, "割合（％）");
                    break;
                case CoverType.UpTo:
                    SupportEditorGUI.DrawYen(value, "上限（円）");
                    break;
                // Full は value を使わないので出さない
            }

            SupportEditorGUI.DrawGameDay(c.FindPropertyRelative("fromAge"), "何歳から");
            SupportEditorGUI.DrawGameDay(c.FindPropertyRelative("toAge"), "何歳まで");
        }
        return false;
    }

    /// <summary>肩代わり（または医療費の助成）に1行足す。全額・0歳1月1日〜5歳12月31日。</summary>
    public static SerializedProperty AppendCover(SerializedProperty list, bool withExpense)
    {
        int n = list.arraySize;
        list.arraySize = n + 1;
        var c = list.GetArrayElementAtIndex(n);
        if (withExpense) c.FindPropertyRelative("expense").objectReferenceValue = null;
        c.FindPropertyRelative("type").intValue = (int)CoverType.Full;
        c.FindPropertyRelative("value").floatValue = 0f;
        SupportEditorGUI.SetDay(c.FindPropertyRelative("fromAge"), 0, 1, 1);
        SupportEditorGUI.SetDay(c.FindPropertyRelative("toAge"), 5, 12, 31);
        return c;
    }
}
