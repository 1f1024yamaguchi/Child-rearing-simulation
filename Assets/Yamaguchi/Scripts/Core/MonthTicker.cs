using System;
using UnityEngine;

public class MonthTicker : MonoBehaviour
{
    public static MonthTicker I { get; private set; }
    public event Action<int, int> OnMonthChanged;   // (歳, 月)

    private int lastYear = -1, lastMonth = -1;

    private void Awake() { I = this; }

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

    private void HandleAgeChanged(float ageDays)
    {
        int y, m, d;
        GameDate.FromAgeDays(ageDays, out y, out m, out d);
        if (y == lastYear && m == lastMonth) return;   // 同じ月なら何もしない
        lastYear = y; lastMonth = m;
        OnMonthChanged?.Invoke(y, m);
    }
}