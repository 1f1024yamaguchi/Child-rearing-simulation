using Maruyama.Events;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 編集ウィンドウの［再生して今すぐ出す］用。
/// 再生していないときに押されたら、出したいイベントを覚えて再生を始め、
/// ゲームの準備ができたところで EventManager.ShowNow で画面に出す。
/// </summary>
[InitializeOnLoad]
public static class EventPreviewLauncher
{
    private const string KeyGuid = "Kosodate.EventPreview.Guid";
    private const double TimeoutSeconds = 30.0;
    private static double startedAt;

    static EventPreviewLauncher()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    /// <summary>このイベントを覚えて、再生を始める。</summary>
    public static void RequestAndPlay(EventData e)
    {
        if (e == null) return;
        string path = AssetDatabase.GetAssetPath(e);
        if (string.IsNullOrEmpty(path)) return;
        SessionState.SetString(KeyGuid, AssetDatabase.AssetPathToGUID(path));
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            if (string.IsNullOrEmpty(SessionState.GetString(KeyGuid, ""))) return;
            startedAt = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= Poll;
            SessionState.EraseString(KeyGuid);
        }
    }

    private static void Poll()
    {
        string guid = SessionState.GetString(KeyGuid, "");
        if (string.IsNullOrEmpty(guid) || !EditorApplication.isPlaying)
        {
            EditorApplication.update -= Poll;
            return;
        }

        if (EditorApplication.timeSinceStartup - startedAt > TimeoutSeconds)
        {
            Debug.LogWarning("[イベント確認] EventManager が見つからないため、イベントを出せませんでした。開いているシーンに EventManager があるか確認してください。");
            EditorApplication.update -= Poll;
            SessionState.EraseString(KeyGuid);
            return;
        }

        // ゲームの準備（シーンの読み込み・初期化）が落ち着くまで少し待つ
        var manager = EventManager.Instance;
        if (manager == null || Time.timeSinceLevelLoad < 1f) return;

        EditorApplication.update -= Poll;
        SessionState.EraseString(KeyGuid);
        var e = AssetDatabase.LoadAssetAtPath<EventData>(AssetDatabase.GUIDToAssetPath(guid));
        if (e != null) manager.ShowNow(e);
    }
}
