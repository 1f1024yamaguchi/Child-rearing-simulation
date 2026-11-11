using UnityEditor;
using UnityEngine;

/// <summary>MoneyCondition の埋め込みインスペクタ用エディタ。</summary>
[CustomEditor(typeof(MoneyCondition))]
public class MoneyConditionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("comparison"), new GUIContent("比較条件"));
        SupportEditorGUI.DrawYen(serializedObject.FindProperty("amount"), "所持金");
        serializedObject.ApplyModifiedProperties();
    }
}

