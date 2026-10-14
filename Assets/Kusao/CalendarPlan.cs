using System;
using UnityEngine;

/// <summary>
/// カレンダーの予定の種類（calendar-system.md 2.2）。
/// </summary>
public enum PlanKind
{
    Apply,   // 申請
    Renew,   // 手続き・更新
    Memo     // メモ
}

/// <summary>
/// 通知リマインダーのタイミング（calendar-system.md 2.2）。
/// </summary>
public enum ReminderTiming
{
    None,
    OneWeekBefore,
    TwoWeeksBefore,
    OneMonthBefore
}

/// <summary>
/// カレンダーに登録される予定データモデル（calendar-system.md 2.2）。
/// そのまま SaveData（セクション）に入れてセーブ可能。
/// </summary>
[Serializable]
public class CalendarPlan
{
    public string uid;             // 予定ごとの一意なID（Guid文字列）
    public int ageDays;            // 予定の日付（通算日数）
    public PlanKind kind;          // 申請・手続き・メモ
    public string schemeId;        // Apply / Renew のときの SupportScheme.id（Memo は空）
    public string title;           // 表示するタイトル（制度名など）
    public ReminderTiming reminder = ReminderTiming.OneWeekBefore;
    public bool pauseOnRemind = true;
    public bool reminded;

    public CalendarPlan()
    {
        uid = Guid.NewGuid().ToString("N");
    }

    public CalendarPlan(string schemeId, string title, int ageDays, PlanKind kind)
    {
        uid = Guid.NewGuid().ToString("N");
        this.schemeId = schemeId;
        this.title = title;
        this.ageDays = ageDays;
        this.kind = kind;
    }
}
