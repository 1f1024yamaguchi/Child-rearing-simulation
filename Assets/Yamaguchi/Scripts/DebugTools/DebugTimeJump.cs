using UnityEngine;

/// <summary>
/// デバッグ用。指定した日（例：10歳1月1日）まで、ゲームを早送りで進める。
/// 日付を直接書き換えるのではなく、毎日の処理（給料・食費・成長・イベント・税金など）を1日ずつ全部通して進めるので、
/// 着いたときのステータスは「普通に遊んでそこまで来た」状態になる。
/// ジャンプ中は判断画面を DebugAutoPilot が自動で処理する（設定で切り替え可）。
/// </summary>
[DefaultExecutionOrder(50)]
public class DebugTimeJump : MonoBehaviour
{
    public static DebugTimeJump I { get; private set; }

    [Tooltip("1フレームで進める最大日数。大きいほど速いが、画面の更新が重くなる")]
    [Range(1, 60)]
    [SerializeField] private int daysPerFrame = 15;
    [Tooltip("ジャンプ中は判断画面をオートで処理する。OFF にすると判断画面のたびに止まって待つ")]
    [SerializeField] private bool autoDuringJump = true;

    public int DaysPerFrame { get => daysPerFrame; set => daysPerFrame = Mathf.Clamp(value, 1, 60); }
    public bool AutoDuringJump { get => autoDuringJump; set => autoDuringJump = value; }

    public bool IsJumping { get; private set; }
    public int StartAgeDays { get; private set; }
    public int TargetAgeDays { get; private set; }
    public string LastResult { get; private set; } = "";

    /// 0〜1 の進み具合。
    public float Progress
    {
        get
        {
            if (!IsJumping || GameState.I == null || TargetAgeDays <= StartAgeDays) return IsJumping ? 0f : 1f;
            return Mathf.Clamp01((GameState.I.AgeDays - StartAgeDays) / (TargetAgeDays - StartAgeDays));
        }
    }

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(this);
            return;
        }
        I = this;
    }

    private void OnDestroy()
    {
        if (I == this)
        {
            SetForced(false);
            I = null;
        }
    }

    // ------------------------------------------------------------
    // 開始・停止
    // ------------------------------------------------------------

    /// 歳・月・日（ゲーム内の暦。0歳1月1日が誕生日）まで進める。
    public bool JumpTo(int year, int month = 1, int day = 1)
    {
        month = Mathf.Clamp(month, 1, 12);
        day = Mathf.Clamp(day, 1, GameDate.DaysInMonth(month));
        return JumpToAgeDays(GameDate.ToAgeDays(year, month, day));
    }

    /// 通算日数まで進める。いまより前には戻れない（戻りたいときはセーブをロードする）。
    public bool JumpToAgeDays(int target)
    {
        var gs = GameState.I;
        if (gs == null || GameClock.I == null)
        {
            Debug.LogWarning("[DebugTimeJump] GameState / GameClock が見つかりません。本編シーンを再生してください。");
            return false;
        }
        if (gs.IsGameOver)
        {
            Debug.LogWarning("[DebugTimeJump] ゲームはもう終わっています。");
            return false;
        }

        int now = Mathf.FloorToInt(gs.AgeDays);
        target = Mathf.Min(target, gs.GoalAgeDays);
        if (target <= now)
        {
            Debug.LogWarning($"[DebugTimeJump] {GameDate.Format(target)} はいま（{GameDate.Format(now)}）より前です。戻ることはできません。");
            return false;
        }

        StartAgeDays = now;
        TargetAgeDays = target;
        IsJumping = true;
        LastResult = "";
        SetForced(autoDuringJump);
        Debug.Log($"[DebugTimeJump] {GameDate.Format(now)} → {GameDate.Format(target)} へジャンプ開始（{target - now}日）");
        return true;
    }

    public void Stop() => Finish("途中で止めました");

    private void Finish(string result)
    {
        if (!IsJumping) return;
        IsJumping = false;
        SetForced(false);
        string at = GameState.I != null ? GameDate.Format(GameState.I.AgeDays) : "?";
        LastResult = $"{result}（{at}）";
        Debug.Log($"[DebugTimeJump] {LastResult}");
    }

    private void SetForced(bool on)
    {
        if (on) DebugTools.Ensure();
        if (DebugAutoPilot.I != null) DebugAutoPilot.I.ForcedByJump = on;
    }

    // ------------------------------------------------------------
    // 毎フレーム
    // ------------------------------------------------------------

    private void Update()
    {
        if (!IsJumping) return;

        var gs = GameState.I;
        var clock = GameClock.I;
        if (gs == null || clock == null) { Finish("本編シーンを離れたので止めました"); return; }
        if (gs.IsGameOver) { Finish("ゲームが終了したので止めました（家出・家計破綻・ゴール）"); return; }
        if (gs.AgeDays >= TargetAgeDays) { Finish("到着しました"); return; }

        // このフレームで判断画面を閉じたばかりなら、次の画面（1フレーム遅れて開くもの）を待つ
        var pilot = DebugAutoPilot.I;
        if (pilot != null && pilot.LastResolvedFrame == Time.frameCount) return;

        for (int i = 0; i < daysPerFrame; i++)
        {
            // 判断画面が開いた（PushPause された）ら、閉じられるまで進めない
            if (!clock.IsRunning || gs.IsGameOver || gs.AgeDays >= TargetAgeDays) break;

            gs.AddAgeDays(1f);   // GameClock の1日と同じ進め方。DayTicker / MonthTicker がここから全部動く

            // 月の変わり目：食事の選択画面などは1フレーム遅れて開くので、ここでフレームを区切る
            int y, m, d;
            GameDate.FromAgeDays(gs.AgeDays, out y, out m, out d);
            if (d == 1) break;
        }

        if (gs.IsGameOver) Finish("ゲームが終了したので止めました（家出・家計破綻・ゴール）");
        else if (gs.AgeDays >= TargetAgeDays) Finish("到着しました");
    }
}
