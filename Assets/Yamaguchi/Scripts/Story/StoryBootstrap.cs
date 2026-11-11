using Maruyama.Events;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 本編シーン（EventManager があるシーン）が読み込まれたら、ストーリーの部品
/// （StoryState / StorySaveParticipant / StoryEventScheduler）を1つの GameObject「Story」にまとめて作る。
///
/// なぜ自動で作るか：
/// - シーンに置き忘れると「続き」「条件を満たしたら」のイベントが出ず、ストーリーもセーブされない。
/// - sceneLoaded はシーン内の Awake / OnEnable のあと、Start の前に呼ばれる。ここで作れば
///   StorySaveParticipant の登録（OnEnable）が GameStartup のロード（Start から1フレーム後）に間に合う。
/// - シーンと一緒に消える（DontDestroyOnLoad にしない）ので、タイトルに戻って「最初から」を選ぶと
///   前回のフラグ・履歴・予約は残らない。
///
/// シーンにすでに StoryState があれば何もしない（GameManager に手で付けた場合はそちらを使う）。
/// </summary>
public static class StoryBootstrap
{
    private const string ObjectName = "Story";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        // ドメインリロードを切っている設定でも二重登録しないように、一度外してから登録する
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 本編シーンだけ（タイトルやリザルトでは作らない）
        if (Object.FindFirstObjectByType<EventManager>(FindObjectsInactive.Include) == null) return;

        if (Object.FindFirstObjectByType<StoryState>(FindObjectsInactive.Include) != null)
        {
            // 手で置いてある。足りない部品だけ足す
            var existing = Object.FindFirstObjectByType<StoryState>(FindObjectsInactive.Include).gameObject;
            if (existing.GetComponent<StorySaveParticipant>() == null &&
                Object.FindFirstObjectByType<StorySaveParticipant>(FindObjectsInactive.Include) == null)
                existing.AddComponent<StorySaveParticipant>();
            if (Object.FindFirstObjectByType<StoryEventScheduler>(FindObjectsInactive.Include) == null)
                existing.AddComponent<StoryEventScheduler>();
            return;
        }

        var go = new GameObject(ObjectName);
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<StoryState>();
        go.AddComponent<StorySaveParticipant>();
        go.AddComponent<StoryEventScheduler>();
        Debug.Log($"[StoryBootstrap] シーン「{scene.name}」にストーリーの部品を作りました。");
    }
}
