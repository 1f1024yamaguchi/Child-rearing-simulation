using System;

/// <summary>学期末テスト1回の記録（ending.md 2.5章）。</summary>
[Serializable]
public class TermTestRecord
{
    public int schoolYear;      // 年度
    public int month;           // 7 / 12 / 3
    public SchoolStage stage;
    public int grade;           // 画面に出ていた成績（丸めた整数）
    public bool passed;
}
