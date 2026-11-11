using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// 申請条件（RequirementSO）のリスト。SupportScheme.applyRequirements と PaymentTier.requirements の両方に使う。
/// 中身はその場で編集でき、同じアセットを他でも使っていたら警告して「この制度専用にコピー」できる。
/// </summary>
public static class SupportRequirementList
{
    private static readonly Dictionary<Object, Editor> editors = new Dictionary<Object, Editor>();

    /// <summary>ウィンドウを閉じるときに、中身を編集するためのエディタを捨てる。</summary>
    public static void ClearCache()
    {
        foreach (var kv in editors)
        {
            if (kv.Value != null) Object.DestroyImmediate(kv.Value);
        }
        editors.Clear();
    }

    public static void Draw(SerializedProperty list, SupportScheme owner, string label)
    {
        using (new SupportEditorGUI.BoxScope(label))
        {
            if (list.arraySize == 0)
                EditorGUILayout.LabelField("条件なし（すぐ申請できます）", EditorStyles.miniLabel);

            for (int i = 0; i < list.arraySize; i++)
            {
                if (DrawRow(list, i, owner)) break;
            }

            DrawAddButton(list, owner);
        }
    }

    // 1件ぶん。消したら true
    private static bool DrawRow(SerializedProperty list, int i, SupportScheme owner)
    {
        var element = list.GetArrayElementAtIndex(i);
        var req = element.objectReferenceValue as RequirementSO;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            bool remove;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(element, GUIContent.none);
                remove = GUILayout.Button("✕", GUILayout.Width(24));
            }
            if (remove)
            {
                element.objectReferenceValue = null;   // 参照を消してから要素を消す（アセットは消えない）
                list.DeleteArrayElementAtIndex(i);
                return true;
            }

            if (req == null)
            {
                EditorGUILayout.LabelField("（空の行です。アセットを入れるか ✕ で消してください）", EditorStyles.miniLabel);
                return false;
            }

            EditorGUILayout.LabelField("→ " + SupportSchemeDescriber.SafeDescribe(req), EditorStyles.wordWrappedLabel);

            var users = SupportRegistry.RequirementUsers(req, owner);
            if (users.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "ほかでも使っています：" + string.Join("、", users) + "\nここで値を変えると、そちらにも効きます。",
                    MessageType.Warning);
                if (GUILayout.Button("この制度専用にコピー"))
                {
                    var copy = CopyForOwner(req, owner);
                    if (copy != null) element.objectReferenceValue = copy;
                }
            }

            // 中身をその場で編集する
            EditorGUI.indentLevel++;
            var ed = GetEditor(req);
            if (ed != null) ed.OnInspectorGUI();
            EditorGUI.indentLevel--;
        }
        return false;
    }

    private static Editor GetEditor(Object o)
    {
        editors.TryGetValue(o, out var ed);
        Editor.CreateCachedEditor(o, null, ref ed);
        editors[o] = ed;
        return ed;
    }

    // ─── 足す ────────────────────────────────────────────────────
    private static void DrawAddButton(SerializedProperty list, SupportScheme owner)
    {
        if (!GUILayout.Button("＋条件を足す ▼", GUILayout.Width(120))) return;

        var target = list.serializedObject.targetObject;
        string path = list.propertyPath;
        string ownerName = OwnerName(owner);

        var menu = new GenericMenu();
        foreach (var existing in SupportRegistry.AllRequirements())
        {
            if (existing == null) continue;
            var asset = existing;
            string text = (asset.name + "　" + SupportSchemeDescriber.SafeDescribe(asset)).Replace('/', '／');
            menu.AddItem(new GUIContent("既存アセットから選ぶ/" + text), false, () => AddReference(target, path, asset));
        }
        menu.AddSeparator("");
        foreach (var type in TypeCache.GetTypesDerivedFrom<RequirementSO>())
        {
            if (type.IsAbstract) continue;
            var t = type;
            menu.AddItem(new GUIContent("新しく作る/" + t.Name), false, () => CreateAndAdd(target, path, t, ownerName));
        }
        menu.ShowAsContext();
    }

    private static void AddReference(Object target, string path, RequirementSO asset)
    {
        var so = new SerializedObject(target);
        so.Update();
        var list = so.FindProperty(path);
        int n = list.arraySize;
        list.arraySize = n + 1;
        list.GetArrayElementAtIndex(n).objectReferenceValue = asset;
        so.ApplyModifiedProperties();
        InternalEditorUtility.RepaintAllViews();
    }

    private static void CreateAndAdd(Object target, string path, System.Type type, string ownerName)
    {
        var asset = CreateAsset(type, ownerName);
        if (asset != null) AddReference(target, path, asset);
    }

    // ─── アセットの作成・コピー ──────────────────────────────────
    private static string NewPath(System.Type type, string ownerName)
    {
        SupportEditorGUI.EnsureFolder(SupportEditorGUI.RequirementDir);
        string shortName = type.Name.EndsWith("SO") ? type.Name.Substring(0, type.Name.Length - 2) : type.Name;
        return AssetDatabase.GenerateUniqueAssetPath($"{SupportEditorGUI.RequirementDir}/Req_{shortName}_{ownerName}.asset");
    }

    private static RequirementSO CreateAsset(System.Type type, string ownerName)
    {
        string path = NewPath(type, ownerName);
        var instance = ScriptableObject.CreateInstance(type);
        AssetDatabase.CreateAsset(instance, path);
        AssetDatabase.SaveAssets();
        SupportRegistry.Invalidate();
        return AssetDatabase.LoadAssetAtPath<RequirementSO>(path);
    }

    private static RequirementSO CopyForOwner(RequirementSO req, SupportScheme owner)
    {
        string src = AssetDatabase.GetAssetPath(req);
        if (string.IsNullOrEmpty(src)) return null;
        string dst = NewPath(req.GetType(), OwnerName(owner));
        if (!AssetDatabase.CopyAsset(src, dst)) return null;
        SupportRegistry.Invalidate();
        return AssetDatabase.LoadAssetAtPath<RequirementSO>(dst);
    }

    private static string OwnerName(SupportScheme owner)
    {
        if (owner == null) return "new";
        return string.IsNullOrEmpty(owner.id) ? owner.name : owner.id;
    }
}
