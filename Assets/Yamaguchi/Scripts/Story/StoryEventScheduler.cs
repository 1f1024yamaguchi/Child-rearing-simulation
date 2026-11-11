using System.Collections.Generic;
using Maruyama.Events;
using UnityEngine;

/// <summary>
/// 毎日1回、StoryState に予約された「続き（FollowUp）」イベントと、
/// 条件を満たした「条件（OnCondition）」イベントを判定して EventManager に投入する。
/// 本編シーンでは StoryBootstrap が自動で作る（GameManager に手で付けてもよい）。シーンと一緒に消える。
/// </summary>
public class StoryEventScheduler : MonoBehaviour
{
    private static StoryEventScheduler instance;

    /// <summary>本編シーンにある StoryEventScheduler。無ければ null。</summary>
    public static StoryEventScheduler I => instance;

    [Header("読み込み設定")]
    [Tooltip("EventManager と同じパスからイベントアセットを読み込む")]
    [SerializeField] private string resourcesPath = "Events";

    // Resources から読み込んだイベントのキャッシュ
    private readonly Dictionary<string, EventData> eventMap = new Dictionary<string, EventData>();
    private readonly List<EventData> onConditionEvents = new List<EventData>();
    private bool initialized = false;
    private bool subscribed = false;

    /// <summary>
    /// 「条件を満たしたら」のイベントのうち、順番待ちに入れたがまだ選ばれていないもの（イベントid）。
    /// 「起きた」の記録は選択肢を押したときに付くので、押されるまでの間に日が進むと
    /// （年齢ジャンプは1フレームに何日も進む）毎日 Enqueue されてしまう。それを防ぐ。
    /// セーブはしない（ロード後にまだ条件を満たしていれば、もう一度出るのが正しい）。
    /// </summary>
    private readonly HashSet<string> queuedOnCondition = new HashSet<string>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[StoryEventScheduler] StoryEventScheduler が2つあります。後から来たほうを外します。", this);
            Destroy(this);
            return;
        }
        instance = this;
    }

    private void Start()
    {
        InitializeEvents();
        Subscribe();
    }

    private void OnEnable()
    {
        Subscribe();
        EventManager.OnEventResolved += HandleEventResolved;
    }

    private void OnDisable()
    {
        Unsubscribe();
        EventManager.OnEventResolved -= HandleEventResolved;
    }

    /// <summary>選ばれたイベントを「順番待ち中」から外す。</summary>
    private void HandleEventResolved(EventResolvedInfo info)
    {
        if (!string.IsNullOrEmpty(info.eventId)) queuedOnCondition.Remove(info.eventId);
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (instance == this) instance = null;
    }

    private void Subscribe()
    {
        if (subscribed) return;
        if (DayTicker.I != null)
        {
            DayTicker.I.OnDayPassed += HandleDayPassed;
            subscribed = true;
        }
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (DayTicker.I != null)
        {
            DayTicker.I.OnDayPassed -= HandleDayPassed;
        }
        subscribed = false;
    }

    /// <summary>Resources からイベント一覧を読み込んでキャッシュする。</summary>
    public void InitializeEvents()
    {
        if (initialized) return;
        eventMap.Clear();
        onConditionEvents.Clear();

        var allEvents = Resources.LoadAll<EventData>(resourcesPath);
        foreach (var e in allEvents)
        {
            if (e == null || string.IsNullOrEmpty(e.id)) continue;
            if (!eventMap.ContainsKey(e.id))
            {
                eventMap.Add(e.id, e);
            }

            if (e.trigger == EventTrigger.OnCondition)
            {
                onConditionEvents.Add(e);
            }
        }
        initialized = true;
        Debug.Log($"[StoryEventScheduler] イベント読み込み完了: 全 {eventMap.Count} 件 / OnCondition {onConditionEvents.Count} 件");
    }

    private void HandleDayPassed(int totalAgeDays)
    {
        InitializeEvents();

        // 1. 予約日が今日の「続き（FollowUp）」
        CheckFollowUpEvents(totalAgeDays);

        // 2. 「条件を満たしたら（OnCondition）」
        CheckOnConditionEvents();
    }

    private void CheckFollowUpEvents(int totalAgeDays)
    {
        if (StoryState.I == null) return;

        var due = StoryState.I.ConsumeScheduledForDay(totalAgeDays);
        if (due == null || due.Count == 0) return;

        foreach (var entry in due)
        {
            if (string.IsNullOrEmpty(entry.eventId)) continue;

            if (!eventMap.TryGetValue(entry.eventId, out var eventData) || eventData == null)
            {
                Debug.LogWarning($"[StoryEventScheduler] 予約されたイベント '{entry.eventId}' が Resources/{resourcesPath} に見つかりません。");
                continue;
            }

            // 条件を判定
            if (eventData.CheckConditions())
            {
                Debug.Log($"[StoryEventScheduler] 続きイベント '{eventData.title}' ({eventData.id}) を Enqueue します（day{totalAgeDays}）");
                if (EventManager.Instance != null)
                {
                    EventManager.Instance.Enqueue(eventData);
                }
                else
                {
                    Debug.LogWarning("[StoryEventScheduler] EventManager.Instance が見つかりません。");
                }
            }
            else
            {
                Debug.Log($"[StoryEventScheduler] 続きイベント '{eventData.title}' ({eventData.id}) は条件を満たさないため破棄されました（day{totalAgeDays}）。");
            }
        }
    }

    private void CheckOnConditionEvents()
    {
        if (StoryState.I == null) return;

        foreach (var e in onConditionEvents)
        {
            if (e == null) continue;

            // 順番待ちに入れたが、まだ選ばれていないものは入れ直さない
            if (queuedOnCondition.Contains(e.id)) continue;

            // まだ起きていなくて条件を満たしたら Enqueue（1回だけ）
            if (!StoryState.I.HasOccurred(e.id) && e.CheckConditions())
            {
                Debug.Log($"[StoryEventScheduler] 条件イベント '{e.title}' ({e.id}) の条件を満たしたため Enqueue します");
                if (EventManager.Instance != null)
                {
                    queuedOnCondition.Add(e.id);
                    EventManager.Instance.Enqueue(e);
                }
                else
                {
                    Debug.LogWarning("[StoryEventScheduler] EventManager.Instance が見つかりません。");
                }
            }
        }
    }
}

