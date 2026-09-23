using System;
using UnityEngine;

/// ストレスの段階（rules-and-stats.md「ストレス値の段階」に準拠）
public enum StressLevel
{
    良好,   // 0–30
    普通,   // 31–70
    危険,   // 71–90
    限界,   // 91–100
}

/// 母親・父親・子どもに共通する「人」の土台クラス。
/// 表示名・ストレス・特性・状態を持ち、変更通知を出す。
public abstract class PersonStatus : MonoBehaviour
{
    [Header("この人の設定")]
    [SerializeField] private string displayName = "未設定";
    [SerializeField][Range(0f, 100f)] private float stress = 30f;

    [Header("特性・状態")]
    [SerializeField] private TraitSet traits = new TraitSet();
    [SerializeField] private ConditionSet conditions = new ConditionSet();

    // ─── 変更通知 ───────────────────────────────────────────
    /// ストレス値（整数）が変わったときに通知される。
    public event Action<int> OnStressChanged;

    // ─── 読み取り ───────────────────────────────────────────
    /// この人の表示名。
    public string DisplayName => displayName;

    /// ストレス値（0–100 の整数）。
    public int Stress => Mathf.RoundToInt(stress);

    /// ストレスの段階。rules-and-stats.md の区分に対応。
    public StressLevel StressLevel
    {
        get
        {
            int s = Stress;
            if (s <= 30) return StressLevel.良好;
            if (s <= 70) return StressLevel.普通;
            if (s <= 90) return StressLevel.危険;
            return StressLevel.限界;
        }
    }

    /// 永続の特性リスト。
    public TraitSet Traits => traits;

    /// 一時の状態リスト。
    public ConditionSet Conditions => conditions;

    // ─── 補正の収集 ─────────────────────────────────────────
    /// 指定した EffectKind の補正を Traits と Conditions の両方から集め、
    /// 加算の合計と倍率の合計をタプルで返す。
    /// 計算順は traits.md 3章：最終値 = (元の値 + addSum) * (1 + mulSum)
    public (float addSum, float mulSum) CollectModifier(EffectKind kind)
    {
        float addSum = 0f;
        float mulSum = 0f;

        // 特性（永続）
        foreach (var trait in traits.All)
        {
            if (trait == null) continue;
            foreach (var mod in trait.modifiers)
            {
                if (mod.kind != kind) continue;
                if (mod.op == ModifierOp.Add)      addSum += mod.value;
                else                               mulSum += mod.value;
            }
        }

        // 状態（一時）
        foreach (var active in conditions.All)
        {
            if (active.definition == null) continue;
            foreach (var mod in active.definition.modifiers)
            {
                if (mod.kind != kind) continue;
                if (mod.op == ModifierOp.Add)      addSum += mod.value;
                else                               mulSum += mod.value;
            }
        }

        return (addSum, mulSum);
    }

    // ─── 特性・状態の追加・除去 ─────────────────────────────
    /// 特性を付ける。すでに同じ id なら無視する。
    public void AddTrait(TraitDefinition def)
    {
        traits.Add(def);
    }

    /// 状態を付ける。days を省略（-1）したら定義の既定日数を使う。
    public void AddCondition(ConditionDefinition def, int days = -1)
    {
        if (def == null) return;
        int d = days < 0 ? def.defaultDays : days;
        conditions.Add(def, d);
    }

    /// 指定 id の状態を除去する。
    public void RemoveCondition(string id)
    {
        conditions.Remove(id);
    }

    // ─── 書き込み ───────────────────────────────────────────
    /// ストレスを delta 分だけ増減する。クランプは 0–100。
    /// delta > 0 のときだけ Stress 補正を掛ける（上がりにくさであって下がりにくさではない）。
    public void AddStress(float delta)
    {
        int prev = Stress;
        if (delta > 0f)
        {
            var (addSum, mulSum) = CollectModifier(EffectKind.Stress);
            delta = (delta + addSum) * (1f + mulSum);
        }
        stress = Mathf.Clamp(stress + delta, 0f, 100f);
        if (Stress != prev)
            OnStressChanged?.Invoke(Stress);
    }

    /// ストレスを指定値に直接セットする。クランプは 0–100。
    public void SetStress(float value)
    {
        int prev = Stress;
        stress = Mathf.Clamp(value, 0f, 100f);
        if (Stress != prev)
            OnStressChanged?.Invoke(Stress);
    }
}
