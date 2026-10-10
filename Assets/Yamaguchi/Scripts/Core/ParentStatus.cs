using System;
using UnityEngine;

public enum JobRank { 平社員 = 0, 主任, 係長, 課長, 部長 }

/// 親（母親・父親）固有のステータス。
/// 表示名・ストレスの共通部分は PersonStatus に持ち、
/// 役職・月給・ワークライフバランス関連はここで管理する。
public class ParentStatus : PersonStatus
{
    [Header("役職・給料")]
    [SerializeField] private JobRank rank = JobRank.平社員;
    [SerializeField] private int baseSalary = 200000;          // 平社員の基本給

    [Header("ワークライフバランス")]
    [Range(0f, 1f)][SerializeField] private float workLifeBalance = 0.5f;

    private float pendingWage;                // まだ受け取っていない積立給料

    // 役職ごとの倍率（平社員→部長）
    [SerializeField] private float[] rankMultipliers = { 1.0f, 1.2f, 1.45f, 1.75f, 2.1f };

    public event Action<int> OnSalaryChanged;          // 月給（役職が上がった時だけ）
    public event Action<int> OnDailyWageChanged;        // 日給（バランスを動かすたび）
    public event Action<float> OnPendingWageChanged;    // 積立給与
    public event Action<float> OnBalanceChanged;

    public float WorkLifeBalance => workLifeBalance;
    public JobRank Rank => rank;

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

    /// 今日の働き方で決まる日給。Money 補正（特性・状態）を最後に掛ける。
    /// traits.md 3章：Money は DailyWage の計算の最後に適用。
    public int DailyWage
    {
        get
        {
            float mult = BalanceMultiplier(workLifeBalance);
            if (mult <= 0f) return 0;   // 休み：特性・状態の補正も含めて給料なし
            float base_ = MonthlySalary / (float)WageDaysPerMonth * mult;
            var (addSum, mulSum) = CollectModifier(EffectKind.Money);
            return Mathf.RoundToInt((base_ + addSum) * (1f + mulSum));
        }
    }

    public float PendingWage => pendingWage;

    /// rules-and-stats.md の表（ライフ寄り0.8倍 〜 ワーク寄り1.5倍）
    /// 一番ライフ寄り（0）は「休み」で給料なし（company-support.md 6章・2026-10-09 決定）
    public static float BalanceMultiplier(float balance01)
    {
        int pos = Mathf.RoundToInt(balance01 * 100f);
        if (pos <= 0) return 0f;      // 休み：給料なし
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

    /// <summary>セーブ復元用。積み立て中の給料を戻す。</summary>
    public void RestorePendingWage(float wage)
    {
        pendingWage = Mathf.Max(0f, wage);
        OnPendingWageChanged?.Invoke(pendingWage);
    }

    public void SetRank(JobRank newRank)
    {
        rank = newRank;
        OnSalaryChanged?.Invoke(MonthlySalary);
        OnDailyWageChanged?.Invoke(DailyWage);     // 月給が上がれば日給も上がる
    }
}