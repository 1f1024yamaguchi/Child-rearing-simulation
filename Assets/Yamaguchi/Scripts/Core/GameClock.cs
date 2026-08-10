using UnityEngine;

/// <summary>
/// 一定間隔で時間を進める時間エンジン。
/// 1ティックごとに子供の月齢を進め、ワークライフバランスに応じて
/// お金・ストレスを増減させる。
/// 増減の具体的なバランス調整（数値）はゲームロジック担当が調整する前提の仮値。
/// </summary>
public class GameClock : MonoBehaviour
{
    [Header("時間設定")]
    [SerializeField] private float secondsPerTick = 1f; // 実時間何秒でゲーム内1か月進むか
    [SerializeField] private float monthsPerTick = 1f;  // 1ティックで進む月数

    [Header("お金の増減（仮値：バランス担当が調整）")]
    [SerializeField] private int moneyWhenLife = -20000; // ライフ寄り(0)のときの1ティック増減
    [SerializeField] private int moneyWhenWork = 50000;  // ワーク寄り(1)のときの1ティック増減

    [Header("ストレスの増減（仮値：バランス担当が調整）")]
    [SerializeField] private int stressWhenLife = -3;    // ライフ寄り(0)のとき
    [SerializeField] private int stressWhenWork = 5;     // ワーク寄り(1)のとき

    private float timer;
    private bool running = true;

    public void SetRunning(bool value) => running = value;

    private void Update()
    {
        if (!running || GameState.I == null || GameState.I.IsGameOver) return;

        timer += Time.deltaTime;
        if (timer < secondsPerTick) return;

        timer -= secondsPerTick;
        Tick();
    }

    private void Tick()
    {
        var s = GameState.I;
        float b = s.WorkLifeBalance; // 0=ライフ 1=ワーク

        s.AddAge(monthsPerTick);
        s.AddMoney(Mathf.RoundToInt(Mathf.Lerp(moneyWhenLife, moneyWhenWork, b)));
        s.AddStress(Mathf.RoundToInt(Mathf.Lerp(stressWhenLife, stressWhenWork, b)));
    }
}
