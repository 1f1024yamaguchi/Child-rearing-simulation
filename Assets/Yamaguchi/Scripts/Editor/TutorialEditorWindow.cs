using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// メニュー「子育て → チュートリアル → 編集ウィンドウ」。
/// - 出すタイミング：いつ出すか（年齢・家出の判定が始まる日・ストレス・お金など）を選ぶ。スクリプトは書かない。
/// - ページ：足す・並べ替える・消す、画像と文を入れる。
/// - 「Canvas で見る」：再生しなくても、シーンのチュートリアル画面にそのページを出す。
/// - すべてのチュートリアル：プロジェクトにあるチュートリアルが、いつ出るかの一覧。
/// </summary>
public class TutorialEditorWindow : EditorWindow
{
    private TutorialData data;
    private Vector2 scroll;
    private int previewIndex = -1;
    private bool showAll = true;

    [MenuItem("子育て/チュートリアル/編集ウィンドウ", false, 2)]
    public static void OpenWindow() => GetWindow<TutorialEditorWindow>("チュートリアル");

    /// 指定のチュートリアルを開いた状態でウィンドウを出す（TutorialData の Inspector から呼ぶ）。
    public static void OpenWith(TutorialData tutorial)
    {
        var w = GetWindow<TutorialEditorWindow>("チュートリアル");
        w.data = tutorial;
        w.previewIndex = -1;
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.HelpBox(
            "1ページ ＝ 上の画像 ＋ 見出し ＋ 説明文。ページは左から順に表示され、スワイプ・矢印で次へ進みます。\n" +
            "いつ出すかは「出すタイミング」で選びます。画像の位置・大きさは、シーンの TutorialCanvas を直接動かして決めます。",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            var picked = (TutorialData)EditorGUILayout.ObjectField("チュートリアル", data, typeof(TutorialData), false);
            if (picked != data) { data = picked; previewIndex = -1; }
            if (GUILayout.Button("新しく作る", GUILayout.Width(80))) CreateNew();
        }

        if (data == null)
        {
            EditorGUILayout.HelpBox("チュートリアルのアセットを選ぶか、「新しく作る」を押してください。下の一覧からも選べます。", MessageType.None);
        }
        else
        {
            DrawTutorial();
        }

        EditorGUILayout.Space(8);
        DrawAllTutorials();
        EditorGUILayout.Space(8);
        DrawSceneSection();

        EditorGUILayout.EndScrollView();
    }

    // ─── 選んだチュートリアル ─────────────────────────────

    private void DrawTutorial()
    {
        var so = new SerializedObject(data);
        so.Update();
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.PropertyField(so.FindProperty("id"), new GUIContent("ID（セーブ用・後から変えない）"));
        EditorGUILayout.Space(4);

        DrawTiming(so.FindProperty("timing"));
        EditorGUILayout.Space(4);

        var pages = so.FindProperty("pages");
        for (int i = 0; i < pages.arraySize; i++)
        {
            if (DrawPage(pages, i)) break;   // 並べ替え・削除をしたら、この回の描画はここまで
        }

        if (GUILayout.Button("＋ ページを追加", GUILayout.Height(28)))
        {
            int n = pages.arraySize;
            pages.arraySize = n + 1;
            var p = pages.GetArrayElementAtIndex(n);   // 追加すると前のページの中身が複製されるので空にする
            p.FindPropertyRelative("image").objectReferenceValue = null;
            p.FindPropertyRelative("imageNote").stringValue = "";
            p.FindPropertyRelative("heading").stringValue = "";
            p.FindPropertyRelative("body").stringValue = "";
        }

        bool changed = EditorGUI.EndChangeCheck();
        so.ApplyModifiedProperties();

        // プレビュー中なら、書き換えをすぐ Canvas に反映する
        if (changed && previewIndex >= 0) Preview(previewIndex);
    }

    // 出すタイミング
    private void DrawTiming(SerializedProperty timing)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("出すタイミング", EditorStyles.boldLabel);

            var kindProp = timing.FindPropertyRelative("kind");
            EditorGUILayout.PropertyField(kindProp, new GUIContent("いつ出すか"));
            var kind = (TutorialTimingKind)kindProp.intValue;

            switch (kind)
            {
                case TutorialTimingKind.AtAge:
                    DrawAge(timing, "出す日");
                    break;

                case TutorialTimingKind.RunawayStart:
                    EditorGUILayout.PropertyField(timing.FindPropertyRelative("offsetDays"),
                        new GUIContent("ずらす日数", "-30 なら30日前（予告）、+10 なら10日後。0 で判定が始まる当日"));
                    break;

                case TutorialTimingKind.ChildStressAtLeast:
                    var stress = timing.FindPropertyRelative("threshold");
                    stress.intValue = EditorGUILayout.IntSlider(new GUIContent("ストレス（以上）"), stress.intValue, 0, 100);
                    DrawAge(timing, "この日から判定（0歳1月1日＝制限なし）");
                    break;

                case TutorialTimingKind.MoneyBelow:
                    EditorGUILayout.PropertyField(timing.FindPropertyRelative("threshold"), new GUIContent("金額（円・これより少ない）"));
                    DrawAge(timing, "この日から判定（0歳1月1日＝制限なし）");
                    break;

                case TutorialTimingKind.FlagSet:
                    EditorGUILayout.PropertyField(timing.FindPropertyRelative("targetFlag"), new GUIContent("対象フラグ"));
                    EditorGUILayout.PropertyField(timing.FindPropertyRelative("offsetDays"),
                        new GUIContent("ずらす日数", "-30 なら30日前、+10 なら10日後。0 でフラグが立った当日"));
                    break;
            }

            // 説明と、スケジュールに入っているか
            EditorGUILayout.LabelField("→ " + data.Timing.Describe(RunawayStartDay()), EditorStyles.wordWrappedLabel);
            DrawRegistration(kind);

            if (EditorApplication.isPlaying)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("今すぐ出す（テスト）") && TutorialManager.I != null) TutorialManager.I.Show(data);
                    if (GUILayout.Button("見た記録を消す") && TutorialManager.I != null) TutorialManager.I.Forget(data);
                }
            }
        }
    }

    // 歳・月・日の入力
    private static void DrawAge(SerializedProperty timing, string label)
    {
        var y = timing.FindPropertyRelative("ageYears");
        var m = timing.FindPropertyRelative("ageMonth");
        var d = timing.FindPropertyRelative("ageDay");
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel(label);
            y.intValue = Mathf.Max(0, EditorGUILayout.IntField(y.intValue, GUILayout.Width(44)));
            GUILayout.Label("歳", GUILayout.Width(18));
            m.intValue = EditorGUILayout.IntPopup(Mathf.Clamp(m.intValue, 1, 12), Numbers(1, 12), Values(1, 12), GUILayout.Width(44));
            GUILayout.Label("月", GUILayout.Width(18));
            int maxDay = GameDate.DaysInMonth(m.intValue);
            d.intValue = EditorGUILayout.IntPopup(Mathf.Clamp(d.intValue, 1, maxDay), Numbers(1, maxDay), Values(1, maxDay), GUILayout.Width(44));
            GUILayout.Label("日", GUILayout.Width(18));
            GUILayout.FlexibleSpace();
        }
    }

    private void DrawRegistration(TutorialTimingKind kind)
    {
        var scheduler = FindScheduler();
        if (kind == TutorialTimingKind.Manual)
        {
            EditorGUILayout.HelpBox("自動では出しません。出したい場面のスクリプトから TutorialManager.I.ShowOnce(データ) を呼んでください。", MessageType.None);
            return;
        }
        if (scheduler == null)
        {
            EditorGUILayout.HelpBox("このシーンに TutorialScheduler がありません。下の「シーンのチュートリアル画面」から画面を作ってください。", MessageType.Warning);
            return;
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (scheduler.Contains(data))
            {
                EditorGUILayout.LabelField("✓ シーンのスケジュールに登録されています");
                if (GUILayout.Button("外す", GUILayout.Width(60))) SetRegistered(scheduler, data, false);
            }
            else
            {
                EditorGUILayout.HelpBox("まだスケジュールに登録されていないので、このままでは出ません。", MessageType.Warning);
                if (GUILayout.Button("登録する", GUILayout.Width(80), GUILayout.Height(38))) SetRegistered(scheduler, data, true);
            }
        }
    }

    // 1ページぶん。並べ替え・削除をしたら true
    private bool DrawPage(SerializedProperty pages, int i)
    {
        var p = pages.GetArrayElementAtIndex(i);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                string mark = previewIndex == i ? "　（Canvas に表示中）" : "";
                EditorGUILayout.LabelField($"ページ {i + 1}{mark}", EditorStyles.boldLabel);
                if (GUILayout.Button("Canvas で見る", GUILayout.Width(100))) Preview(i);

                using (new EditorGUI.DisabledScope(i == 0))
                    if (GUILayout.Button("↑", GUILayout.Width(26))) { pages.MoveArrayElement(i, i - 1); return true; }
                using (new EditorGUI.DisabledScope(i == pages.arraySize - 1))
                    if (GUILayout.Button("↓", GUILayout.Width(26))) { pages.MoveArrayElement(i, i + 1); return true; }
                if (GUILayout.Button("削除", GUILayout.Width(44)))
                {
                    if (EditorUtility.DisplayDialog("チュートリアル", $"ページ {i + 1} を消しますか？", "消す", "やめる"))
                    {
                        pages.DeleteArrayElementAtIndex(i);
                        if (previewIndex >= pages.arraySize) previewIndex = pages.arraySize - 1;
                        // 消したページの「出す条件」がアセットの中に残らないよう、反映後に掃除する
                        EventConditionList.CleanupUnreferencedLater(pages.serializedObject.targetObject);
                        return true;
                    }
                }
            }

            var image = p.FindPropertyRelative("image");
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.PropertyField(image, new GUIContent("画像（ドット絵）"));
                    EditorGUILayout.PropertyField(p.FindPropertyRelative("imageNote"), new GUIContent("絵の指示メモ"));
                }
                var sprite = image.objectReferenceValue as Sprite;
                if (sprite != null)
                {
                    // サムネイルは後から読み込まれるので、場所だけは毎回同じだけ取る（Layout と Repaint で数がずれるとエラーになる）
                    var rect = GUILayoutUtility.GetRect(96, 64, GUILayout.Width(96), GUILayout.Height(64));
                    var tex = AssetPreview.GetAssetPreview(sprite);
                    if (tex != null) GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit);
                    else Repaint();
                }
            }
            EditorGUILayout.PropertyField(p.FindPropertyRelative("heading"), new GUIContent("見出し"));
            EditorGUILayout.PropertyField(p.FindPropertyRelative("body"), new GUIContent("説明文"));

            var showConds = p.FindPropertyRelative("showConditions");
            if (showConds != null)
            {
                var host = pages.serializedObject.targetObject;
                EventConditionList.Draw(showConds, host, "このページを出す条件（任意・すべて満たしたとき表示）");
            }
        }
        return false;
    }

    // ─── すべてのチュートリアルの一覧 ───────────────────────

    private void DrawAllTutorials()
    {
        showAll = EditorGUILayout.Foldout(showAll, "すべてのチュートリアル（いつ出るか）", true, EditorStyles.foldoutHeader);
        if (!showAll) return;

        var scheduler = FindScheduler();
        int runawayDay = RunawayStartDay();
        var list = new List<TutorialData>();
        foreach (var guid in AssetDatabase.FindAssets("t:TutorialData"))
        {
            var t = AssetDatabase.LoadAssetAtPath<TutorialData>(AssetDatabase.GUIDToAssetPath(guid));
            if (t != null) list.Add(t);
        }
        list.Sort((a, b) => SortKey(a, runawayDay).CompareTo(SortKey(b, runawayDay)));

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (list.Count == 0) EditorGUILayout.LabelField("まだありません。");
            foreach (var t in list)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var style = t == data ? EditorStyles.boldLabel : EditorStyles.label;
                    if (GUILayout.Button(t.name, style, GUILayout.Width(150))) { data = t; previewIndex = -1; }

                    string state;
                    if (t.Timing.kind == TutorialTimingKind.Manual) state = "コード";
                    else if (scheduler != null && scheduler.Contains(t)) state = "✓ 登録";
                    else state = "⚠ 未登録";
                    GUILayout.Label(state, GUILayout.Width(60));

                    GUILayout.Label(t.Timing.Describe(runawayDay), EditorStyles.wordWrappedLabel);
                }
            }
        }
    }

    // 一覧の並び順：出るのが早い順。コードから呼ぶものは最後
    private static int SortKey(TutorialData t, int runawayDay)
    {
        var tm = t.Timing;
        switch (tm.kind)
        {
            case TutorialTimingKind.GameStart: return -1;
            case TutorialTimingKind.AtAge: return tm.AgeDays;
            case TutorialTimingKind.RunawayStart: return (runawayDay >= 0 ? runawayDay : 0) + tm.offsetDays;
            case TutorialTimingKind.ChildStressAtLeast:
            case TutorialTimingKind.MoneyBelow: return tm.AgeDays;
            default: return int.MaxValue;
        }
    }

    // ─── シーン ───────────────────────────────────────────

    private void DrawSceneSection()
    {
        EditorGUILayout.LabelField("シーンのチュートリアル画面", EditorStyles.boldLabel);
        var panel = FindPanel();
        if (panel == null)
        {
            EditorGUILayout.HelpBox("このシーンにはチュートリアル画面がありません。", MessageType.Warning);
            if (GUILayout.Button("画面をシーンに作る")) TutorialUIBuilder.BuildMenu();
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("画面を選択")) Selection.activeObject = panel.gameObject;
            var scheduler = FindScheduler();
            using (new EditorGUI.DisabledScope(scheduler == null))
                if (GUILayout.Button("スケジュールを選択")) Selection.activeObject = scheduler.gameObject;
            using (new EditorGUI.DisabledScope(!panel.gameObject.activeSelf))
                if (GUILayout.Button("プレビューを閉じる")) ClosePreview(panel);
        }
        if (EditorApplication.isPlaying)
            EditorGUILayout.HelpBox("再生中は「Canvas で見る」は使えません。上の「今すぐ出す（テスト）」を使ってください。", MessageType.None);
    }

    private static void SetRegistered(TutorialScheduler scheduler, TutorialData tutorial, bool on)
    {
        var so = new SerializedObject(scheduler);
        var list = so.FindProperty("tutorials");
        int found = -1;
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == tutorial) { found = i; break; }

        if (on && found < 0)
        {
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = tutorial;
        }
        else if (!on && found >= 0)
        {
            list.GetArrayElementAtIndex(found).objectReferenceValue = null;   // 参照を消してから要素を消す
            list.DeleteArrayElementAtIndex(found);
        }
        so.ApplyModifiedProperties();
        if (!EditorApplication.isPlaying) EditorSceneManager.MarkSceneDirty(scheduler.gameObject.scene);
    }

    private void Preview(int index)
    {
        if (EditorApplication.isPlaying || data == null || data.PageCount == 0) return;
        var panel = FindPanel();
        if (panel == null)
        {
            EditorUtility.DisplayDialog("チュートリアル", "シーンにチュートリアル画面がありません。先に「画面をシーンに作る」を押してください。", "OK");
            return;
        }
        Undo.RegisterFullObjectHierarchyUndo(panel.gameObject, "チュートリアルのプレビュー");
        panel.PreviewInEditor(data, index);
        previewIndex = Mathf.Clamp(index, 0, data.PageCount - 1);
        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
        SceneView.RepaintAll();
    }

    private void ClosePreview(TutorialPanelUI panel)
    {
        Undo.RegisterFullObjectHierarchyUndo(panel.gameObject, "チュートリアルのプレビューを閉じる");
        panel.ClearPreview();
        previewIndex = -1;
        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
    }

    private static TutorialPanelUI FindPanel() =>
        Object.FindFirstObjectByType<TutorialPanelUI>(FindObjectsInactive.Include);

    private static TutorialScheduler FindScheduler() =>
        Object.FindFirstObjectByType<TutorialScheduler>(FindObjectsInactive.Include);

    private static int RunawayStartDay()
    {
        var w = Object.FindFirstObjectByType<RunawayWatcher>(FindObjectsInactive.Include);
        return w != null ? w.ActiveFromDay : -1;
    }

    private static string[] Numbers(int from, int to)
    {
        var a = new string[to - from + 1];
        for (int i = 0; i < a.Length; i++) a[i] = (from + i).ToString();
        return a;
    }

    private static int[] Values(int from, int to)
    {
        var a = new int[to - from + 1];
        for (int i = 0; i < a.Length; i++) a[i] = from + i;
        return a;
    }

    private void CreateNew()
    {
        string dir = TutorialUIBuilder.DataDir;
        if (!AssetDatabase.IsValidFolder(dir))
        {
            Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }
        string path = EditorUtility.SaveFilePanelInProject("チュートリアルを作る", "Tutorial_New", "asset",
            "ファイル名を決めてください（例：Tutorial_Food）", dir);
        if (string.IsNullOrEmpty(path)) return;

        var asset = CreateInstance<TutorialData>();
        AssetDatabase.CreateAsset(asset, path);
        var so = new SerializedObject(asset);
        so.FindProperty("pages").arraySize = 1;
        so.FindProperty("timing").FindPropertyRelative("kind").intValue = (int)TutorialTimingKind.AtAge;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();

        // 作ったらすぐスケジュールに登録しておく（タイミングを選ぶだけで出るように）
        var scheduler = FindScheduler();
        if (scheduler != null) SetRegistered(scheduler, asset, true);

        data = asset;
        previewIndex = -1;
        EditorGUIUtility.PingObject(asset);
    }
}

/// <summary>TutorialData の Inspector に「編集ウィンドウで開く」ボタンを足す。</summary>
[CustomEditor(typeof(TutorialData))]
public class TutorialDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("編集ウィンドウで開く", GUILayout.Height(26)))
            TutorialEditorWindow.OpenWith((TutorialData)target);
        var t = (TutorialData)target;
        EditorGUILayout.HelpBox("出すタイミング：" + t.Timing.Describe(), MessageType.None);
        EditorGUILayout.Space();
        DrawDefaultInspector();
    }
}
