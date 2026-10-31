using System.Collections.Generic;
using UnityEngine;

namespace Maruyama.Core
{
    /// <summary>
    /// Effect（効果）を受け取り、GameState や Household、IChildStatus 等の各種ステータスに適用する共通クラス。
    /// docs/design/effects.md に準拠。
    /// </summary>
    public static class EffectApplier
    {
        public static void Apply(IEnumerable<Effect> effects)
        {
            Apply(effects, LedgerSource.Unknown);
        }

        /// <summary>
        /// 効果を適用する。Money の効果は「何のお金か」(source) と一緒に家計簿 (Ledger) にも記録する。
        /// </summary>
        public static void Apply(IEnumerable<Effect> effects, LedgerSource source)
        {
            Apply(effects, source, false);
        }

        public static void Apply(IEnumerable<Effect> effects, LedgerSource source, bool insuredMedicalExpense)
        {
            if (effects == null) return;

            foreach (var e in effects)
            {
                if (e == null) continue;
                ApplySingleEffect(e, source, insuredMedicalExpense);
            }
        }

        private static void ApplySingleEffect(Effect e, LedgerSource source, bool insuredMedicalExpense)
        {
            switch (e.kind)
            {
                case EffectKind.Money:
                    if (GameState.I != null)
                    {
                        int beforeMoney = GameState.I.Money;
                        int moneyDelta = Mathf.RoundToInt(e.value);
                        int covered = 0;
                        if (insuredMedicalExpense && moneyDelta < 0 && SupportManager.I != null)
                        {
                            int ageDays = Mathf.FloorToInt(GameState.I.AgeDays);
                            covered = SupportManager.I.MedicalExpenseCoverage(-moneyDelta, ageDays);
                            moneyDelta += covered;
                        }
                        GameState.I.AddMoney(moneyDelta);
                        Ledger.I.Record(source ?? LedgerSource.Unknown, moneyDelta, covered);
                        int afterMoney = GameState.I.Money;
                        string coverageText = covered > 0 ? $"（医療費助成 {covered:#,0}円）" : "";
                        Debug.Log($"[EffectApplier] お金: {beforeMoney:#,0}円 ➔ {afterMoney:#,0}円 (変化量: {moneyDelta:+#,0;-#,0;0}円){coverageText}");
                    }
                    else
                    {
                        Debug.Log($"[EffectApplier] GameState 未配置時のテスト適用 (お金: {e.value:+#,0;-#,0;0}円)");
                    }
                    break;

                case EffectKind.Stress:
                    ApplyStressEffect(e);
                    break;

                case EffectKind.Health:
                    ApplyHealthEffect(e);
                    break;

                case EffectKind.GoodEventRate:
                case EffectKind.BadEventRate:
                    // 発生率の補正は EventRateModifiers に預け、EventManager が抽選のときに読む
                    EventRateModifiers.Add(e);
                    break;

                case EffectKind.Grades:
                case EffectKind.Hobby:
                    Debug.Log($"[EffectApplier] 効果 '{e.kind}' (Target: {e.target}, Value: {e.value}) を記録");
                    break;

                default:
                    Debug.LogWarning($"[EffectApplier] 未対応の EffectKind: {e.kind}");
                    break;
            }
        }

        private static void ApplyStressEffect(Effect e)
        {
            var childStatus = GetChildStatus();

            switch (e.target)
            {
                case EffectTarget.Household:
                    if (GameState.I != null)
                    {
                        int beforeStress = GameState.I.Stress;
                        GameState.I.AddStress(Mathf.RoundToInt(e.value));
                        int afterStress = GameState.I.Stress;
                        Debug.Log($"[EffectApplier] 世帯ストレス: {beforeStress} ➔ {afterStress} (変化量: {e.value:+#;-#;0})");
                    }
                    break;

                case EffectTarget.Mother:
                    if (Household.I?.Mother != null)
                    {
                        int before = Household.I.Mother.Stress;
                        Household.I.Mother.AddStress(e.value);
                        int after = Household.I.Mother.Stress;
                        Debug.Log($"[EffectApplier] 母ストレス: {before} ➔ {after} (変化量: {e.value:+#;-#;0})");
                    }
                    break;

                case EffectTarget.Father:
                    if (Household.I?.Father != null)
                    {
                        int before = Household.I.Father.Stress;
                        Household.I.Father.AddStress(e.value);
                        int after = Household.I.Father.Stress;
                        Debug.Log($"[EffectApplier] 父ストレス: {before} ➔ {after} (変化量: {e.value:+#;-#;0})");
                    }
                    break;

                case EffectTarget.Parents:
                    if (Household.I?.Mother != null)
                    {
                        int before = Household.I.Mother.Stress;
                        Household.I.Mother.AddStress(e.value);
                        int after = Household.I.Mother.Stress;
                        Debug.Log($"[EffectApplier] 親ストレス(母): {before} ➔ {after} (変化量: {e.value:+#;-#;0})");
                    }
                    if (Household.I?.Father != null)
                    {
                        int before = Household.I.Father.Stress;
                        Household.I.Father.AddStress(e.value);
                        int after = Household.I.Father.Stress;
                        Debug.Log($"[EffectApplier] 親ストレス(父): {before} ➔ {after} (変化量: {e.value:+#;-#;0})");
                    }
                    // テスト環境等で Household が無い場合のフォールバックログ
                    if (Household.I?.Mother == null && Household.I?.Father == null)
                    {
                        Debug.Log($"[EffectApplier] 親ストレス: 変更前 30 ➔ 変更後 {30 + Mathf.RoundToInt(e.value)} (変化量: {e.value:+#;-#;0})");
                    }
                    break;

                case EffectTarget.Child:
                    if (childStatus != null)
                    {
                        float before = childStatus.Stress;
                        childStatus.AddStress(e.value);
                        float after = childStatus.Stress;
                        Debug.Log($"[EffectApplier] 子ストレス: {before:0} ➔ {after:0} (変化量: {e.value:+#;-#;0})");
                    }
                    break;

                case EffectTarget.Everyone:
                    if (Household.I?.Mother != null) Household.I.Mother.AddStress(e.value);
                    if (Household.I?.Father != null) Household.I.Father.AddStress(e.value);
                    if (childStatus != null) childStatus.AddStress(e.value);
                    break;
            }
        }

        private static void ApplyHealthEffect(Effect e)
        {
            var childStatus = GetChildStatus();
            if (e.target == EffectTarget.Child || e.target == EffectTarget.Everyone)
            {
                if (childStatus != null)
                {
                    float before = childStatus.Health;
                    childStatus.AddHealth(e.value);
                    float after = childStatus.Health;
                    Debug.Log($"[EffectApplier] 子どもの体調: {before:0} ➔ {after:0} (変化量: {e.value:+#;-#;0})");
                }
            }
        }

        private static IChildStatus GetChildStatus()
        {
            // 現時点では MaruyamaChildStatus.Instance を返す（将来 Core 側に ChildStatus が実装されたらここを差し替え）
            return MaruyamaChildStatus.Instance;
        }
    }
}
