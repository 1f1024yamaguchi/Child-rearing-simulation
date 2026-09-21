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



    
}
