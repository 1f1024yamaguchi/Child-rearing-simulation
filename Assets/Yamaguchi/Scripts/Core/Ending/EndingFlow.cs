using System.Collections;
using Maruyama.Events;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ゲームが終わったら（巣立ち・家出・家計破綻）、時間を止め、値を写し、育成記録に保存して end シーンへ移る（result-scene.md 2章）。
/// 本編シーン（EventManager があるシーン）が読み込まれたら自動で作る。シーンに置かなくてよい。
/// </summary>
public class EndingFlow : MonoBehaviour
{
    [SerializeField] private float waitSeconds = 2f;
    [SerializeField] private string endSceneName = "end";

    private bool started;
    private bool subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindFirstObjectByType<EventManager>(FindObjectsInactive.Include) == null) return;
        if (FindFirstObjectByType<EndingFlow>(FindObjectsInactive.Include) != null) return;
        var go = new GameObject("EndingFlow");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<EndingFlow>();
    }

    private void OnEnable() => Subscribe();
    private void Start() => Subscribe();

    private void OnDisable()
    {
        if (subscribed && GameState.I != null) GameState.I.OnGameEnded -= HandleGameEnded;
        subscribed = false;
    }

    private void Subscribe()
    {
        if (subscribed || GameState.I == null) return;
        GameState.I.OnGameEnded += HandleGameEnded;
        subscribed = true;
    }

    private void HandleGameEnded(GameEndReason reason)
    {
        if (started) return;
        started = true;
        if (GameClock.I != null) GameClock.I.SetRunning(false);

        var result = EndingResultBuilder.Build(reason);
        RecordStore.Add(result);
        ResultCarrier.Set(result);
        Debug.Log($"[EndingFlow] {reason}：{result.childName} {result.GetAgeText()} 職業「{result.careerName}」夢「{result.dreamName}」→ {endSceneName} へ");
        StartCoroutine(LoadEndScene());
    }

    private IEnumerator LoadEndScene()
    {
        yield return new WaitForSecondsRealtime(waitSeconds);
        SceneManager.LoadScene(endSceneName);
    }

    [ContextMenu("Debug/今すぐ巣立ち")] private void DebugGoal() => GameState.I?.EndGame(GameEndReason.GoalReached);
    [ContextMenu("Debug/今すぐ家出")] private void DebugRunaway() => GameState.I?.EndGame(GameEndReason.Runaway);
    [ContextMenu("Debug/今すぐ家計破綻")] private void DebugBankrupt() => GameState.I?.EndGame(GameEndReason.Bankrupt);
}
