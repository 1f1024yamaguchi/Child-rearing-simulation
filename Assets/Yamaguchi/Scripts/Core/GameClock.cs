using UnityEngine;

/// <summary>
/// 一定間隔で時間を進める時間エンジン。
/// 「実時間何秒でゲーム内1日進むか」を基準に、1日ごとに子供の年齢（日数）を進め、
/// ワークライフバランスに応じてお金・ストレスを1日分だけ増減させる。
/// 増減の具体的なバランス調整（数値）はゲームロジック担当が調整する前提の仮値。
/// </summary>
public class GameClock : MonoBehaviour
{
    [Header("時間設定")]
    [Tooltip("実時間で何秒たつとゲーム内で1日進むか")]
    [SerializeField] private float secondsPerDay = 1f;

    [Header("お金の1日あたり増減（仮値：バランス担当が調整）")]
    [SerializeField] private int moneyPerDayWhenLife = -700;  // ライフ寄り(0)のとき1日あたり
    [SerializeField] private int moneyPerDayWhenWork = 1700;  // ワーク寄り(1)のとき1日あたり

    [Header("ストレスの1日あたり増減（仮値：バランス担当が調整）")]
    [SerializeField] private int stressPerDayWhenLife = -3;   // ライフ寄り(0)のとき
    [SerializeField] private int stressPerDayWhenWork = 5;    // ワーク寄り(1)のとき

    private float timer;
    private bool running = true;

    public void SetRunning(bool value) => running = value;

    private void Update()
    {
        if (!running || GameState.I == null || GameState.I.IsGameOver) return;

        timer += Time.deltaTime;
        // 1フレームで複数日ぶんたまっても取りこぼさないよう while で消化
        while (timer >= secondsPerDay)
        {
            timer -= secondsPerDay;
            Tick();
        }
    }

    // 1日ぶん進める
    private void Tick()
    {
        var s = GameState.I;
        float b = s.WorkLifeBalance; // 0=ライフ 1=ワーク

        s.AddAgeDays(1f);
        s.AddMoney(Mathf.RoundToInt(Mathf.Lerp(moneyPerDayWhenLife, moneyPerDayWhenWork, b)));
        s.AddStress(Mathf.RoundToInt(Mathf.Lerp(stressPerDayWhenLife, stressPerDayWhenWork, b)));
    }
}
