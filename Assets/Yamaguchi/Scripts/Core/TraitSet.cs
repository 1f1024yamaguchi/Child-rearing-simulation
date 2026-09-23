using System;
using System.Collections.Generic;
using UnityEngine;

/// 永続の特性リスト。MonoBehaviour にはしない。
/// PersonStatus が1つ持ち、[SerializeField] でシリアライズされる。
/// traits.md 4章・ADR 0007（なぜ ConditionSet と分けるか）。
[Serializable]
public class TraitSet
{
    [SerializeField] private List<TraitDefinition> traits = new List<TraitDefinition>();

    // ─── 変更通知 ───────────────────────────────────────────
    /// 特性リストが変わったときに通知される。
    public event Action OnChanged;

    // ─── 読み取り ───────────────────────────────────────────
    /// 全特性の列挙。
    public IReadOnlyList<TraitDefinition> All => traits;

    /// 指定 id の特性を持っているか。
    public bool Has(string id) => traits.Exists(t => t != null && t.id == id);

    // ─── 書き込み ───────────────────────────────────────────
    /// 特性を追加する。すでに同じ id を持つ場合は追加しない。
    public void Add(TraitDefinition def)
    {
        if (def == null) return;
        if (Has(def.id)) return;
        traits.Add(def);
        OnChanged?.Invoke();
    }

    /// 指定 id の特性を除去する。
    public void Remove(string id)
    {
        int idx = traits.FindIndex(t => t != null && t.id == id);
        if (idx < 0) return;
        traits.RemoveAt(idx);
        OnChanged?.Invoke();
    }
}
