using System;
using UnityEngine;

/// ゲーム内で1日たつごとに通知する。日単位の処理はすべてこれを購読する。
public class DayTicker : MonoBehaviour
{
    public static DayTicker I { get; private set; }
    public event Action<int> OnDayPassed;   // 通算日数

    private int lastDay = -1;

    private void Awake() { I = this; }

    private void Start()
    {
        if (GameState.I == null) { Debug.LogError("DayTicker: GameState が見つかりません"); return; }
        lastDay = Mathf.FloorToInt(GameState.I.AgeDays);
        GameState.I.OnAgeChanged += HandleAgeChanged;
    }

    private void OnDestroy()
    {
        if (GameState.I != null) GameState.I.OnAgeChanged -= HandleAgeChanged;
    }

    /// ロード用。「今日はこの日」と教えるだけで、通知は出さない。
    public void Resync(int ageDays) { lastDay = ageDays; }

    private void HandleAgeChanged(float ageDays)
    {
        int day = Mathf.FloorToInt(ageDays);
        while (lastDay < day)     // 複数日まとめて進んでも1日ずつ処理
        {
            lastDay++;
            OnDayPassed?.Invoke(lastDay);
        }
    }
}