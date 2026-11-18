using System;

public enum ExamKind { 高校受験, 推薦, 一般 }

/// <summary>受験1回の記録（ending.md 5.1）。リザルトとエピローグにだけ使う（職業判定には使わない）。</summary>
[Serializable]
public class ExamRecord
{
    public ExamKind kind;
    public int schoolYear;
    public string pathId = "";
    public bool wasWished;     // 本人の希望の進路だったか
    public string mockRank = "";
    public bool passed;
}
