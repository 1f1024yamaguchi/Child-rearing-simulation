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



    public static GameClock Instance { get; private set; }
    public static GameClock I => Instance;

    private float timer;
    private bool running = true;
    private int pauseRequests = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void SetRunning(bool value) => running = value;

    /// <summary>
    /// 判断画面等を開いたときに一時停止を要求する。
    /// カウント管理を行うため、複数画面が開いた場合でもすべての画面が閉じるまで再開しない。
    /// </summary>
    public void PushPause()
    {
        pauseRequests++;
        if (pauseRequests == 1) SetRunning(false);
    }

    /// <summary>
    /// 判断画面等を閉じ閉じたときに一時停止要求を解除する。
    /// カウントが0になったら時間の進行を再開する。
    /// </summary>
    public void PopPause()
    {
        pauseRequests = Mathf.Max(0, pauseRequests - 1);
        if (pauseRequests == 0) SetRunning(true);
    }

    private void Update()
    {
        if (!running) return;   // SetRunning(false) で止まるようにする（判断画面・ゲームオーバー用）

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
        
        
    }
}
