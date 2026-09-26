using System;
using System.Collections.Generic;
using UnityEngine;

/// 1件ぶんの状態インスタンス（定義 + 残り日数）。
[Serializable]
public class ActiveCondition
{
    public ConditionDefinition definition;
    public int remainingDays;

    public ActiveCondition(ConditionDefinition def, int days)
    {
        definition = def;
        remainingDays = days;
    }
}

/// 一時の状態リスト。MonoBehaviour にはしない。
/// PersonStatus が1つ持ち、[SerializeField] でシリアライズされる。
/// TraitSet とは必ず別クラスにする（ADR 0007 参照）。
[Serializable]
public class ConditionSet
{
    [SerializeField] private List<ActiveCondition> conditions = new List<ActiveCondition>();

    // ─── 変更通知 ───────────────────────────────────────────
    /// 状態リストが変わったときに通知される。
    public event Action OnChanged;

    // ─── 読み取り ───────────────────────────────────────────
    /// 全状態の列挙。
    public IReadOnlyList<ActiveCondition> All => conditions;

    /// 指定 id の状態を持っているか。
    public bool Has(string id) => conditions.Exists(c => c.definition != null && c.definition.id == id);

    /// 優先度が最も高い状態を返す（0 件なら null）。
    public ActiveCondition Top()
    {
        if (conditions.Count == 0) return null;
        ActiveCondition best = conditions[0];
        for (int i = 1; i < conditions.Count; i++)
        {
            if (conditions[i].definition != null &&
                conditions[i].definition.priority > (best.definition?.priority ?? 0))
                best = conditions[i];
        }
        return best;
    }

    // ─── 書き込み ───────────────────────────────────────────
    /// 状態を追加する。すでに同じ id を持つ場合は残り日数を上書きする。
    public void Add(ConditionDefinition def, int days)
    {
        if (def == null) return;
        int idx = conditions.FindIndex(c => c.definition != null && c.definition.id == def.id);
        if (idx >= 0)
        {
            // 既存なら日数を上書き
            conditions[idx].remainingDays = days;
        }
        else
        {
            conditions.Add(new ActiveCondition(def, days));
        }
        OnChanged?.Invoke();
    }

    /// 指定 id の状態を除去する。
    public void Remove(string id)
    {
        int idx = conditions.FindIndex(c => c.definition != null && c.definition.id == id);
        if (idx < 0) return;
        conditions.RemoveAt(idx);
        OnChanged?.Invoke();
    }

    /// 全状態の残り日数を1日減らし、0 になったものを除去する。
    /// ConditionSystem が毎日呼ぶ。
    /// 状態が1件でもあれば OnChanged を出す（カードの「あと○日」更新のため）。
    public void TickDay()
    {
        if (conditions.Count == 0) return;

        for (int i = conditions.Count - 1; i >= 0; i--)
        {
            conditions[i].remainingDays--;
            if (conditions[i].remainingDays <= 0)
                conditions.RemoveAt(i);
        }
        // 状態が1件でもあれば常に通知（残り日数の変化をカードに伝えるため）。
        OnChanged?.Invoke();
    }

    /// 全状態を除去する（テスト用）。
    public void RemoveAll()
    {
        if (conditions.Count == 0) return;
        conditions.Clear();
        OnChanged?.Invoke();
    }
}
