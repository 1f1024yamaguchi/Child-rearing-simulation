using Maruyama.Events;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 本編シーン（EventManager があるシーン）が読み込まれたら、学校と進路の部品（TermTestSystem / PathSystem / PathPanelUI）を
/// 1つの GameObject「School」にまとめて作る。シーンに手で置かなくてよい（StoryBootstrap・EndingFlow と同じ考え方）。
/// sceneLoaded は Start より前なので、セーブへの登録（OnEnable）がロードに間に合う。
/// </summary>
public static class SchoolBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Object.FindFirstObjectByType<EventManager>(FindObjectsInactive.Include) == null) return;
        bool hasTest = Object.FindFirstObjectByType<TermTestSystem>(FindObjectsInactive.Include) != null;
        bool hasPath = Object.FindFirstObjectByType<PathSystem>(FindObjectsInactive.Include) != null;
        bool hasPanel = Object.FindFirstObjectByType<PathPanelUI>(FindObjectsInactive.Include) != null;
        if (hasTest && hasPath && hasPanel) return;

        var go = new GameObject("School");
        SceneManager.MoveGameObjectToScene(go, scene);
        if (!hasTest) go.AddComponent<TermTestSystem>();
        if (!hasPath) go.AddComponent<PathSystem>();
        if (!hasPanel) go.AddComponent<PathPanelUI>();
    }
}
