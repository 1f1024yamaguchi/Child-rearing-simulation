using System;
using System.Collections.Generic;

/// <summary>
/// 学期末テストの記録の入れ物（ending.md 2.5章・5.1）。評定と不合格回数はここから計算する。
/// 画面・セーブ・時間には触らない（TermTestSystem が持ってセーブする）。
/// </summary>
[Serializable]
public class AcademicRecord
{
    /// <summary>評定の境目（たたき台）：0–19=1／20–39=2／40–59=3／60–79=4／80–100=5。値は各評定の下限。</summary>
    public static int[] HyoteiLowerBounds = { 0, 20, 40, 60, 80 };

    public List<TermTestRecord> records = new List<TermTestRecord>();

    /// <summary>中学・高校の不合格の数（在学中のテストは含めない）。職業判定の条件に使う。</summary>
    public int FailCount
    {
        get
        {
            int n = 0;
            foreach (var r in records)
                if (r != null && !r.passed && (r.stage == SchoolStage.中学校 || r.stage == SchoolStage.高校)) n++;
            return n;
        }
    }

    /// <summary>高校のテストの平均（9回。まだ無ければ今ある分。0回なら -1）。</summary>
    public float HighSchoolAverage
    {
        get
        {
            int n = 0; float sum = 0;
            foreach (var r in records)
                if (r != null && r.stage == SchoolStage.高校) { n++; sum += r.grade; }
            return n == 0 ? -1f : sum / n;
        }
    }

    /// <summary>評定（1〜5）。高校のテストが無ければ 0。</summary>
    public int Hyotei
    {
        get
        {
            float avg = HighSchoolAverage;
            if (avg < 0) return 0;
            int h = 1;
            for (int i = 0; i < HyoteiLowerBounds.Length; i++)
                if (avg >= HyoteiLowerBounds[i]) h = i + 1;
            return h;
        }
    }

    /// <summary>その年度のテストの平均（在学中の留年判定用）。無ければ -1。</summary>
    public float AverageOfYear(int schoolYear, SchoolStage stage = SchoolStage.高校卒業後)
    {
        int n = 0; float sum = 0;
        foreach (var r in records)
            if (r != null && r.schoolYear == schoolYear && r.stage == stage) { n++; sum += r.grade; }
        return n == 0 ? -1f : sum / n;
    }
}
