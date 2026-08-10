using System;
using UnityEngine;

/// <summary>
/// ゲームの全パラメータを保持する中心クラス。
/// 書き込みは必ずメソッド経由（値は自動でクランプ）、
/// 変更時はイベントで通知するので、UI等は監視するだけで更新できる。
/// どこからでも GameState.I で参照可能。
/// 年齢は「日数」で保持する（GameClockが1日ずつ進める）。
/// </summary>
public class GameState : MonoBehaviour
{
    public static GameState I { get; private set; }

    [Header("パラメータ初期値")]
    [SerializeField] private int money = 300000;      // お金（円）
    [SerializeField] private int stress = 30;         // ストレス 0〜100
    [SerializeField] private float ageDays = 0f;      // 子供の年齢（日数・スライダー用）
    [Range(0f, 1f)]
    [SerializeField] private float workLifeBalance = 0.5f; // 0=ライフ寄り 1=ワーク寄り

    [Header("設定")]
    [SerializeField] private int goalAgeYears = 20;   // ゴール年齢（歳）
    [SerializeField] private int daysPerYear = 365;   // 1年の日数

    // ---- 変更通知イベント（UI・SEなどがこれを購読する）----
    public event Action<int> OnMoneyChanged;
    public event Action<int> OnStressChanged;
    public event Action<float> OnAgeChanged;
    public event Action<float> OnBalanceChanged;
    public event Action OnGameOver;

    // ---- 読み取り専用プロパティ ----
    public int Money => money;
    public int Stress => stress;
    public float AgeDays => ageDays;
    public float AgeYears => ageDays / daysPerYear;   // 歳での表示用
    public float WorkLifeBalance => workLifeBalance;
    public int GoalAgeDays => goalAgeYears * daysPerYear;
    public bool IsGameOver { get; private set; }

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    // ---- 書き込みは全部この窓口を通す ----

    public void AddMoney(int delta)
    {
        money = Mathf.Max(0, money + delta);
        OnMoneyChanged?.Invoke(money);
    }

    public void AddStress(int delta)
    {
        stress = Mathf.Clamp(stress + delta, 0, 100);
        OnStressChanged?.Invoke(stress);
    }

    public void AddAgeDays(float days)
    {
        ageDays += days;
        OnAgeChanged?.Invoke(ageDays);

        if (!IsGameOver && ageDays >= GoalAgeDays)
        {
            IsGameOver = true;
            OnGameOver?.Invoke();
        }
    }

    public void SetBalance(float value)
    {
        workLifeBalance = Mathf.Clamp01(value);
        OnBalanceChanged?.Invoke(workLifeBalance);
    }

    /// <summary>
    /// セーブ復元時などにまとめて値を差し込む。差し込み後に通知を飛ばしてUIを合わせる。
    /// </summary>
    public void LoadValues(int newMoney, int newStress, float newAgeDays, float newBalance)
    {
        money = Mathf.Max(0, newMoney);
        stress = Mathf.Clamp(newStress, 0, 100);
        ageDays = Mathf.Max(0f, newAgeDays);
        workLifeBalance = Mathf.Clamp01(newBalance);
        IsGameOver = ageDays >= GoalAgeDays;

        OnMoneyChanged?.Invoke(money);
        OnStressChanged?.Invoke(stress);
        OnAgeChanged?.Invoke(ageDays);
        OnBalanceChanged?.Invoke(workLifeBalance);
    }
}
