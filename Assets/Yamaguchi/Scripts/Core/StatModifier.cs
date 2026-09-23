using System;
using UnityEngine;

/// 補正演算の種類。traits.md 2章。
public enum ModifierOp
{
    Add,        // 加算。値をそのまま足す
    Multiply,   // 倍率。+0.05 で 5% 増し、-0.3 で 3割減
}

/// 特性・状態が持つ1本の補正。
/// どの値（EffectKind）を、どう（ModifierOp）、どれだけ（value）動かすか。
[Serializable]
public class StatModifier
{
    [Tooltip("補正対象の値の種類")]
    public EffectKind kind;
    [Tooltip("加算か倍率か")]
    public ModifierOp op = ModifierOp.Multiply;
    [Tooltip("増減量。Multiply のときは +0.05 で 5% 増し")]
    public float value;
}
