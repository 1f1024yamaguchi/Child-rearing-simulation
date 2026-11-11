using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>再生中だけ出る欄：状態の確認と、テスト用の申請・サポート画面を開く・ログ出力。</summary>
public static class SupportPlayModeSection
{
    // 再生中は状態が刻々と変わるので、Layout のときだけ読み取って、Repaint では同じ内容を描く
    private static string stateText = "";
    private static string deadlineText = "";
    private static readonly List<string> unmet = new List<string>();
    private static string lastMessage = "";

    public static void Draw(SupportScheme s)
    {
        if (!EditorApplication.isPlaying || s == null) return;

        using (new SupportEditorGUI.BoxScope("再生中のテスト"))
        {
            var m = SupportManager.I;
            if (m == null)
            {
                EditorGUILayout.HelpBox("SupportManager が動いていません（このシーンに SupportManager がありません）。", MessageType.Warning);
                return;
            }
            if (string.IsNullOrEmpty(s.id))
            {
                EditorGUILayout.HelpBox("id が空の制度は、再生中に扱えません。", MessageType.Warning);
                return;
            }
            if (!SupportRegistry.IsRegistered(s))
            {
                EditorGUILayout.HelpBox("SupportManager に登録されていないので、テストできません。", MessageType.Warning);
                return;
            }

            if (Event.current.type == EventType.Layout) Snapshot(m, s);

            EditorGUILayout.LabelField("いまの状態", stateText);
            EditorGUILayout.LabelField("期限まで", deadlineText);
            if (unmet.Count > 0)
                EditorGUILayout.HelpBox("満たしていない申請条件：\n" + string.Join("\n", unmet), MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("申請する（テスト）"))
                {
                    if (m.CanApply(s))
                        lastMessage = m.Apply(s) ? "申請しました。" : "申請できませんでした。";
                    else
                        lastMessage = "申請できません：" + WhyNot(m, s);
                }
                if (GUILayout.Button("サポート画面でこの制度を開く"))
                {
                    var panel = Object.FindFirstObjectByType<SupportPanelController>(FindObjectsInactive.Include);
                    if (panel != null) panel.Open(s.id);
                    else lastMessage = "シーンに SupportPanelController がありません。";
                }
                if (GUILayout.Button("状態をログに出す"))
                {
                    Debug.Log($"[支援制度] {s.title}（{s.id}）：{stateText}／期限まで {deadlineText}" +
                              (unmet.Count > 0 ? "／満たしていない条件：" + string.Join("、", unmet) : ""), s);
                }
            }

            if (!string.IsNullOrEmpty(lastMessage))
                EditorGUILayout.HelpBox(lastMessage, MessageType.Info);

            EditorGUILayout.LabelField("※ 再生中に変えた値はアセットに残ります（再生を止めても元に戻りません）。", EditorStyles.wordWrappedMiniLabel);
        }
    }

    private static void Snapshot(SupportManager m, SupportScheme s)
    {
        stateText = StateName(m.GetState(s));
        int days = m.DaysUntilDeadline(s);
        deadlineText = days < 0 ? "期限なし（または対象外）" : $"あと {days} 日";
        unmet.Clear();
        foreach (var r in m.UnmetRequirements(s))
            unmet.Add(SupportSchemeDescriber.SafeDescribe(r));
    }

    private static string WhyNot(SupportManager m, SupportScheme s)
    {
        var state = m.GetState(s);
        if (state != SupportState.Available)
            return $"いまの状態が「{StateName(state)}」です（申請できるのは「申請できる」のときだけ）";
        var reasons = new List<string>();
        foreach (var r in m.UnmetRequirements(s)) reasons.Add(SupportSchemeDescriber.SafeDescribe(r));
        if (reasons.Count > 0) return "申請条件を満たしていません：" + string.Join("、", reasons);
        return "働き方の条件（産休・育休など）を満たしていません";
    }

    private static string StateName(SupportState st)
    {
        switch (st)
        {
            case SupportState.Locked: return "申請できない";
            case SupportState.Available: return "申請できる";
            case SupportState.Received: return "受け取り済み";
            case SupportState.Active: return "受給中";
            case SupportState.Suspended: return "支給停止";
            case SupportState.Expired: return "期限切れ";
            case SupportState.Lost: return "受給資格なし";
            case SupportState.Ended: return "受給終了";
            default: return st.ToString();
        }
    }
}
