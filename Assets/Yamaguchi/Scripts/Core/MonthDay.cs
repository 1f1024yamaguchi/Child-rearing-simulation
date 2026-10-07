using UnityEngine;

/// <summary>「毎年 ○月○日」用（support-systems.md 3.5）。</summary>
[System.Serializable]
public struct MonthDay
{
    [Range(1, 12)] public int month;
    [Range(1, 31)] public int day;
}
