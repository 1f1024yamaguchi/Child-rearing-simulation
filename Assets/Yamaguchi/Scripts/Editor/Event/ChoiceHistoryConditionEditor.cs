using System.Collections.Generic;
using Maruyama.Events;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ChoiceHistoryCondition のインスペクター表示。
/// 選択肢IDの入力をドロップダウン（そのイベントの選択肢一覧）で選べるようにする。
/// </summary>
[CustomEditor(typeof(ChoiceHistoryCondition))]
public class ChoiceHistoryConditionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var targetEventProp = serializedObject.FindProperty("targetEvent");
        var checkTypeProp = serializedObject.FindProperty("checkType");
        var choiceIdProp = serializedObject.FindProperty("choiceId");

        EditorGUILayout.PropertyField(targetEventProp, new GUIContent("対象イベント"));
        EditorGUILayout.PropertyField(checkTypeProp, new GUIContent("条件の種類"));

        var checkType = (HistoryCheckType)checkTypeProp.intValue;
        bool needsChoice = checkType == HistoryCheckType.選んだ || checkType == HistoryCheckType.選ばなかった;

        if (needsChoice)
        {
            var evt = targetEventProp.objectReferenceValue as EventData;
            if (evt == null)
            {
                EditorGUILayout.HelpBox("対象イベントを設定してください。", MessageType.Info);
                EditorGUILayout.PropertyField(choiceIdProp, new GUIContent("選択肢ID"));
            }
            else if (evt.choices == null || evt.choices.Count == 0)
            {
                EditorGUILayout.HelpBox("対象イベントに選択肢が登録されていません。", MessageType.Warning);
                EditorGUILayout.PropertyField(choiceIdProp, new GUIContent("選択肢ID"));
            }
            else
            {
                var displayOptions = new List<string>();
                var idOptions = new List<string>();
                int selectedIndex = -1;
                string currentId = choiceIdProp.stringValue;

                for (int i = 0; i < evt.choices.Count; i++)
                {
                    var c = evt.choices[i];
                    if (c == null) continue;
                    string cid = !string.IsNullOrEmpty(c.id) ? c.id : $"c{i + 1}";
                    string label = string.IsNullOrEmpty(c.label) ? $"（選択肢{i + 1}）" : c.label;
                    displayOptions.Add($"{label} [{cid}]");
                    idOptions.Add(cid);

                    if (cid == currentId)
                    {
                        selectedIndex = displayOptions.Count - 1;
                    }
                }

                if (selectedIndex < 0 && !string.IsNullOrEmpty(currentId))
                {
                    displayOptions.Insert(0, $"不明な選択肢ID [{currentId}]");
                    idOptions.Insert(0, currentId);
                    selectedIndex = 0;
                }

                int defaultIndex = selectedIndex >= 0 ? selectedIndex : 0;
                int newIndex = EditorGUILayout.Popup("選択肢", defaultIndex, displayOptions.ToArray());
                if (newIndex >= 0 && newIndex < idOptions.Count)
                {
                    choiceIdProp.stringValue = idOptions[newIndex];
                }
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}

