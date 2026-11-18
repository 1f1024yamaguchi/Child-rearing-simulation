using System;
using System.Collections.Generic;

/// <summary>進路の状態（ending.md 8章）。PathSystem が持ってセーブする。</summary>
[Serializable]
public class ChildPath
{
    public string targetPathId = "";     // 高3の7月に決めた進路（合格前）
    public string currentPathId = "";    // 入学した進路（合格後。就職は決めた時点で入る）
    public bool enrolled;                // 専門・大学に在学中か
    public int enrolledSchoolYear = -1;  // 入学した年度
    public int yearInSchool;             // 今何年生か
    public int roninCount;
    public int repeatCount;              // 留年の回数
    public string wishPathId = "";       // 子どもの希望（決めたときの）
    public bool decidedByParent;         // 希望と違う進路を親が選んだか
    public bool isPrivateHighSchool;
    public bool highSchoolExamDone;
    public bool recommendationDone;
    public string pendingDialog = "";    // 選んでもらう途中の画面（decide / retry）。ロードしたら出し直す
    public List<ExamRecord> exams = new List<ExamRecord>();
}

/// <summary>一般入試に落ちたときの選び直し（ending.md 3.5）。</summary>
public enum RetryChoice { 浪人, 就職, 専門 }
