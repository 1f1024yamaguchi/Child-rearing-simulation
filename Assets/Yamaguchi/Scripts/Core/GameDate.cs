using UnityEngine;


//生まれた日を0歳1月1日から数える。
//1秒1日で進む。
public class GameDate : MonoBehaviour
{
    public const int DaysPerYear = 365;
    private static readonly int[] MonthDays = {31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31};


    //経過日数から年齢・月・日を求める[
    
    public static void FromAgeDays(float ageDays, out int year, out int month, out int day )
    {
        int totalday = Mathf.FloorToInt(ageDays);

        if(totalday <0)
        {
            totalday = 0;
        }

        year = totalday / DaysPerYear; 

        int dayOfYear = totalday % DaysPerYear;

        month = 1;

        for (int i = 0; i < MonthDays.Length; i++)
        {
            if(dayOfYear < MonthDays[i])
            {
                month = i + 1;
                break;
            }
            dayOfYear -= MonthDays[i];
        }

        day = dayOfYear + 1;
    }

    public static string Format(float ageDays)
    {
        int year, month, day;

        FromAgeDays(ageDays, out year, out month, out day);
        return year +"歳" + month + "月" + day + "日";
    }

    /// <summary>指定した月（1〜12）の日数を取得する</summary>
    public static int DaysInMonth(int month)
    {
        if (month < 1 || month > 12) return 31;
        return MonthDays[month - 1];
    }

    // ─── 学年（ending.md 1章・antigravity-ending.md 2.3）─────────────
    // 年度 = (月 >= 4) ? 歳 : 歳 − 1。小学校 = 年度 6〜11、中学 = 12〜14、高校 = 15〜17、18〜 = 高校卒業後。

    /// <summary>年度（4月に1つ上がる）。0歳1〜3月は -1。</summary>
    public static int SchoolYear(float ageDays)
    {
        FromAgeDays(ageDays, out int y, out int m, out _);
        return m >= 4 ? y : y - 1;
    }

    /// <summary>学校の段階。</summary>
    public static SchoolStage Stage(float ageDays)
    {
        int sy = SchoolYear(ageDays);
        if (sy < 6) return SchoolStage.未就学;
        if (sy <= 11) return SchoolStage.小学校;
        if (sy <= 14) return SchoolStage.中学校;
        if (sy <= 17) return SchoolStage.高校;
        return SchoolStage.高校卒業後;
    }

    /// <summary>その学校の何年生か（小1なら1）。未就学・高校卒業後は 0。</summary>
    public static int GradeInStage(float ageDays)
    {
        int sy = SchoolYear(ageDays);
        switch (Stage(ageDays))
        {
            case SchoolStage.小学校: return sy - 5;
            case SchoolStage.中学校: return sy - 11;
            case SchoolStage.高校: return sy - 14;
            default: return 0;
        }
    }

    /// <summary>学期末の月（7・12・3月）か。</summary>
    public static bool IsTermEndMonth(int month) => month == 7 || month == 12 || month == 3;

    /// <summary>年度 schoolYear が始まる日（その歳の4月1日）。</summary>
    public static int SchoolYearStart(int schoolYear) => ToAgeDays(schoolYear, 4, 1);

    /// <summary>歳・月・日 から通算日数（ageDays）を取得する</summary>
    public static int ToAgeDays(int year, int month, int day)
    {
        int total = year * DaysPerYear;
        for (int i = 0; i < month - 1 && i < MonthDays.Length; i++)
        {
            total += MonthDays[i];
        }
        return total + (day - 1);
    }



    
}
