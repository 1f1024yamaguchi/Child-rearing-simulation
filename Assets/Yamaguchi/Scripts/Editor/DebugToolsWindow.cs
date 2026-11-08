using UnityEditor;
using UnityEngine;

/// <summary>
/// メニュー「子育て → デバッグツール」。再生中に使う。
/// ・全部オート：判断画面（食事・イベント・税金・赤字警告・ミニゲーム）と支援制度の申請を自動で行う
/// ・ジャンプ：指定した年齢まで、毎日の処理を全部通しながら早送りする
/// 中身の処理は DebugAutoPilot / DebugTimeJump が持つ。このウィンドウは表示と操作だけ。
/// </summary>
public class DebugToolsWindow : EditorWindow
{
    private int targetYear = 10;
    private int targetMonth = 1;
    private int targetDay = 1;

    private static readonly int[] PresetYears = { 1, 3, 6, 10, 12, 15, 18 };

    [MenuItem("子育て/デバッグツール")]
    private static void Open()
    {
        var w = GetWindow<DebugToolsWindow>("デバッグツール");
        w.minSize = new Vector2(320f, 360f);
    }

    // 再生中は進み具合が変わるので、定期的に描き直す
    private void OnInspectorUpdate()
    {
        if (Application.isPlaying) Repaint();
    }

    private void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("本編シーンを再生すると使えます。", MessageType.Info);
            return;
        }

        var gs = GameState.I;
        if (gs == null)
        {
            EditorGUILayout.HelpBox("GameState が見つかりません。本編シーン（GameManager のあるシーン）を再生してください。", MessageType.Warning);
            return;
        }

        DebugTools.Ensure();
        var pilot = DebugAutoPilot.I;
        var jump = DebugTimeJump.I;

        DrawStatus(gs);
        EditorGUILayout.Space(8);
        DrawAuto(pilot);
        EditorGUILayout.Space(8);
        DrawJump(gs, jump);
    }

    private void DrawStatus(GameState gs)
    {
        EditorGUILayout.LabelField("いま", EditorStyles.boldLabel);
        var clock = GameClock.I;
        string run = clock == null ? "?" : clock.IsRunning ? "進行中" : $"停止中（止めている画面 {clock.PauseRequests}）";
        EditorGUILayout.LabelField("日付", GameDate.Format(gs.AgeDays));
        EditorGUILayout.LabelField("お金", $"{gs.Money:N0} 円");
        EditorGUILayout.LabelField("時間", run);
        if (gs.IsGameOver) EditorGUILayout.HelpBox("ゲームは終了しています。", MessageType.Warning);
    }

    private void DrawAuto(DebugAutoPilot pilot)
    {
        EditorGUILayout.LabelField("全部オート", EditorStyles.boldLabel);
        pilot.AutoEnabled = EditorGUILayout.Toggle("オート", pilot.AutoEnabled);
        using (new EditorGUI.IndentLevelScope())
        {
            pilot.EventChoice = (DebugDialogResolver.ChoicePolicy)EditorGUILayout.EnumPopup("イベントの選択肢", pilot.EventChoice);
            pilot.AutoApplySupports = EditorGUILayout.Toggle("支援制度を自動申請", pilot.AutoApplySupports);
            using (new EditorGUI.DisabledScope(!pilot.AutoApplySupports))
            {
                pilot.IncludeWorkModeSchemes = EditorGUILayout.Toggle(new GUIContent("育休・時短も申請", "親の働き方が変わる制度も申請する"), pilot.IncludeWorkModeSchemes);
            }
        }
        EditorGUILayout.LabelField("処理した画面", $"{pilot.ResolvedCount} 回　／　制度の操作 {pilot.SupportActionCount} 回");
        EditorGUILayout.HelpBox("食事＝先月と同じ（初回は普通）、税金＝提出、赤字警告＝閉じる、ミニゲーム＝スキップ（効果なし）。", MessageType.None);
    }

    private void DrawJump(GameState gs, DebugTimeJump jump)
    {
        EditorGUILayout.LabelField("指定の年齢までジャンプ", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            targetYear = Mathf.Clamp(EditorGUILayout.IntField(targetYear, GUILayout.Width(40)), 0, 20);
            GUILayout.Label("歳", GUILayout.Width(18));
            targetMonth = Mathf.Clamp(EditorGUILayout.IntField(targetMonth, GUILayout.Width(30)), 1, 12);
            GUILayout.Label("月", GUILayout.Width(18));
            targetDay = Mathf.Clamp(EditorGUILayout.IntField(targetDay, GUILayout.Width(30)), 1, GameDate.DaysInMonth(targetMonth));
            GUILayout.Label("日", GUILayout.Width(18));
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            foreach (int y in PresetYears)
            {
                if (GUILayout.Button($"{y}歳", EditorStyles.miniButton))
                {
                    targetYear = y; targetMonth = 1; targetDay = 1;
                }
            }
        }

        jump.DaysPerFrame = EditorGUILayout.IntSlider("1フレームの日数", jump.DaysPerFrame, 1, 60);
        jump.AutoDuringJump = EditorGUILayout.Toggle(new GUIContent("ジャンプ中はオート", "OFF にすると判断画面のたびに止まって待つ"), jump.AutoDuringJump);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(jump.IsJumping || gs.IsGameOver))
            {
                if (GUILayout.Button($"{targetYear}歳{targetMonth}月{targetDay}日 へジャンプ", GUILayout.Height(26)))
                {
                    jump.JumpTo(targetYear, targetMonth, targetDay);
                }
            }
            using (new EditorGUI.DisabledScope(!jump.IsJumping))
            {
                if (GUILayout.Button("止める", GUILayout.Width(60), GUILayout.Height(26))) jump.Stop();
            }
        }

        if (jump.IsJumping)
        {
            var r = EditorGUILayout.GetControlRect(false, 18);
            EditorGUI.ProgressBar(r, jump.Progress, $"{GameDate.Format(gs.AgeDays)} → {GameDate.Format(jump.TargetAgeDays)}");
        }
        else if (!string.IsNullOrEmpty(jump.LastResult))
        {
            EditorGUILayout.HelpBox(jump.LastResult, MessageType.None);
        }
    }
}
