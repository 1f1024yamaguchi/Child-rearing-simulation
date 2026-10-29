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
    public event Action OnGameOver;                         // 移行期間用（GoalReached のときだけ発火）
    public event Action<GameEndReason> OnGameEnded;

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

    // ---- ゲーム終了（ゴール到達・家出・家計破綻）----

    /// <summary>
    /// ゲームを終了させる。ゴール到達・家出・家計破綻のいずれでもここを通す（expenses.md 5.7・依頼E4）。
    /// </summary>
    public void EndGame(GameEndReason reason)
    {
        if (IsGameOver) return;
        IsGameOver = true;
        OnGameEnded?.Invoke(reason);
        if (reason == GameEndReason.GoalReached)
        {
            OnGameOver?.Invoke(); // 移行期間として既存の OnGameOver も出す
        }
    }

    // ---- 書き込みは全部この窓口を通す ----

    public void AddMoney(int delta)
    {
        money = money + delta; // 依頼E4：マイナス所持金を許可する（0円止めを外す）
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
            EndGame(GameEndReason.GoalReached);
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
        money = newMoney; // 依頼E4：0円止めを外す
        stress = Mathf.Clamp(newStress, 0, 100);
        ageDays = Mathf.Max(0f, newAgeDays);
        workLifeBalance = Mathf.Clamp01(newBalance);
        IsGameOver = ageDays >= GoalAgeDays;

        OnMoneyChanged?.Invoke(money);
        OnStressChanged?.Invoke(stress);
        OnAgeChanged?.Invoke(ageDays);
        OnBalanceChanged?.Invoke(workLifeBalance);
    }

    /// <summary>
    /// セーブ復元用。ゲームオーバー済みかどうかも戻す。
    /// </summary>
    public void LoadValues(int newMoney, int newStress, float newAgeDays, float newBalance, bool newIsGameOver)
    {
        LoadValues(newMoney, newStress, newAgeDays, newBalance);
        IsGameOver = newIsGameOver;
    }
}

/// <summary>
/// ゲームの終了理由（expenses.md 5.7・依頼E4）。
/// </summary>
public enum GameEndReason
{
    GoalReached, // 20歳到達（ゴール）
    Runaway,     // 子どもの家出
    Bankrupt     // 家計破綻（赤字が規定月数継続）
}
