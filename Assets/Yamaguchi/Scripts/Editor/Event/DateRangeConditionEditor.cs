using UnityEditor;
using UnityEngine;

/// <summary>DateRangeCondition の埋め込みインスペクタ用エディタ。</summary>
[CustomEditor(typeof(DateRangeCondition))]
public class DateRangeConditionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SupportEditorGUI.DrawGameDay(serializedObject.FindProperty("fromDay"), "開始日");
        SupportEditorGUI.DrawGameDay(serializedObject.FindProperty("toDay"), "終了日");
        serializedObject.ApplyModifiedProperties();
    }
}

