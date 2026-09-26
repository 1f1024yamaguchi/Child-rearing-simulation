using System;

/// 状態（Condition）が毎日1回だけ直接加算する効果。
/// 加算のみ。倍率（ModifierOp）は持たせない。
/// traits.md 3章「状態の『毎日の効果』（dailyEffects）」参照。
[Serializable]
public class DailyEffect
{
    /// 対象のステータスの種類。
    public EffectKind kind;

    /// 毎日加算する量（マイナスも可）。
    public float value;
}
