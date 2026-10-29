using UnityEngine;

/// <summary>
/// 月が変わったら自動セーブする（save-system.md 3章）。GameManager に付ける。
/// - OnMonthChanged ではフラグを立てるだけ。給料・支給・イベントなど他の購読者が全部終わった後、
///   LateUpdate で SaveSystem.Save() を1回だけ呼ぶ。
/// - ゲームが終わっていたら保存しない。ゲームが終わったとき（ゴール＝OnGameOver、家出＝RunawayWatcher.OnRunaway）はセーブを消す。
/// - 起動時の自動ロードはしない（タイトル画面の「続きから」で SaveSystem.Load() を呼ぶ）。
/// </summary>
public class AutoSaver : MonoBehaviour
{
    private bool pending;
    private bool monthSubscribed;
    private bool gameEndedSubscribed;

    /// ゲームが終わっているか（ゴール到達・家出・家計破綻）。SaveButton からも使う。
    public static bool GameEnded
    {
        get
        {
            if (GameState.I != null && GameState.I.IsGameOver) return true;
            var rw = FindObjectOfType<RunawayWatcher>();
            if (rw != null && rw.HasRunAway) return true;
            var bw = FindObjectOfType<BankruptcyWatcher>();
            return bw != null && bw.HasBankrupted;
        }
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void Start()
    {
        Subscribe();   // OnEnable の時点で相手がまだ無かったとき用
    }

    private void OnDisable()
    {
        if (monthSubscribed && MonthTicker.I != null) MonthTicker.I.OnMonthChanged -= HandleMonthChanged;
        if (gameEndedSubscribed && GameState.I != null) GameState.I.OnGameEnded -= HandleGameEnded;
        monthSubscribed = false;
        gameEndedSubscribed = false;
        pending = false;
    }

    private void Subscribe()
    {
        if (!monthSubscribed && MonthTicker.I != null)
        {
            MonthTicker.I.OnMonthChanged += HandleMonthChanged;
            monthSubscribed = true;
        }
        if (!gameEndedSubscribed && GameState.I != null)
        {
            GameState.I.OnGameEnded += HandleGameEnded;
            gameEndedSubscribed = true;
        }
    }

    private void HandleMonthChanged(int year, int month)
    {
        pending = true;   // 保存は LateUpdate まで待つ
    }

    private void HandleGameEnded(GameEndReason reason)
    {
        pending = false;
        SaveSystem.Delete();
    }

    private void LateUpdate()
    {
        if (!pending) return;
        pending = false;

        if (GameState.I == null || GameEnded) return;
        SaveSystem.Save();
    }
}
