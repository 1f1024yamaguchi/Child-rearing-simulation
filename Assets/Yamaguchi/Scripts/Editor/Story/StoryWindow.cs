using UnityEditor;
using UnityEngine;

/// <summary>
/// ストーリーウィンドウ（story-tool.md）。イベント同士のつながり全体を見て、フラグを管理する。
/// このクラスはメニューとタブの切り替えだけ。中身は各タブのクラス。
/// ランタイムには何も足さない（変更は StoryState / DreamSystem の窓口を通す）。
/// </summary>
public class StoryWindow : EditorWindow
{
    private static readonly string[] TabNames = { "フラグ", "流れ図", "ストーリーの文章", "再生中の状態" };
    private const string TabPrefKey = "Kosodate.StoryWindow.Tab";

    private int tab;
    private readonly StoryFlagTab flagTab = new StoryFlagTab();
    private readonly StoryPlayModeTab playTab = new StoryPlayModeTab();
    private readonly StoryGraphTab graphTab = new StoryGraphTab();
    private readonly StoryTextTab textTab = new StoryTextTab();

    [MenuItem("子育て/ストーリー/ストーリーウィンドウ", false, 5)]
    public static void OpenWindow()
    {
        var w = GetWindow<StoryWindow>("ストーリー");
        w.minSize = new Vector2(520, 360);
    }

    /// <summary>フラグを選んだ状態で開く（ほかのウィンドウから呼ぶ用）。</summary>
    public static void OpenFlag(StoryFlag flag)
    {
        var w = GetWindow<StoryWindow>("ストーリー");
        w.tab = 0;
        w.flagTab.Select(flag);
        w.Repaint();
    }

    private void OnEnable()
    {
        tab = EditorPrefs.GetInt(TabPrefKey, 0);
        graphTab.openFlag = f => { tab = 0; flagTab.Select(f); Repaint(); };
        EditorApplication.projectChanged += HandleProjectChanged;
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= HandleProjectChanged;
    }

    private void HandleProjectChanged()
    {
        StoryIndex.Invalidate();
        Repaint();
    }

    // 再生中は値が毎日変わるので、ときどき描き直す
    private void OnInspectorUpdate()
    {
        if (Application.isPlaying && tab == 3) Repaint();
    }

    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        tab = GUILayout.Toolbar(tab, TabNames, GUILayout.Height(24));
        if (EditorGUI.EndChangeCheck()) EditorPrefs.SetInt(TabPrefKey, tab);

        EditorGUILayout.Space(4);
        switch (tab)
        {
            case 0: flagTab.Draw(position); break;
            case 1: graphTab.Draw(position); break;
            case 2: textTab.Draw(position); break;
            case 3: playTab.Draw(); break;
        }
    }
}
