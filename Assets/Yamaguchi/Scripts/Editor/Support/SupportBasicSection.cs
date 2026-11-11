using UnityEditor;
using UnityEngine;

/// <summary>基本の欄：id / title / description / icon / category / merits / demerits。</summary>
public static class SupportBasicSection
{
    public static void Draw(SerializedObject so, EditorWindow repaintTarget)
    {
        using (new SupportEditorGUI.BoxScope("基本"))
        {
            EditorGUILayout.PropertyField(so.FindProperty("id"), new GUIContent("ID（セーブ用・後から変えない）"));
            EditorGUILayout.PropertyField(so.FindProperty("title"), new GUIContent("名前（title）"));
            EditorGUILayout.PropertyField(so.FindProperty("description"), new GUIContent("説明（description）"));

            var icon = so.FindProperty("icon");
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.PropertyField(icon, new GUIContent("アイコン（icon）"));
                }
                var sprite = icon.objectReferenceValue as Sprite;
                if (sprite != null) SupportEditorGUI.DrawThumbnail(sprite, repaintTarget);
            }

            SupportEditorGUI.EnumPopup(so.FindProperty("category"), "カテゴリ（出どころ）", SupportEditorGUI.CategoryNames);

            EditorGUILayout.Space(4);
            SupportEditorGUI.DrawStringList(so.FindProperty("merits"), "メリット（緑のチップ）");
            EditorGUILayout.Space(4);
            SupportEditorGUI.DrawStringList(so.FindProperty("demerits"), "デメリット（赤のチップ）");
        }
    }
}
