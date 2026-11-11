using UnityEditor;
using UnityEngine;

/// <summary>
/// メニュー「子育て → 支援制度 → 編集ウィンドウ」。
/// 支援制度（SupportScheme）を、スクリプトを触らずに作る・直す・チェックする・SupportManager に登録する。
/// 各欄の中身は Support*Section.cs に分けてある。
/// </summary>
public class SupportEditorWindow : EditorWindow
{
    private SupportScheme scheme;
    private Vector2 scroll;

    [MenuItem("子育て/支援制度/編集ウィンドウ", false, 3)]
    public static void OpenWindow() => GetWindow<SupportEditorWindow>("支援制度");

    /// <summary>指定の制度を開いた状態でウィンドウを出す（SupportScheme の Inspector から呼ぶ）。</summary>
    public static void OpenWith(SupportScheme s)
    {
        var w = GetWindow<SupportEditorWindow>("支援制度");
        w.scheme = s;
        w.Repaint();
    }

    private void OnEnable()
    {
        Undo.undoRedoPerformed += Repaint;
        if (scheme == null && Selection.activeObject is SupportScheme selected) scheme = selected;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= Repaint;
        SupportRequirementList.ClearCache();
    }

    private void OnSelectionChange()
    {
        if (Selection.activeObject is SupportScheme selected && selected != scheme)
        {
            scheme = selected;
            Repaint();
        }
    }

    private void OnProjectChange()
    {
        SupportRegistry.Invalidate();
        Repaint();
    }

    // 再生中は状態が変わるので、定期的に描き直す
    private void OnInspectorUpdate()
    {
        if (EditorApplication.isPlaying) Repaint();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.HelpBox(
            "支援制度（児童手当・育休など）の中身を、ここで作って直します。\n" +
            "「使う項目」のスイッチをONにした欄だけが下に出ます。いちばん下の「チェック」で、直すところが分かります。\n" +
            "値の書き換えは元に戻せます（Ctrl+Z）。",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            var picked = (SupportScheme)EditorGUILayout.ObjectField("制度", scheme, typeof(SupportScheme), false);
            if (picked != scheme) scheme = picked;

            if (GUILayout.Button("新しく作る ▼", GUILayout.Width(100))) ShowTemplateMenu();
            using (new EditorGUI.DisabledScope(scheme == null))
            {
                if (GUILayout.Button("複製して作る", GUILayout.Width(100)))
                {
                    var copy = SupportSchemeFactory.Duplicate(scheme);
                    if (copy != null) scheme = copy;
                }
            }
        }

        if (scheme == null)
        {
            EditorGUILayout.HelpBox("制度のアセットを選ぶか、「新しく作る」を押してください。下の一覧からも選べます。", MessageType.None);
        }
        else
        {
            DrawScheme();
        }

        EditorGUILayout.Space(8);
        var fromList = SupportListSection.Draw(scheme);
        if (fromList != null)
        {
            scheme = fromList;
            Selection.activeObject = fromList;
            GUI.FocusControl(null);
        }

        EditorGUILayout.EndScrollView();
    }

    private void ShowTemplateMenu()
    {
        var menu = new GenericMenu();
        var values = (SupportSchemeFactory.Template[])System.Enum.GetValues(typeof(SupportSchemeFactory.Template));
        foreach (var t in values)
        {
            var template = t;
            menu.AddItem(new GUIContent(SupportSchemeFactory.TemplateNames[(int)template]), false, () =>
            {
                var created = SupportSchemeFactory.Create(template);
                if (created != null)
                {
                    scheme = created;
                    Repaint();
                }
            });
        }
        menu.ShowAsContext();
    }

    private void DrawScheme()
    {
        var so = new SerializedObject(scheme);
        so.Update();

        // まとめ
        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(SupportSchemeDescriber.Summary(scheme), MessageType.None);

        SupportBasicSection.Draw(so, this);
        SupportPaymentSection.DrawTiming(so);
        var features = SupportFeatureToggles.Draw(so);
        SupportPeriodSection.Draw(so);
        SupportRequirementList.Draw(so.FindProperty("applyRequirements"), scheme, "申請条件（全部満たしたら申請できる）");
        SupportPaymentSection.Draw(so, features, scheme);
        SupportCoverSection.Draw(so, features);
        SupportWorkSection.Draw(so, features);

        so.ApplyModifiedProperties();

        DrawValidation();
        SupportRegistry.DrawGUI(scheme);
        SupportPlayModeSection.Draw(scheme);
    }

    private void DrawValidation()
    {
        using (new SupportEditorGUI.BoxScope("チェック"))
        {
            var issues = SupportSchemeValidator.Check(scheme);
            var errors = new System.Text.StringBuilder();
            var warnings = new System.Text.StringBuilder();
            foreach (var i in issues)
            {
                var sb = i.error ? errors : warnings;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(i.error ? "✕ " : "⚠ ").Append(i.text);
            }

            if (errors.Length > 0) EditorGUILayout.HelpBox(errors.ToString(), MessageType.Error);
            if (warnings.Length > 0) EditorGUILayout.HelpBox(warnings.ToString(), MessageType.Warning);
            if (issues.Count == 0) EditorGUILayout.HelpBox("✓ 問題は見つかりませんでした。", MessageType.Info);
        }
    }
}
