using System;
using UnityEngine;

/// <summary>
/// 赤字の月数を監視し、規定月数（既定2か月）連続で赤字が続いたら家計破綻（ゲームオーバー）にする（expenses.md 5.7・依頼E4）。
/// GameManager に付ける。
/// </summary>
public class BankruptcyWatcher : MonoBehaviour, ISaveParticipant
{
    [Header("破綻判定")]
    [Tooltip("赤字が何ヶ月続いたら家計破綻にするか（既定2）")]
    [SerializeField] private int maxDeficitMonths = 2;

    [Tooltip("破綻時に停止させる GameClock（未設定なら自動検索）")]
    [SerializeField] private GameClock gameClock;

    [Tooltip("赤字の月に開く警告（未設定なら自動検索。非表示のまま置いてあってよい）")]
    [SerializeField] private DeficitWarningUI warningUI;

    // ─── 内部状態 ──────────────────────────────────────────────
    private int deficitMonthCount = 0;
    private bool hasBankrupted = false;
    private bool subscribed = false;

    // ─── セーブ参加者 ──────────────────────────────────────────
    public string SaveKey => "bankrupt";
    public int RestoreOrder => 45;

    private void OnEnable() => SaveSystem.Register(this);
    private void OnDisable()
    {
        SaveSystem.Unregister(this);
        Unsubscribe();
    }

    // ─── 公開イベント ──────────────────────────────────────────
    /// <summary>家計破綻が発生したときに通知される。</summary>
    public event Action OnBankrupt;

    /// <summary>赤字月数カウントが変わったときに通知される（引数：現在の赤字月数）。</summary>
    public event Action<int> OnDeficitMonthsChanged;

    // ─── 読み取り ───────────────────────────────────────────
    public int DeficitMonths => deficitMonthCount;
    public int MaxDeficitMonths => maxDeficitMonths;
    public bool HasBankrupted => hasBankrupted;

    private void Start()
    {
        if (gameClock == null)
            gameClock = FindFirstObjectByType<GameClock>();
        if (warningUI == null)
            warningUI = FindFirstObjectByType<DeficitWarningUI>(FindObjectsInactive.Include);

        if (!subscribed)
        {
            if (MonthTicker.I == null)
            {
                Debug.LogError("BankruptcyWatcher: MonthTicker が見つかりません");
                return;
            }
            // 依頼E4：OnEnable ではなく Start で購読する（MonthTicker.Awake 待ち）
            MonthTicker.I.OnMonthSettled += HandleMonthSettled;
            subscribed = true;
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (subscribed && MonthTicker.I != null)
        {
            MonthTicker.I.OnMonthSettled -= HandleMonthSettled;
        }
        subscribed = false;
    }

    private void HandleMonthSettled(int year, int month)
    {
        if (hasBankrupted) return;

        int money = GameState.I != null ? GameState.I.Money : 0;

        if (money < 0)
        {
            deficitMonthCount++;
            Debug.LogWarning($"[BankruptcyWatcher] 今月は赤字でした（残高: {money:N0}円 / 連続赤字: {deficitMonthCount}ヶ月目）");
            OnDeficitMonthsChanged?.Invoke(deficitMonthCount);

            if (deficitMonthCount >= maxDeficitMonths)
            {
                hasBankrupted = true;
                Debug.LogError($"[BankruptcyWatcher] 家計破綻しました（{deficitMonthCount}か月連続赤字）");
                gameClock?.SetRunning(false);
                GameState.I?.EndGame(GameEndReason.Bankrupt);
                OnBankrupt?.Invoke();
            }
            else
            {
                // 依頼E4-2：警告は非表示で置いてあり、自分で購読できない（Start が呼ばれない）ので、ここから開く
                if (warningUI != null) warningUI.Open();
                else Debug.LogWarning("[BankruptcyWatcher] DeficitWarningUI が見つからないので、赤字の警告を出せません");
            }
        }
        else
        {
            // プラスに戻ったらカウントをリセット
            if (deficitMonthCount > 0)
            {
                deficitMonthCount = 0;
                Debug.Log($"[BankruptcyWatcher] 残高がプラスに戻りました（残高: {money:N0}円 / 赤字カウントリセット）");
                OnDeficitMonthsChanged?.Invoke(0);
            }
        }
    }

    // ─── セーブ・ロード（ISaveParticipant）───────────────────
    [Serializable]
    private class BankruptcySaveData
    {
        public int deficitMonths;
        public bool hasBankrupted;
    }

    public string Capture()
    {
        var data = new BankruptcySaveData
        {
            deficitMonths = deficitMonthCount,
            hasBankrupted = hasBankrupted
        };
        return JsonUtility.ToJson(data);
    }

    public void Restore(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        var data = JsonUtility.FromJson<BankruptcySaveData>(json);
        if (data == null) return;
        deficitMonthCount = Mathf.Max(0, data.deficitMonths);
        hasBankrupted = data.hasBankrupted;
        OnDeficitMonthsChanged?.Invoke(deficitMonthCount);
    }
}
