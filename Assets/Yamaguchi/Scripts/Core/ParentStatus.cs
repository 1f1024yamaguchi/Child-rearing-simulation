using System;
using UnityEngine;

public enum JobRank { 平社員 = 0, 主任, 係長, 課長, 部長 }

public class ParentStatus : MonoBehaviour
{
    [Header("この親の設定")]
    [SerializeField] private string displayName = "母親";
    [SerializeField] private JobRank rank = JobRank.平社員;
    [SerializeField] private int baseSalary = 200000;          // 平社員の基本給
    [Range(0f, 1f)][SerializeField] private float workLifeBalance = 0.5f;
    [SerializeField] private float stress = 30;

    private float pendingWage;                // まだ受け取っていない積立給料

    // 役職ごとの倍率（平社員→部長）
    [SerializeField] private float[] rankMultipliers = { 1.0f, 1.2f, 1.45f, 1.75f, 2.1f };

    public event Action<int> OnSalaryChanged;          // 月給（役職が上がった時だけ）
    public event Action<int> OnDailyWageChanged;        // 日給（バランスを動かすたび）
    public event Action<float> OnPendingWageChanged;    // 積立給与
    public event Action<float> OnBalanceChanged;
    public event Action<int> OnStressChanged;

    public string DisplayName => displayName;
    public float WorkLifeBalance => workLifeBalance;
    public int Stress => Mathf.RoundToInt(stress);

    public const int WageDaysPerMonth = 30;   // 日給の計算基準

    /// 月給 ＝ 基本給 × 役職倍率。実力の指標なので、バランスでは変わらない。
    public int MonthlySalary
    {
        get
        {
            int i = Mathf.Clamp((int)rank, 0, rankMultipliers.Length - 1);
            return Mathf.RoundToInt(baseSalary * rankMultipliers[i]);
        }
    }

    /// 今日の働き方で決まる日給。
    public int DailyWage =>
        Mathf.RoundToInt(MonthlySalary / (float)WageDaysPerMonth * BalanceMultiplier(workLifeBalance));

    public float PendingWage => pendingWage;

    /// rules-and-stats.md の表（ライフ寄り0.8倍 〜 ワーク寄り1.5倍）
    public static float BalanceMultiplier(float balance01)
    {
        int pos = Mathf.RoundToInt(balance01 * 100f);
        if (pos <= 20) return 0.8f;
        if (pos <= 40) return 0.9f;
        if (pos <= 60) return 1.0f;
        if (pos <= 80) return 1.2f;
        return 1.5f;
    }

    public void SetBalance(float value)
    {
        workLifeBalance = Mathf.Clamp01(value);
        OnBalanceChanged?.Invoke(workLifeBalance);
        OnDailyWageChanged?.Invoke(DailyWage);     // バランスが変わると日給が変わる
    }

    public void AddStress(float delta)
    {
        stress = Mathf.Clamp(stress + delta, 0, 100);
        OnStressChanged?.Invoke(Stress);
    }

    /// 1日ぶん働いた分を積み立てる。DayTicker.OnDayPassed から呼ぶ。
    public void AccrueDailyWage()
    {
        pendingWage += DailyWage;
        OnPendingWageChanged?.Invoke(pendingWage);
    }

    /// 給料日：積み立てた分を受け取り、0 に戻す。
    public int CollectWage()
    {
        int paid = Mathf.RoundToInt(pendingWage);
        pendingWage = 0f;
        OnPendingWageChanged?.Invoke(pendingWage);
        return paid;
    }

    public void SetRank(JobRank newRank)
    {
        rank = newRank;
        OnSalaryChanged?.Invoke(MonthlySalary);
        OnDailyWageChanged?.Invoke(DailyWage);     // 月給が上がれば日給も上がる
    }
}