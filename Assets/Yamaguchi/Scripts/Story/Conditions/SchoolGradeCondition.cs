using Maruyama.Events;
using UnityEngine;

/// <summary>学年の区分（その年度の4月1日時点の満年齢に基づく）。</summary>
public enum SchoolGrade
{
    未就園 = 0,   // 0〜2歳
    年少 = 1,     // 3歳
    年中 = 2,     // 4歳
    年長 = 3,     // 5歳
    小1 = 4,      // 6歳
    小2 = 5,      // 7歳
    小3 = 6,      // 8歳
    小4 = 7,      // 9歳
    小5 = 8,      // 10歳
    小6 = 9,      // 11歳
    中1 = 10,     // 12歳
    中2 = 11,     // 13歳
    中3 = 12,     // 14歳
    高1 = 13,     // 15歳
    高2 = 14,     // 16歳
    高3 = 15,     // 17歳
    大学など = 16 // 18歳以上
}

/// <summary>
/// 子どもの学年が指定した範囲内のときだけ発生を許す条件。
/// 学年は「その年度の4月1日時点の満年齢」から求める（ending.md 1章に準拠）。
/// </summary>
public class SchoolGradeCondition : EventConditionSO, IDescribable
{
    [Tooltip("この学年以上で発生する")]
    public SchoolGrade minGrade = SchoolGrade.未就園;

    [Tooltip("この学年以下で発生する")]
    public SchoolGrade maxGrade = SchoolGrade.高3;

    public override bool IsSatisfied()
    {
        if (GameState.I == null) return true;

        GameDate.FromAgeDays(GameState.I.AgeDays, out int year, out int month, out _);
        SchoolGrade current = CalculateGrade(year, month);

        int lo = Mathf.Min((int)minGrade, (int)maxGrade);
        int hi = Mathf.Max((int)minGrade, (int)maxGrade);
        return (int)current >= lo && (int)current <= hi;
    }

    /// <summary>ゲーム内の満年齢と月から、その年度（4月1日時点）の学年を計算する。</summary>
    public static SchoolGrade CalculateGrade(int year, int month)
    {
        // 4月1日時点の満年齢（1〜3月なら誕生日前年度の4月1日なので year - 1）
        int baseAge = (month >= 4) ? year : year - 1;

        if (baseAge < 3) return SchoolGrade.未就園;
        switch (baseAge)
        {
            case 3: return SchoolGrade.年少;
            case 4: return SchoolGrade.年中;
            case 5: return SchoolGrade.年長;
            case 6: return SchoolGrade.小1;
            case 7: return SchoolGrade.小2;
            case 8: return SchoolGrade.小3;
            case 9: return SchoolGrade.小4;
            case 10: return SchoolGrade.小5;
            case 11: return SchoolGrade.小6;
            case 12: return SchoolGrade.中1;
            case 13: return SchoolGrade.中2;
            case 14: return SchoolGrade.中3;
            case 15: return SchoolGrade.高1;
            case 16: return SchoolGrade.高2;
            case 17: return SchoolGrade.高3;
            default: return SchoolGrade.大学など;
        }
    }

    public string Describe()
    {
        if (minGrade == maxGrade) return minGrade.ToString();
        return $"{minGrade}〜{maxGrade}";
    }
}

