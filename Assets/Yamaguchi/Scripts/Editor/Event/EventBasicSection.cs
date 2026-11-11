using Maruyama.Events;
using UnityEditor;
using UnityEngine;

/// <summary>編集ウィンドウの「基本」欄：ID / メリット・デメリット / タイトル / 説明 / 画像。</summary>
public static class EventBasicSection
{
    private static readonly string[] CategoryLabels = { "デメリット", "メリット" }; // EventCategory の並び（Demerit, Merit）と同じ

    public static void Draw(SerializedObject so, EditorWindow window)
    {
        using (new SupportEditorGUI.BoxScope("基本"))
        {
            var e = (EventData)so.targetObject;

            // ID
            var id = so.FindProperty("id");
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(id, new GUIContent("ID"));
                if (GUILayout.Button("ファイル名から付ける", GUILayout.Width(130)))
                    id.stringValue = EventRegistry.MakeId(e.name, e);
            }

            // メリット / デメリット
            SupportEditorGUI.EnumPopup(so.FindProperty("category"), "種類", CategoryLabels);

            // タイトル
            EditorGUILayout.PropertyField(so.FindProperty("title"), new GUIContent("タイトル"));

            // 説明
            var desc = so.FindProperty("description");
            EditorGUILayout.PropertyField(desc, new GUIContent("説明"));
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(" ");
                // 置き換えの処理はストーリー側の段階で作る。ここでは文字を足すだけ。
                if (GUILayout.Button("{name}", GUILayout.Width(70)))
                    desc.stringValue = (desc.stringValue ?? "") + "{name}";
                GUILayout.Label("← 子どもの名前に置き換わる印を、説明の最後に足します", EditorStyles.miniLabel);
            }

            // 画像
            var icon = so.FindProperty("iconSprite");
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(icon, new GUIContent("画像"));
                if (icon.objectReferenceValue == null)
                    GUILayout.Label("画像なし", EditorStyles.miniLabel, GUILayout.Width(52));
            }
            if (icon.objectReferenceValue is Sprite sprite)
                SupportEditorGUI.DrawThumbnail(sprite, window);

            // 絵の指示メモ（画像が未設定のときに開発用プレースホルダーで表示される）
            var note = so.FindProperty("imageNote");
            if (note != null)
            {
                EditorGUILayout.PropertyField(note, new GUIContent("絵の指示メモ"));
            }
        }
    }
}

