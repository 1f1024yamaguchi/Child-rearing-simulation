using UnityEditor;
using UnityEngine;

/// <summary>MonthCondition の埋め込みインスペクタ用エディタ。</summary>
[CustomEditor(typeof(MonthCondition))]
public class MonthConditionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("発生する月（1〜12月）", EditorStyles.miniBoldLabel);
        SupportEditorGUI.DrawMonthToggles(serializedObject.FindProperty("months"), false);
        serializedObject.ApplyModifiedProperties();
    }
}

