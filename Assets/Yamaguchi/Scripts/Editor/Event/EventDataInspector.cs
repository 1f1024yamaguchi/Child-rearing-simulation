using Maruyama.Events;
using UnityEditor;
using UnityEngine;

/// <summary>EventData の Inspector に「編集ウィンドウで開く」ボタンとまとめ文を足す。</summary>
[CustomEditor(typeof(EventData))]
public class EventDataInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var e = (EventData)target;
        if (GUILayout.Button("編集ウィンドウで開く", GUILayout.Height(26)))
            EventEditorWindow.OpenWith(e);
        EditorGUILayout.HelpBox("まとめ：" + EventDescriber.Summary(e), MessageType.None);
        EditorGUILayout.Space();
        DrawDefaultInspector();
    }
}

