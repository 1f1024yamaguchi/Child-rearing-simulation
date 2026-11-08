using System;
using UnityEngine;

/// <summary>
/// ミニゲーム1本ぶんの土台（MonoBehaviour ではない）。
/// 枠（説明画面・カウントダウン・残り時間・結果）は MiniGameManager が持ち、
/// 遊ぶ部分の UI と判定、説明画面に出す文と絵だけをこのクラスを継承して作る。
/// 値は家族のステータスに直接書かず、結果（Rank）を Finish で返すだけ。
/// </summary>
public abstract class MiniGame
{
    /// ドット絵の置き場所。
    protected readonly MiniGameArt art;

    protected MiniGame(MiniGameArt art)
    {
        this.art = art != null ? art : MiniGameArt.Load();
    }

    public abstract string Title { get; }
    /// 遊んでいる最中に上に出す一行説明。
    public abstract string Instruction { get; }
    public abstract float TimeLimit { get; }

    /// 説明画面の「あそびかた」（1行1手順）。
    public abstract string[] HowToSteps { get; }
    /// 説明画面の「まめちしき」（現実の子育ての知識）。
    public abstract string Tip { get; }
    /// 説明画面の左側に、このゲームの絵を並べる。
    public abstract void BuildIntroArt(RectTransform area);

    /// 残り秒数。0 になると OnTimeUp が呼ばれる。
    public float Remaining { get; private set; }
    public bool IsFinished { get; private set; }
    public MiniGameRank Rank { get; private set; }

    /// 結果が決まったとき（1回だけ）に通知される。
    public event Action<MiniGameRank> OnFinished;

    /// 結果に応じた、家族のステータスの変化。
    public abstract MiniGameEffect EffectFor(MiniGameRank rank);

    /// 結果画面に出す一言。
    public abstract string ResultMessage(MiniGameRank rank);

    /// 遊ぶ部分の UI を area の下に作る（まだ時間は進めない）。
    public void Begin(RectTransform area)
    {
        Remaining = TimeLimit;
        IsFinished = false;
        Build(area);
    }

    /// 毎フレーム呼ぶ。dt は実時間の秒。
    public void Tick(float dt)
    {
        if (IsFinished) return;
        Remaining -= dt;
        OnTick(dt);
        if (!IsFinished && Remaining <= 0f)
        {
            Remaining = 0f;
            OnTimeUp();
            if (!IsFinished) Finish(MiniGameRank.失敗);   // 念のため
        }
    }

    protected abstract void Build(RectTransform area);
    protected abstract void OnTick(float dt);
    /// 時間切れ。必ず Finish を呼ぶこと。
    protected abstract void OnTimeUp();

    /// 残り時間を減らす（失敗したときのペナルティ用）。
    protected void Penalty(float seconds)
    {
        Remaining = Mathf.Max(0f, Remaining - seconds);
    }

    protected void Finish(MiniGameRank rank)
    {
        if (IsFinished) return;
        IsFinished = true;
        Rank = rank;
        OnFinished?.Invoke(rank);
    }
}
