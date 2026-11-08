using System;
using UnityEngine;

/// 子どものストレスが限界（91以上）のまま規定日数続いたら家出（ゲームオーバー）にする。
/// child-status.md 4章「家出（ゲームオーバー）」の仕様を実装。
/// 小学生まで（12歳3月31日まで）はルールを覚える期間として家出しない（decisions/0033）。
public class RunawayWatcher : MonoBehaviour, ISaveParticipant
{
    [Header("参照")]
    [Tooltip("監視する子どものステータス（Household.Child から自動取得）")]
    [SerializeField] private ChildStatus child;
    [Tooltip("家出時に停止させる GameClock（未設定なら自動検索）")]
    [SerializeField] private GameClock gameClock;

    [Header("家出判定")]
    [Tooltip("子ストレスがこの値以上の状態が続いたら家出カウントを進める")]
    [SerializeField] private float limitStress = 91f;
    [Tooltip("家出発生までの日数（仕様未定の仮値・たたき台）")]
    [SerializeField] private int runawayDays = 30;

    [Header("家出が起きる時期")]
    [Tooltip("この年齢（歳）の activeFromMonth 月1日から家出の判定を始める。それまではルールを覚える期間として家出しない（既定：中学入学＝12歳4月1日）")]
    [SerializeField] private int activeFromAge = 12;
    [Tooltip("判定を始める月（学年は4月始まり）")]
    [Range(1, 12)][SerializeField] private int activeFromMonth = 4;

    // ─── 内部状態 ──────────────────────────────────────────────
    private int dangerDayCount = 0;
    private bool hasRunAway = false;

    // ─── セーブ参加者 ──────────────────────────────────────────
    public string SaveKey => "runaway";
    public int RestoreOrder => 40;

    private void OnEnable() => SaveSystem.Register(this);
    private void OnDisable() => SaveSystem.Unregister(this);

    // ─── 公開イベント ──────────────────────────────────────────
    /// 家出が発生したときに通知される。
    public event Action OnRunaway;
    /// 危険日数カウントが変わったときに通知される（引数：現在の日数）。
    public event Action<int> OnDangerDaysChanged;

    // ─── 読み取り ───────────────────────────────────────────
    /// 現在の危険日数カウント。
    public int DangerDays => dangerDayCount;

    /// 家出発生までの残り日数（カウントが0のときは runawayDays をそのまま返す）。
    public int RemainingDays => Mathf.Max(0, runawayDays - dangerDayCount);

    /// 家出が発生済みかどうか。発生後は判定を行わない。
    public bool HasRunAway => hasRunAway;

    /// 家出の判定を始める日（通算日数）。既定は 12歳4月1日（中学入学）。
    public int ActiveFromDay => GameDate.ToAgeDays(activeFromAge, activeFromMonth, 1);

    /// いま家出が起きうる時期か（小学生まではルールを覚える期間なので false）。
    public bool IsActive => GameState.I != null && Mathf.FloorToInt(GameState.I.AgeDays) >= ActiveFromDay;

    // ─── セーブ復元窓口 ─────────────────────────────────────
    public void RestoreDangerDays(int days)
    {
        dangerDayCount = Mathf.Max(0, days);
        OnDangerDaysChanged?.Invoke(dangerDayCount);
    }

    public void RestoreHasRunAway(bool value)
    {
        hasRunAway = value;
    }

    private void Start()
    {
        if (child == null && Household.I != null)
            child = Household.I.Child;

        if (gameClock == null)
            gameClock = FindObjectOfType<GameClock>();

        if (DayTicker.I == null)
        {
            Debug.LogError("RunawayWatcher: DayTicker が見つかりません");
            return;
        }
        DayTicker.I.OnDayPassed += CheckOneDay;
    }

    private void OnDestroy()
    {
        if (DayTicker.I != null) DayTicker.I.OnDayPassed -= CheckOneDay;
    }

    private void CheckOneDay(int day)
    {
        // 家出済みなら以降は判定しない
        if (hasRunAway) return;

        if (child == null)
        {
            if (Household.I != null) child = Household.I.Child;
            if (child == null) return;
        }

        // 小学生まではルールを覚える期間。家出のカウントを進めない（警告の行も出ない）
        if (day < ActiveFromDay)
        {
            if (dangerDayCount > 0)
            {
                dangerDayCount = 0;
                OnDangerDaysChanged?.Invoke(dangerDayCount);
            }
            return;
        }

        if (child.Stress >= limitStress)
        {
            // 限界ストレスが続いているのでカウントを増やす
            dangerDayCount++;
            OnDangerDaysChanged?.Invoke(dangerDayCount);

            if (dangerDayCount >= runawayDays)
            {
                // 家出発生（一度きり） → フラグを立てて時間を止める
                hasRunAway = true;
                Debug.LogWarning($"RunawayWatcher: 子どもが家出しました（{dangerDayCount}日連続で限界ストレス）");
                gameClock?.SetRunning(false);
                GameState.I?.EndGame(GameEndReason.Runaway);
                OnRunaway?.Invoke();
                // エンディング画面やゲームオーバー演出は未実装のため、ここでは止めるだけ
            }
        }
        else
        {
            // 限界を下回ったらカウントをリセット
            if (dangerDayCount > 0)
            {
                dangerDayCount = 0;
                OnDangerDaysChanged?.Invoke(dangerDayCount);
            }
        }
    }

    // ─── セーブ・ロード（ISaveParticipant）───────────────────
    [Serializable]
    private class RunawaySaveData
    {
        public int dangerDays;
        public bool hasRunAway;
    }

    public string Capture()
    {
        var data = new RunawaySaveData
        {
            dangerDays = dangerDayCount,
            hasRunAway = hasRunAway
        };
        return JsonUtility.ToJson(data);
    }

    public void Restore(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        var data = JsonUtility.FromJson<RunawaySaveData>(json);
        if (data == null) return;
        RestoreDangerDays(data.dangerDays);
        RestoreHasRunAway(data.hasRunAway);
    }
}
