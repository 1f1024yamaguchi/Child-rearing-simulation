using System;
using UnityEngine;

public class MonthTicker : MonoBehaviour
{
    public static MonthTicker I { get; private set; }

    /// <summary>月が変わったとき（毎月1日の月初）に出す。食事選択・イベント抽選など。</summary>
    public event Action<int, int> OnMonthChanged;   // (歳, 月)

    /// <summary>その月の最終日（月末）の終わりに締め処理として出す。給与・固定費・支援金など。</summary>
    public event Action<int, int> OnMonthEnding;    // (歳, 月)

    /// <summary>OnMonthEnding の処理が全部終わった直後に同じ (歳, 月) で出す。赤字判定・家計簿の締め・年末調整はこちら。Resync では出さない。</summary>
    public event Action<int, int> OnMonthSettled;   // (歳, 月)

    private int lastYear = -1, lastMonth = -1;
    private int lastSettledYear = -1, lastSettledMonth = -1;

    private void Awake() { I = this; }

    private void Start()
    {
        if (DayTicker.I != null)
        {
            DayTicker.I.OnDayCompleted += HandleDayCompleted;
        }
    }

    private void OnDestroy()
    {
        if (DayTicker.I != null)
        {
            DayTicker.I.OnDayCompleted -= HandleDayCompleted;
        }
        if (I == this) I = null;
    }

    private void OnEnable()
    {
        if (GameState.I == null) return;
        GameState.I.OnAgeChanged += HandleAgeChanged;
        int d;
        GameDate.FromAgeDays(GameState.I.AgeDays, out lastYear, out lastMonth, out d);
    }

    private void OnDisable()
    {
        if (GameState.I != null) GameState.I.OnAgeChanged -= HandleAgeChanged;
    }

    /// ロード用。今の年・月を合わせるだけで、通知は出さない。
    public void Resync(float ageDays)
    {
        int d;
        GameDate.FromAgeDays(ageDays, out lastYear, out lastMonth, out d);
        int daysInMonth = GameDate.DaysInMonth(lastMonth);
        if (d >= daysInMonth)
        {
            lastSettledYear = lastYear;
            lastSettledMonth = lastMonth;
        }
    }

    private void HandleDayCompleted(int day)
    {
        GameDate.FromAgeDays(day, out int y, out int m, out int d);
        int daysInMonth = GameDate.DaysInMonth(m);
        if (d == daysInMonth)
        {
            if (y == lastSettledYear && m == lastSettledMonth) return;
            lastSettledYear = y;
            lastSettledMonth = m;
            OnMonthEnding?.Invoke(y, m);
            OnMonthSettled?.Invoke(y, m);
        }
    }

    private void HandleAgeChanged(float ageDays)
    {
        int y, m, d;
        GameDate.FromAgeDays(ageDays, out y, out m, out d);
        if (y == lastYear && m == lastMonth) return;   // 同じ月なら何もしない
        lastYear = y; lastMonth = m;
        OnMonthChanged?.Invoke(y, m);
    }
}