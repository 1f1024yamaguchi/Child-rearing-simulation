using System;
using System.Collections.Generic;
using Maruyama.Events;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 条件（EventConditionSO）のリスト部品。1行ずつ、その場で中身を編集できる。
/// 「新しく作る」は、ホストのアセットの中にサブアセットとして保存する。
/// イベント本体・選択肢の出す条件・チュートリアルページの出す条件で使い回せる。
/// </summary>
public static class EventConditionList
{
    private static readonly Dictionary<Object, Editor> editors = new Dictionary<Object, Editor>();

    /// <summary>ウィンドウを閉じるときに、埋め込みエディタを片付ける。</summary>
    public static void ClearCache()
    {
        foreach (var ed in editors.Values)
        {
            if (ed != null) Object.DestroyImmediate(ed);
        }
        editors.Clear();
    }

    /// <summary>既存の呼び出しとの互換用。</summary>
    public static void Draw(SerializedProperty list, EventData host)
    {
        Draw(list, (Object)host, "出る条件（すべて満たしたとき）");
    }

    /// <summary>list は EventConditionSO の参照のリスト。host は保存先のアセット（サブアセットを入れる先）。</summary>
    public static void Draw(SerializedProperty list, Object host, string boxTitle = "出る条件（すべて満たしたとき）")
    {
        using (new SupportEditorGUI.BoxScope(boxTitle))
        {
            if (list.arraySize == 0)
                EditorGUILayout.LabelField("条件なし（いつでも出ます／押せます）", EditorStyles.miniLabel);

            for (int i = 0; i < list.arraySize; i++)
            {
                if (DrawRow(list, i, host)) break; // 配列が変わったので、この回の描画はやめる
            }

            DrawAddButton(list, host);
        }
    }

    // ─── 1行 ─────────────────────────────────────────────────────
    private static bool DrawRow(SerializedProperty list, int i, Object host)
    {
        var element = list.GetArrayElementAtIndex(i);
        var cond = element.objectReferenceValue as EventConditionSO;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            bool remove = false;
            using (new EditorGUILayout.HorizontalScope())
            {
                string kind = cond == null ? "（空）" : EventDescriber.ConditionTypeName(cond.GetType());
                GUILayout.Label(kind, EditorStyles.boldLabel, GUILayout.Width(100));
                if (cond != null)
                {
                    string where = IsOwnSubAsset(cond, host) ? "このアセットの中" : "別アセット：" + cond.name;
                    GUILayout.Label(where, EditorStyles.miniLabel);
                }
                GUILayout.FlexibleSpace();
                remove = GUILayout.Button("✕", GUILayout.Width(24));
            }

            if (remove)
            {
                Remove(list, i, host);
                return true;
            }

            if (cond == null)
            {
                EditorGUILayout.HelpBox("条件が空（None）です。アセットを入れるか ✕ で消してください", MessageType.Error);
                var picked = (EventConditionSO)EditorGUILayout.ObjectField("条件", null, typeof(EventConditionSO), false);
                if (picked != null) element.objectReferenceValue = picked;
                return false;
            }

            EditorGUILayout.LabelField("→ " + EventDescriber.ConditionText(cond), EditorStyles.wordWrappedLabel);

            if (host is EventData evHost)
            {
                var users = EventRegistry.ConditionUsers(cond, evHost);
                if (users.Count > 0)
                {
                    EditorGUILayout.HelpBox(
                        "ほかでも使っています：" + string.Join("、", users) + "\nここで変えるとそちらも変わります。",
                        MessageType.Warning);
                    if (GUILayout.Button("このイベント専用にコピー"))
                    {
                        var copy = CopyForHost(cond, host);
                        if (copy != null)
                        {
                            element.objectReferenceValue = copy;
                            list.serializedObject.ApplyModifiedProperties();
                            AssetDatabase.SaveAssets();
                            EventRegistry.Invalidate();
                        }
                        return true;
                    }
                }
            }

            // 中身をその場で編集する
            EditorGUI.indentLevel++;
            var ed = GetEditor(cond);
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

    // ─── 外す ────────────────────────────────────────────────────
    /// <summary>
    /// 行を外す。このアセットのサブアセットなら、サブアセットごと消す
    /// （別アセットの条件は参照を外すだけで、アセットは消さない）。
    /// </summary>
    private static void Remove(SerializedProperty list, int i, Object host)
    {
        var element = list.GetArrayElementAtIndex(i);
        var cond = element.objectReferenceValue as EventConditionSO;

        // 参照のリストは、先に null にしてから消さないと要素が残る
        element.objectReferenceValue = null;
        list.DeleteArrayElementAtIndex(i);
        list.serializedObject.ApplyModifiedProperties();
        EventRegistry.Invalidate();

        bool canDeleteSubAsset = false;
        if (cond != null && IsOwnSubAsset(cond, host))
        {
            if (host is EventData evHost)
            {
                canDeleteSubAsset = EventRegistry.ConditionUsers(cond, evHost).Count == 0;
            }
            else
            {
                canDeleteSubAsset = true;
            }
        }

        if (canDeleteSubAsset)
        {
            if (editors.TryGetValue(cond, out var ed))
            {
                if (ed != null) Object.DestroyImmediate(ed);
                editors.Remove(cond);
            }
            AssetDatabase.RemoveObjectFromAsset(cond);
            Object.DestroyImmediate(cond, true);
            EditorUtility.SetDirty(host);
            AssetDatabase.SaveAssets();
        }
    }

    /// <summary>
    /// host（イベント・チュートリアル）の中にある条件のサブアセットのうち、どこからも参照されていないものを消す。
    /// 選択肢やページを ✕ で消すと、その「出す条件」のサブアセットがアセットの中に取り残されるため。
    /// 消す操作が反映された後（ApplyModifiedProperties の後）に呼ぶこと。EditorApplication.delayCall から呼ぶのが安全。
    /// </summary>
    public static void CleanupUnreferenced(Object host)
    {
        if (host == null) return;
        string path = AssetDatabase.GetAssetPath(host);
        if (string.IsNullOrEmpty(path)) return;

        // host と、そのファイルの中のすべてのオブジェクトから参照されている条件を集める
        var referenced = new HashSet<Object>();
        var all = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var obj in all)
        {
            if (obj == null) continue;
            var so = new SerializedObject(obj);
            var it = so.GetIterator();
            while (it.Next(true))
            {
                if (it.propertyType == SerializedPropertyType.ObjectReference &&
                    it.objectReferenceValue is EventConditionSO c && c != obj)
                    referenced.Add(c);
            }
        }

        var evHost = host as EventData;
        int removed = 0;
        foreach (var obj in all)
        {
            if (!(obj is EventConditionSO cond) || !AssetDatabase.IsSubAsset(cond)) continue;
            if (referenced.Contains(cond)) continue;
            // ほかのイベントが参照していたら消さない
            if (evHost != null && EventRegistry.ConditionUsers(cond, evHost).Count > 0) continue;

            if (editors.TryGetValue(cond, out var ed))
            {
                if (ed != null) Object.DestroyImmediate(ed);
                editors.Remove(cond);
            }
            AssetDatabase.RemoveObjectFromAsset(cond);
            Object.DestroyImmediate(cond, true);
            removed++;
        }

        if (removed > 0)
        {
            EditorUtility.SetDirty(host);
            AssetDatabase.SaveAssets();
            EventRegistry.Invalidate();
            Debug.Log($"[EventConditionList] 「{host.name}」の中の使われていない条件を {removed} 個消しました。");
        }
    }

    /// <summary>次のエディタ更新で CleanupUnreferenced を呼ぶ（削除の反映を待つため）。</summary>
    public static void CleanupUnreferencedLater(Object host)
    {
        if (host == null) return;
        EditorApplication.delayCall += () => CleanupUnreferenced(host);
    }

    private static bool IsOwnSubAsset(Object cond, Object host)
    {
        if (cond == null || host == null) return false;
        return AssetDatabase.IsSubAsset(cond) && AssetDatabase.GetAssetPath(cond) == AssetDatabase.GetAssetPath(host);
    }

    // ─── 足す ────────────────────────────────────────────────────
    private static void DrawAddButton(SerializedProperty list, Object host)
    {
        if (!GUILayout.Button("＋条件を足す ▼", GUILayout.Width(120))) return;

        var menu = new GenericMenu();

        // 部品が増えれば自動で並ぶ
        foreach (var type in TypeCache.GetTypesDerivedFrom<EventConditionSO>())
        {
            if (type.IsAbstract) continue;
            var t = type;
            string name = EventDescriber.ConditionTypeName(t);
            if (name != t.Name) name += "（" + t.Name + "）";
            menu.AddItem(new GUIContent("新しく作る/" + name), false, () => CreateAndAdd(list, host, t));
        }

        menu.AddSeparator("");
        foreach (var existing in EventRegistry.AllConditionAssets())
        {
            if (existing == null) continue;
            var asset = existing;
            string text = (asset.name + "　" + EventDescriber.ConditionText(asset)).Replace('/', '／');
            menu.AddItem(new GUIContent("既存のアセットから選ぶ/" + text), false, () => AddReference(list, host, asset));
        }
        menu.ShowAsContext();
    }

    private static void CreateAndAdd(SerializedProperty list, Object host, Type type)
    {
        var instance = (EventConditionSO)ScriptableObject.CreateInstance(type);
        instance.name = "Cond_" + EventDescriber.ConditionShortName(type);
        AssetDatabase.AddObjectToAsset(instance, host);
        AddReference(list, host, instance);
    }

    private static void AddReference(SerializedProperty list, Object host, EventConditionSO asset)
    {
        list.serializedObject.Update();
        int n = list.arraySize;
        list.arraySize = n + 1;
        list.GetArrayElementAtIndex(n).objectReferenceValue = asset;
        list.serializedObject.ApplyModifiedProperties();

        EditorUtility.SetDirty(host);
        AssetDatabase.SaveAssets();
        EventRegistry.Invalidate();
        InternalEditorUtility.RepaintAllViews();
    }

    /// <summary>別アセットの条件を、host のサブアセットとしてコピーする。</summary>
    private static EventConditionSO CopyForHost(EventConditionSO src, Object host)
    {
        var copy = Object.Instantiate(src);
        copy.name = "Cond_" + EventDescriber.ConditionShortName(src.GetType());
        AssetDatabase.AddObjectToAsset(copy, host);
        return copy;
    }
}
