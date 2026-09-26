using System.Collections.Generic;
using UnityEngine;

/// 毎日1回、全員の状態（Condition）を処理する。
/// 順序：① 毎日の効果（dailyEffects）を適用 → ② 残り日数を1日減らして期限切れを外す（TickDay）。
/// 7日の状態なら、毎日の効果はちょうど7回効く。traits.md 3章・4章。
/// Trait は期限が無いのでなめない。
///
/// 【安全対策】ループ中に AddCondition / RemoveCondition が呼ばれても壊れないようにするため、
/// その人の状態リストを写しに詰めてから回す。写しはフィールドで使い回す（毎日 new しない）。
/// antigravity-child-status.md「今後：イベント機能と繋ぐ段階で必ずやること」参照。
public class ConditionSystem : MonoBehaviour
{
    /// 状態の写し（スナップショット）。フィールドで持ち使い回す。
    private readonly List<ActiveCondition> _snapshot = new List<ActiveCondition>();

    private void Start()
    {
        if (DayTicker.I == null)
        {
            Debug.LogError("ConditionSystem: DayTicker が見つかりません");
            return;
        }
        DayTicker.I.OnDayPassed += TickAll;
    }

    private void OnDestroy()
    {
        if (DayTicker.I != null) DayTicker.I.OnDayPassed -= TickAll;
    }

    private void TickAll(int day)
    {
        if (Household.I == null) return;
        foreach (var member in Household.I.Members)
        {
            // ① 状態リストの写しを作る（ループ中にリストが増減しても安全）
            _snapshot.Clear();
            _snapshot.AddRange(member.Conditions.All);

            foreach (var active in _snapshot)
            {
                if (active.definition == null) continue;

                // ループ中に外れた状態はスキップ（その日の残り効果もかからない）
                if (!member.Conditions.Has(active.definition.id)) continue;

                // 毎日の効果を適用する
                foreach (var effect in active.definition.dailyEffects)
                {
                    member.ApplyDailyEffect(effect);
                }
                // ※ ループ中に新たに付いた状態は写しに入っていないため、翌日からかかる
            }

            // ② そのあと残り日数を減らして期限切れを外す
            //    TickDay は後ろから for で回しているので、元のリストに対して呼んで安全
            member.Conditions.TickDay();
        }
    }
}
