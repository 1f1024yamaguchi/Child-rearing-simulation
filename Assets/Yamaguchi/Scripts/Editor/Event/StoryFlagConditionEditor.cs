using UnityEditor;
using UnityEngine;

/// <summary>
/// StoryFlagCondition のインスペクター表示。
/// </summary>
[CustomEditor(typeof(StoryFlagCondition))]
public class StoryFlagConditionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var flagProp = serializedObject.FindProperty("flag");
        var isSetProp = serializedObject.FindProperty("isSet");

        EditorGUILayout.PropertyField(flagProp, new GUIContent("フラグ"));

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel("状態");
            bool set = isSetProp.boolValue;
            if (GUILayout.Toggle(set, "立っている", EditorStyles.radioButton, GUILayout.Width(80)) && !set)
                isSetProp.boolValue = true;
            if (GUILayout.Toggle(!set, "立っていない", EditorStyles.radioButton, GUILayout.Width(90)) && set)
                isSetProp.boolValue = false;
            GUILayout.FlexibleSpace();
        }

        serializedObject.ApplyModifiedProperties();
    }
}

