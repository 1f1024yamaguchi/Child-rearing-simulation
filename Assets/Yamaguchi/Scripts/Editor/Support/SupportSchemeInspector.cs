using UnityEditor;
using UnityEngine;

/// <summary>SupportScheme の Inspector に「編集ウィンドウで開く」ボタンとまとめ文を足す。</summary>
[CustomEditor(typeof(SupportScheme))]
public class SupportSchemeInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var s = (SupportScheme)target;
        if (GUILayout.Button("編集ウィンドウで開く", GUILayout.Height(26)))
            SupportEditorWindow.OpenWith(s);
        EditorGUILayout.HelpBox("まとめ：" + SupportSchemeDescriber.OneLine(s), MessageType.None);
        EditorGUILayout.Space();
        DrawDefaultInspector();
    }
}
