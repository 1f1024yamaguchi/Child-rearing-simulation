using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 制度アセットの検索（キャッシュ）と、シーンの SupportManager への登録。
/// 登録は SupportManager の private な "schemes" に SerializedObject で書く。
/// プレハブのインスタンスなら、元プレハブに書く。
/// </summary>
public static class SupportRegistry
{
    private static List<SupportScheme> schemes;
    private static List<ExpenseItem> expenses;
    private static List<RequirementSO> requirements;

    private static SupportManager manager;
    private static double nextFind;

    [InitializeOnLoadMethod]
    private static void Init() => EditorApplication.projectChanged += Invalidate;

    /// <summary>アセットの一覧キャッシュを捨てる（作った・消した・複製したあとに呼ぶ）。</summary>
    public static void Invalidate()
    {
        schemes = null;
        expenses = null;
        requirements = null;
    }

    // ─── アセットの一覧 ─────────────────────────────────────────
    public static List<SupportScheme> AllSchemes()
    {
        if (schemes == null) schemes = LoadAll<SupportScheme>();
        return schemes;
    }

    public static List<RequirementSO> AllRequirements()
    {
        if (requirements == null) requirements = LoadAll<RequirementSO>();
        return requirements;
    }

    private static List<ExpenseItem> AllExpenses()
    {
        if (expenses == null) expenses = LoadAll<ExpenseItem>();
        return expenses;
    }

    private static List<T> LoadAll<T>() where T : Object
    {
        var list = new List<T>();
        foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (a != null) list.Add(a);
        }
        list.Sort((x, y) => string.CompareOrdinal(x.name, y.name));
        return list;
    }

    // ─── 条件アセットの使われ方 ─────────────────────────────────
    /// <summary>同じ条件アセットを、exclude 以外の制度・支出が使っていたらその名前を返す。</summary>
    public static List<string> RequirementUsers(RequirementSO r, SupportScheme exclude)
    {
        var names = new List<string>();
        if (r == null) return names;
        var seen = new HashSet<string>();

        foreach (var s in AllSchemes())
        {
            if (s == null || s == exclude) continue;
            bool used = s.applyRequirements != null && s.applyRequirements.Contains(r);
            if (!used && s.monthlyTiers != null)
            {
                foreach (var t in s.monthlyTiers)
                {
                    if (t != null && t.requirements != null && t.requirements.Contains(r)) { used = true; break; }
                }
            }
            if (used && seen.Add(s.name)) names.Add(s.name);
        }

        foreach (var e in AllExpenses())
        {
            if (e == null || e.tiers == null) continue;
            foreach (var t in e.tiers)
            {
                if (t != null && t.requirements != null && t.requirements.Contains(r))
                {
                    if (seen.Add("支出：" + e.name)) names.Add("支出：" + e.name);
                    break;
                }
            }
        }
        return names;
    }

    // ─── SupportManager ────────────────────────────────────────
    /// <summary>
    /// シーンの SupportManager。見つからない時の探し直しは Layout イベントか GUI の外だけ（1秒に1回まで）。
    /// Layout と Repaint で結果がずれて、描画の数が合わなくなるのを避けるため。
    /// </summary>
    public static SupportManager FindManager(bool force = false)
    {
        if (manager != null) return manager;
        var e = Event.current;
        if (!force && e != null && e.type != EventType.Layout) return null;
        double now = EditorApplication.timeSinceStartup;
        if (!force && now < nextFind) return null;
        nextFind = now + 1.0;
        manager = Object.FindFirstObjectByType<SupportManager>(FindObjectsInactive.Include);
        return manager;
    }

    public static bool IsRegistered(SupportScheme s)
    {
        var m = FindManager();
        if (m == null || s == null) return false;
        foreach (var x in m.Schemes) if (x == s) return true;
        return false;
    }

    public static void SetRegistered(SupportScheme s, bool on)
    {
        if (s == null) return;
        EditSchemes(list =>
        {
            int found = -1;
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == s) { found = i; break; }
            }
            if (on && found < 0)
            {
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = s;
            }
            else if (!on && found >= 0)
            {
                list.GetArrayElementAtIndex(found).objectReferenceValue = null;   // 参照を消してから要素を消す
                list.DeleteArrayElementAtIndex(found);
            }
        });
    }

    /// <summary>一覧の中で、同じカテゴリの前／次の制度と入れ替えられるか。</summary>
    public static bool CanMove(SupportScheme s, bool up) => NeighborIndex(s, up) >= 0;

    /// <summary>表示順（SupportManager.schemes の並び）を、同じカテゴリの前／次の制度と入れ替える。</summary>
    public static void Move(SupportScheme s, bool up)
    {
        EditSchemes(list =>
        {
            int fromIdx = -1;
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == s) { fromIdx = i; break; }
            }
            if (fromIdx < 0) return;
            int step = up ? -1 : 1;
            for (int j = fromIdx + step; j >= 0 && j < list.arraySize; j += step)
            {
                var other = list.GetArrayElementAtIndex(j).objectReferenceValue as SupportScheme;
                if (other != null && other.category == s.category)
                {
                    list.MoveArrayElement(fromIdx, j);
                    return;
                }
            }
        });
    }

    private static int NeighborIndex(SupportScheme s, bool up)
    {
        var m = FindManager();
        if (m == null || s == null) return -1;
        var list = m.Schemes;
        int fromIdx = -1;
        for (int i = 0; i < list.Count; i++) if (list[i] == s) { fromIdx = i; break; }
        if (fromIdx < 0) return -1;
        int step = up ? -1 : 1;
        for (int j = fromIdx + step; j >= 0 && j < list.Count; j += step)
        {
            if (list[j] != null && list[j].category == s.category) return j;
        }
        return -1;
    }

    /// <summary>登録済みの順番（登録されていなければ int.MaxValue）。</summary>
    public static int OrderOf(SupportScheme s)
    {
        var m = FindManager();
        if (m == null || s == null) return int.MaxValue;
        var list = m.Schemes;
        for (int i = 0; i < list.Count; i++) if (list[i] == s) return i;
        return int.MaxValue;
    }

    /// <summary>schemes のリストを書き換える。プレハブのインスタンスなら元プレハブに書く。</summary>
    private static void EditSchemes(System.Action<SerializedProperty> edit)
    {
        var m = FindManager(true);
        if (m == null) return;

        if (!EditorApplication.isPlaying && PrefabUtility.IsPartOfPrefabInstance(m))
        {
            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(m);
            string path = source != null ? AssetDatabase.GetAssetPath(source) : "";
            if (!string.IsNullOrEmpty(path))
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var pm = root.GetComponentInChildren<SupportManager>(true);
                    if (pm != null)
                    {
                        var psO = new SerializedObject(pm);
                        edit(psO.FindProperty("schemes"));
                        psO.ApplyModifiedPropertiesWithoutUndo();
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                return;
            }
        }

        var so = new SerializedObject(m);
        so.Update();
        edit(so.FindProperty("schemes"));
        so.ApplyModifiedProperties();
        if (!EditorApplication.isPlaying) EditorSceneManager.MarkSceneDirty(m.gameObject.scene);
    }

    // ─── ウィンドウの「登録」欄 ─────────────────────────────────
    public static void DrawGUI(SupportScheme s)
    {
        using (new SupportEditorGUI.BoxScope("シーンへの登録"))
        {
            var m = FindManager();
            if (m == null)
            {
                EditorGUILayout.HelpBox("このシーンに SupportManager がありません。登録はできません（アセットの編集はできます）。", MessageType.Warning);
                return;
            }

            if (!EditorApplication.isPlaying && PrefabUtility.IsPartOfPrefabInstance(m))
                EditorGUILayout.LabelField("※ SupportManager はプレハブのインスタンスです。登録は元のプレハブに書き込みます。", EditorStyles.wordWrappedMiniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (IsRegistered(s))
                {
                    EditorGUILayout.LabelField("✓ SupportManager に登録されています（サポート画面に出ます）");
                    if (GUILayout.Button("外す", GUILayout.Width(60))) SetRegistered(s, false);
                }
                else
                {
                    EditorGUILayout.HelpBox("まだ SupportManager に登録されていないので、このままではサポート画面に出ません。", MessageType.Warning);
                    if (GUILayout.Button("登録する", GUILayout.Width(80), GUILayout.Height(38))) SetRegistered(s, true);
                }
            }
        }
    }
}


