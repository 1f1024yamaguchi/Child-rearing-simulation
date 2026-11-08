using System;
using System.Text;
using UnityEngine;

/// ミニゲームの結果（3段階）。失敗してもゲームオーバーにはならず、ステータスが悪い方に動くだけ。
public enum MiniGameRank { 失敗, 成功, 大成功 }

/// どのミニゲームか。
public enum MiniGameKind { ミルクの温度合わせ, 寝かしつけ, ミルクを飲ませる }

/// 結果が家族のステータスに与える変化。プラスで増える／マイナスで減る。
/// 数値はすべて調整前のたたき台。
[Serializable]
public struct MiniGameEffect
{
    [Tooltip("子どものストレスの増減（マイナスで下がる）")]
    public float childStress;
    [Tooltip("子どもの体調の増減（プラスで良くなる）")]
    public float childHealth;
    [Tooltip("親全員のストレスの増減（マイナスで下がる）")]
    public float parentStress;

    /// 結果画面に出す説明文。変化がなければ「変化なし」。
    public string Describe()
    {
        var sb = new StringBuilder();
        Append(sb, "子どものストレス", childStress);
        Append(sb, "子どもの体調", childHealth);
        Append(sb, "親のストレス", parentStress);
        return sb.Length == 0 ? "変化なし" : sb.ToString();
    }

    private static void Append(StringBuilder sb, string label, float value)
    {
        if (Mathf.Approximately(value, 0f)) return;
        if (sb.Length > 0) sb.Append('\n');
        sb.Append(label).Append("  ").Append(value > 0f ? "+" : "−").Append(Mathf.Abs(value).ToString("0.#"));
    }
}

/// 結果ごとの変化の一覧。
[Serializable]
public class MiniGameEffectTable
{
    public MiniGameEffect failure;
    public MiniGameEffect success;
    public MiniGameEffect great;

    public MiniGameEffectTable() { }

    public MiniGameEffectTable(MiniGameEffect failure, MiniGameEffect success, MiniGameEffect great)
    {
        this.failure = failure;
        this.success = success;
        this.great = great;
    }

    public MiniGameEffect Get(MiniGameRank rank)
    {
        switch (rank)
        {
            case MiniGameRank.大成功: return great;
            case MiniGameRank.成功:   return success;
            default:                   return failure;
        }
    }
}
