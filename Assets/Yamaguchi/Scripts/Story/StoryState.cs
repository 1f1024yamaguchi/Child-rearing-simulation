using System;
using System.Collections.Generic;
using Maruyama.Events;
using UnityEngine;

/// <summary>
/// ストーリー分岐の記録（起きたイベント、選んだ選択肢、フラグ、予約中の続き）を管理するシングルトン。
/// EventManager.OnEventResolved を購読して自動的にイベントの発生や選択肢を記録する。
/// </summary>
public class StoryState : MonoBehaviour
{
    private static StoryState instance;

    /// <summary>
    /// 本編シーンにある StoryState。無ければ null（呼ぶ側で null を確認すること）。
    /// 本編シーンでは StoryBootstrap が自動で作る（GameManager に手で付けてもよい）。
    /// シーンと一緒に消えるので、タイトルに戻って「最初から」を選ぶと記録はまっさらになる。
    /// </summary>
    public static StoryState I => instance;

    // ─── 記録データ構造 ─────────────────────────────────

    [Serializable]
    public class EventOccurredRecord
    {
        public string eventId;
        public int count;
        public int lastAgeDay;
    }

    [Serializable]
    public class ChoiceRecord
    {
        public string eventId;
        public string choiceId;
        public int ageDay;
    }

    [Serializable]
    public class ScheduledEventEntry
    {
        public string eventId;
        public int scheduledAgeDay;
    }

    [Serializable]
    public class FlagSetDayEntry
    {
        public string flagId;
        public int day;
    }

    // 内部データ
    private readonly Dictionary<string, EventOccurredRecord> occurredEvents = new Dictionary<string, EventOccurredRecord>();
    private readonly Dictionary<string, ChoiceRecord> chosenChoices = new Dictionary<string, ChoiceRecord>();
    private readonly HashSet<string> activeFlags = new HashSet<string>();
    private readonly List<string> activeFlagOrder = new List<string>();
    private readonly Dictionary<string, int> flagSetDays = new Dictionary<string, int>();
    private readonly Dictionary<string, StoryFlag> flagAssetCache = new Dictionary<string, StoryFlag>();
    private readonly List<ScheduledEventEntry> scheduledEvents = new List<ScheduledEventEntry>();

    [Header("デバッグ表示（Inspector確認用）")]
    [SerializeField] private List<EventOccurredRecord> inspectorOccurred = new List<EventOccurredRecord>();
    [SerializeField] private List<ChoiceRecord> inspectorChoices = new List<ChoiceRecord>();
    [SerializeField] private List<string> inspectorFlags = new List<string>();

    // ─── 変更通知イベント ─────────────────────────────────

    /// <summary>フラグの状態が変更されたときの通知</summary>
    public event Action<StoryFlag, bool> OnFlagChanged;

    /// <summary>フラグIDによる変更通知</summary>
    public event Action<string, bool> OnFlagIdChanged;

    /// <summary>イベントで選択肢が選ばれたときの通知</summary>
    public event Action<EventResolvedInfo> OnChoiceRecorded;

    /// <summary>
    /// ロード（RestoreSaveData）や初期化（Clear）で記録がまとめて入れ替わったときの通知。
    /// このとき OnFlagChanged は1件ずつは出ないので、フラグで表示を変えている画面（趣味の呼び名など）はこれで描き直す。
    /// </summary>
    public event Action OnRestored;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[StoryState] StoryState が2つあります。後から来たほうを外します。", this);
            Destroy(this);
            return;
        }
        instance = this;
    }

    private void Start()
    {
        // 置き忘れに気づけるように（続き・条件のイベントが出なくなるため）
        if (StoryEventScheduler.I == null)
            Debug.LogWarning("[StoryState] StoryEventScheduler がシーンにありません。「続き」「条件を満たしたら」のイベントが出ません。");
    }

    private void OnEnable()
    {
        EventManager.OnEventResolved += HandleEventResolved;
    }

    private void OnDisable()
    {
        EventManager.OnEventResolved -= HandleEventResolved;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void HandleEventResolved(EventResolvedInfo info)
    {
        if (string.IsNullOrEmpty(info.eventId)) return;

        // 起きたイベントの記録
        if (occurredEvents.TryGetValue(info.eventId, out var rec))
        {
            rec.count++;
            rec.lastAgeDay = info.ageDays;
        }
        else
        {
            rec = new EventOccurredRecord
            {
                eventId = info.eventId,
                count = 1,
                lastAgeDay = info.ageDays
            };
            occurredEvents[info.eventId] = rec;
        }

        // 選んだ選択肢の記録（最新で上書き）
        var choiceRec = new ChoiceRecord
        {
            eventId = info.eventId,
            choiceId = info.choiceId,
            ageDay = info.ageDays
        };
        chosenChoices[info.eventId] = choiceRec;

        SyncInspectorLists();

        Debug.Log($"[StoryState] イベント記録: {info.eventId}（{info.title}） 選択肢: {info.choiceId}（{info.choiceLabel}） 累計: {rec.count}回");

        OnChoiceRecorded?.Invoke(info);
    }

    // ─── 窓口 ─────────────────────────────────────────────

    /// <summary>イベントが過去に1回以上起きたか</summary>
    public bool HasOccurred(string eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return false;
        return occurredEvents.TryGetValue(eventId, out var rec) && rec.count > 0;
    }

    /// <summary>イベントが過去に起きた回数</summary>
    public int TimesOccurred(string eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return 0;
        return occurredEvents.TryGetValue(eventId, out var rec) ? rec.count : 0;
    }

    /// <summary>そのイベントで最後に選んだ選択肢ID（選んでいなければ null）</summary>
    public string ChoiceOf(string eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return null;
        return chosenChoices.TryGetValue(eventId, out var rec) ? rec.choiceId : null;
    }

    /// <summary>フラグが立っているか</summary>
    public bool HasFlag(StoryFlag flag)
    {
        if (flag == null) return false;
        return HasFlag(flag.id);
    }

    /// <summary>フラグIDが立っているか</summary>
    public bool HasFlag(string flagId)
    {
        if (string.IsNullOrEmpty(flagId)) return false;
        return activeFlags.Contains(flagId);
    }

    /// <summary>フラグが立った通算日数を取得する。立っていなければ -1 を返す。</summary>
    public int GetFlagSetDay(string flagId)
    {
        if (string.IsNullOrEmpty(flagId)) return -1;
        return flagSetDays.TryGetValue(flagId, out int day) ? day : -1;
    }

    /// <summary>立っているフラグの順序リスト（後から立ったものが後ろ）。</summary>
    public IReadOnlyList<string> ActiveFlagOrder => activeFlagOrder;

    /// <summary>フラグのアセット定義を取得する（キャッシュまたは Resources から検索）。</summary>
    public StoryFlag GetFlagAsset(string flagId)
    {
        if (string.IsNullOrEmpty(flagId)) return null;
        if (flagAssetCache.TryGetValue(flagId, out var cached) && cached != null)
            return cached;

        var allFlags = Resources.LoadAll<StoryFlag>("");
        foreach (var f in allFlags)
        {
            if (f != null && f.id == flagId)
            {
                flagAssetCache[flagId] = f;
                return f;
            }
        }
        return null;
    }

    /// <summary>フラグを立てる</summary>
    public void SetFlag(StoryFlag flag)
    {
        if (flag == null || string.IsNullOrEmpty(flag.id)) return;
        flagAssetCache[flag.id] = flag;
        SetFlag(flag.id, flag);
    }

    /// <summary>フラグIDでフラグを立てる</summary>
    public void SetFlag(string flagId, StoryFlag flagRef = null)
    {
        if (string.IsNullOrEmpty(flagId)) return;
        if (flagRef != null) flagAssetCache[flagId] = flagRef;

        int currentDay = GetCurrentDay();
        flagSetDays[flagId] = currentDay;

        if (activeFlags.Add(flagId))
        {
            activeFlagOrder.Remove(flagId); // 念のため既存を除去
            activeFlagOrder.Add(flagId);

            SyncInspectorLists();
            string disp = flagRef != null ? flagRef.displayName : flagId;
            Debug.Log($"[StoryState] フラグ「{disp} ({flagId})」が立ちました（{currentDay}日目）。");
            OnFlagChanged?.Invoke(flagRef ?? GetFlagAsset(flagId), true);
            OnFlagIdChanged?.Invoke(flagId, true);
        }
    }

    /// <summary>フラグを消す</summary>
    public void ClearFlag(StoryFlag flag)
    {
        if (flag == null || string.IsNullOrEmpty(flag.id)) return;
        ClearFlag(flag.id, flag);
    }

    /// <summary>フラグIDでフラグを消す</summary>
    public void ClearFlag(string flagId, StoryFlag flagRef = null)
    {
        if (string.IsNullOrEmpty(flagId)) return;
        if (flagRef != null) flagAssetCache[flagId] = flagRef;

        if (activeFlags.Remove(flagId))
        {
            activeFlagOrder.Remove(flagId);
            flagSetDays.Remove(flagId);

            SyncInspectorLists();
            string disp = flagRef != null ? flagRef.displayName : flagId;
            Debug.Log($"[StoryState] フラグ「{disp} ({flagId})」が解除されました。");
            OnFlagChanged?.Invoke(flagRef ?? GetFlagAsset(flagId), false);
            OnFlagIdChanged?.Invoke(flagId, false);
        }
    }

    private int GetCurrentDay()
    {
        if (GameState.I != null) return Mathf.FloorToInt(GameState.I.AgeDays);
        return 0;
    }

    // ─── 続きの予約（④で使用。入れ物だけ今作る）─────────

    public void Schedule(string eventId, int scheduledAgeDay)
    {
        if (string.IsNullOrEmpty(eventId)) return;
        scheduledEvents.Add(new ScheduledEventEntry { eventId = eventId, scheduledAgeDay = scheduledAgeDay });
        SyncInspectorLists();
    }

    /// <summary>EventData を直接指定して予約するオーバーロード</summary>
    public void Schedule(EventData eventData, int scheduledAgeDay)
    {
        if (eventData == null) return;
        Schedule(eventData.id, scheduledAgeDay);
    }

    /// <summary>
    /// 予約日が指定日（通算日数）以前のイベントをすべて取り出し、予約リストから削除する。
    /// 「== 今日」だと、0日後の予約（今日はもう終わっている）や日が飛んだときに永遠に出なくなるため「<=」。
    /// </summary>
    public List<ScheduledEventEntry> ConsumeScheduledForDay(int ageDay)
    {
        var due = new List<ScheduledEventEntry>();
        for (int i = scheduledEvents.Count - 1; i >= 0; i--)
        {
            if (scheduledEvents[i].scheduledAgeDay <= ageDay)
            {
                due.Add(scheduledEvents[i]);
                scheduledEvents.RemoveAt(i);
            }
        }
        due.Reverse(); // 元の登録順に戻す
        if (due.Count > 0) SyncInspectorLists();
        return due;
    }

    public IReadOnlyList<ScheduledEventEntry> ScheduledEvents => scheduledEvents;

    /// <summary>すべての記録を初期化する（ロード時・テスト用）</summary>
    public void Clear()
    {
        occurredEvents.Clear();
        chosenChoices.Clear();
        activeFlags.Clear();
        activeFlagOrder.Clear();
        flagSetDays.Clear();
        scheduledEvents.Clear();
        SyncInspectorLists();
        OnRestored?.Invoke();
    }

    private void SyncInspectorLists()
    {
        inspectorOccurred.Clear();
        foreach (var rec in occurredEvents.Values) inspectorOccurred.Add(rec);

        inspectorChoices.Clear();
        foreach (var rec in chosenChoices.Values) inspectorChoices.Add(rec);

        inspectorFlags.Clear();
        inspectorFlags.AddRange(activeFlagOrder);
    }

    // ─── セーブ・ロード連携用 ──────────────────────────────

    [Serializable]
    public class SaveData
    {
        public List<EventOccurredRecord> occurred = new List<EventOccurredRecord>();
        public List<ChoiceRecord> choices = new List<ChoiceRecord>();
        public List<string> flags = new List<string>();
        public List<string> flagOrder = new List<string>();
        public List<FlagSetDayEntry> flagSetDays = new List<FlagSetDayEntry>();
        public List<ScheduledEventEntry> scheduled = new List<ScheduledEventEntry>();
    }

    public SaveData CaptureSaveData()
    {
        var data = new SaveData
        {
            flags = new List<string>(activeFlags),
            flagOrder = new List<string>(activeFlagOrder),
            scheduled = new List<ScheduledEventEntry>(scheduledEvents)
        };
        foreach (var rec in occurredEvents.Values) data.occurred.Add(rec);
        foreach (var rec in chosenChoices.Values) data.choices.Add(rec);
        foreach (var kvp in flagSetDays)
        {
            data.flagSetDays.Add(new FlagSetDayEntry { flagId = kvp.Key, day = kvp.Value });
        }
        return data;
    }

    public void RestoreSaveData(SaveData data)
    {
        Clear();
        if (data == null) return;

        if (data.occurred != null)
        {
            foreach (var rec in data.occurred)
            {
                if (rec != null && !string.IsNullOrEmpty(rec.eventId))
                    occurredEvents[rec.eventId] = rec;
            }
        }

        if (data.choices != null)
        {
            foreach (var rec in data.choices)
            {
                if (rec != null && !string.IsNullOrEmpty(rec.eventId))
                    chosenChoices[rec.eventId] = rec;
            }
        }

        if (data.flags != null)
        {
            foreach (var f in data.flags)
            {
                if (!string.IsNullOrEmpty(f)) activeFlags.Add(f);
            }
        }

        if (data.flagOrder != null && data.flagOrder.Count > 0)
        {
            foreach (var f in data.flagOrder)
            {
                if (!string.IsNullOrEmpty(f) && activeFlags.Contains(f) && !activeFlagOrder.Contains(f))
                    activeFlagOrder.Add(f);
            }
        }
        // 古いセーブデータ対応
        foreach (var f in activeFlags)
        {
            if (!activeFlagOrder.Contains(f)) activeFlagOrder.Add(f);
        }

        if (data.flagSetDays != null)
        {
            foreach (var entry in data.flagSetDays)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.flagId))
                    flagSetDays[entry.flagId] = entry.day;
            }
        }

        if (data.scheduled != null)
        {
            foreach (var s in data.scheduled)
            {
                if (s != null && !string.IsNullOrEmpty(s.eventId))
                    scheduledEvents.Add(s);
            }
        }

        SyncInspectorLists();
        OnRestored?.Invoke();
    }
}

