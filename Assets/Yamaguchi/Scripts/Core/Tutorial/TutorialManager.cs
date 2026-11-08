using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// チュートリアルを出す窓口。
/// - ShowOnce：まだ見せていなければ出す（同じチュートリアルは二度出さない）。「見た」はセーブに入る。
/// - Show：見たかどうかに関係なく出す（設定画面から見返す、などの用途）。
/// 他の判断画面が開いている間（GameClock の一時停止の要求がある間）は順番待ちにし、全部閉じてから出す。
/// ゲームが終わっている（家出・破綻・ゴール）ときは出さない。
/// TutorialCanvas のルート（常に表示されているオブジェクト）に付ける。
/// </summary>
public class TutorialManager : MonoBehaviour, ISaveParticipant
{
    public static TutorialManager I { get; private set; }

    [Tooltip("チュートリアル画面（未設定なら子から自動で探す）")]
    [SerializeField] private TutorialPanelUI panel;

    [Tooltip("判断画面が全部閉じてから、何フレーム待って出すか。月初めは食事の選択画面が1フレーム遅れて開くので、重ならないよう少し待つ")]
    [SerializeField] private int waitFrames = 3;

    // もう見せたチュートリアルの ID
    private readonly HashSet<string> seen = new HashSet<string>();
    // 出す順番待ち（重複は queuedIds で防ぐ）
    private readonly Queue<TutorialData> queue = new Queue<TutorialData>();
    private readonly HashSet<string> queuedIds = new HashSet<string>();
    // 判断画面が閉じてから数えたフレーム数
    private int clearFrames;

    /// チュートリアルを開いた（引数は開いたもの）。
    public event Action<TutorialData> OnTutorialOpened;
    /// チュートリアルを閉じた。
    public event Action<TutorialData> OnTutorialClosed;

    // ─── セーブ参加者 ──────────────────────────────────────
    public string SaveKey => "tutorial";
    public int RestoreOrder => 60;

    private void Awake()
    {
        if (I != null && I != this)
        {
            Debug.LogWarning("[TutorialManager] シーンに2つあります。後から来たほうは使いません", this);
            return;
        }
        I = this;
        if (panel == null) panel = GetComponentInChildren<TutorialPanelUI>(true);
    }

    private void OnEnable() => SaveSystem.Register(this);
    private void OnDisable() => SaveSystem.Unregister(this);

    private void OnDestroy()
    {
        if (I == this) I = null;
    }

    // ─── 窓口 ─────────────────────────────────────────────

    /// もう見せたか。
    public bool HasSeen(TutorialData tutorial) => tutorial != null && seen.Contains(tutorial.Id);

    /// まだ見せていなければ出す（順番待ちに入れる）。入れたら true。
    public bool ShowOnce(TutorialData tutorial)
    {
        if (tutorial == null || HasSeen(tutorial) || queuedIds.Contains(tutorial.Id)) return false;
        Enqueue(tutorial);
        return true;
    }

    /// 見たかどうかに関係なく出す。
    public void Show(TutorialData tutorial)
    {
        if (tutorial == null || queuedIds.Contains(tutorial.Id)) return;
        Enqueue(tutorial);
    }

    private void Enqueue(TutorialData tutorial)
    {
        queue.Enqueue(tutorial);
        queuedIds.Add(tutorial.Id);
    }

    // 判断画面が全部閉じたら、順番待ちの先頭を開く
    private void Update()
    {
        if (queue.Count == 0 || panel == null || panel.IsOpen)
        {
            clearFrames = 0;
            return;
        }

        if (GameState.I != null && GameState.I.IsGameOver)
        {
            queue.Clear();
            queuedIds.Clear();
            return;
        }
        if (GameClock.I != null && GameClock.I.PauseRequests > 0)
        {
            clearFrames = 0;
            return;
        }
        if (++clearFrames <= waitFrames) return;   // 閉じた直後・別の画面が開きかけのときは少し待つ
        clearFrames = 0;

        var next = queue.Dequeue();
        queuedIds.Remove(next.Id);
        seen.Add(next.Id);   // 開いた時点で「見た」にする（閉じる前にセーブされても二度出さない）
        panel.Open(next, () => OnTutorialClosed?.Invoke(next));
        OnTutorialOpened?.Invoke(next);
    }

    /// 「見た」の記録を1件消す（テスト用。編集ウィンドウの「見た記録を消す」）。
    public void Forget(TutorialData tutorial)
    {
        if (tutorial != null) seen.Remove(tutorial.Id);
    }

    // ─── テスト用 ─────────────────────────────────────────

    [ContextMenu("テスト：見た記録を全部消す")]
    private void ResetSeenForTest()
    {
        seen.Clear();
        Debug.Log("[TutorialManager] 見た記録を消しました");
    }

    // ─── セーブ・ロード（ISaveParticipant）───────────────────

    [Serializable]
    private class TutorialSaveData
    {
        public List<string> seen = new List<string>();
    }

    public string Capture()
    {
        var d = new TutorialSaveData();
        d.seen.AddRange(seen);
        return JsonUtility.ToJson(d);
    }

    public void Restore(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        var d = JsonUtility.FromJson<TutorialSaveData>(json);
        if (d == null) return;
        seen.Clear();
        if (d.seen != null) foreach (var id in d.seen) seen.Add(id);
    }
}
