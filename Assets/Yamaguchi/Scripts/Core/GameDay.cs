using UnityEngine;

/// <summary>
/// 歳・月・日を Inspector で入力するための入れ物（support-systems.md 3.5）。
/// 日付はゲーム内の暦（GameDate：0歳1月1日起点）。
/// </summary>
[System.Serializable]
public struct GameDay
{
    [Min(0)]       public int year;    // 歳（0〜19）
    [Range(1, 12)] public int month;
    [Range(1, 31)] public int day;

    public GameDay(int year, int month, int day)
    {
        this.year = year;
        this.month = month;
        this.day = day;
    }

    /// <summary>通算日数（GameState.AgeDays と同じ数え方）に変換する。</summary>
    public int ToAgeDays() => GameDate.ToAgeDays(year, month, day);
}
