using UnityEditor;
using UnityEngine;

/// <summary>ChildStatCondition の埋め込みインスペクタ用エディタ。</summary>
[CustomEditor(typeof(ChildStatCondition))]
public class ChildStatConditionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var statKind = serializedObject.FindProperty("statKind");
        EditorGUILayout.PropertyField(statKind, new GUIContent("判定する項目"));

        var kind = (ChildStatKind)statKind.intValue;
        if (kind == ChildStatKind.趣味の種類)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("hobbyKind"), new GUIContent("趣味の種類"));
        }
        else
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("範囲（0〜100）");
                var min = serializedObject.FindProperty("minValue");
                var max = serializedObject.FindProperty("maxValue");

                int lo = EditorGUILayout.IntField(min.intValue, GUILayout.Width(44));
                GUILayout.Label("〜", GUILayout.Width(18));
                int hi = EditorGUILayout.IntField(max.intValue, GUILayout.Width(44));

                if (lo != min.intValue) min.intValue = Mathf.Clamp(lo, 0, 100);
                if (hi != max.intValue) max.intValue = Mathf.Clamp(hi, 0, 100);
                GUILayout.FlexibleSpace();
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}

