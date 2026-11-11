using System.Collections.Generic;
using Maruyama.Events;
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

            // 「趣味を決める」を先に済ませる。
            // 趣味が「なし」のあいだは趣味の経験値（Hobby）が入らない（ChildStatus.AddHobbyExp）ので、
            // 同じ選択肢に「趣味を決める」と「趣味 +20」があると、並び順しだいで +20 が消えてしまうため。
            foreach (var e in effects)
            {
                if (e != null && e.kind == EffectKind.SetHobby) ApplySingleEffect(e, source, insuredMedicalExpense);
            }
            foreach (var e in effects)
            {
                if (e == null || e.kind == EffectKind.SetHobby) continue;
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
                    ApplyChildGrowthEffect(e);
                    break;

                case EffectKind.SetFlag:
                    if (e.asset is StoryFlag setFlag)
                    {
                        if (StoryState.I != null) StoryState.I.SetFlag(setFlag);
                        else Debug.LogWarning("[EffectApplier] SetFlag: StoryState.I が見つかりません");
                    }
                    else
                    {
                        Debug.LogWarning($"[EffectApplier] SetFlag: asset が StoryFlag ではないか空です ({e.asset})");
                    }
                    break;

                case EffectKind.ClearFlag:
                    if (e.asset is StoryFlag clearFlag)
                    {
                        if (StoryState.I != null) StoryState.I.ClearFlag(clearFlag);
                        else Debug.LogWarning("[EffectApplier] ClearFlag: StoryState.I が見つかりません");
                    }
                    else
                    {
                        Debug.LogWarning($"[EffectApplier] ClearFlag: asset が StoryFlag ではないか空です ({e.asset})");
                    }
                    break;

                case EffectKind.SetHobby:
                    if (Household.I?.Child != null)
                    {
                        var hobby = (HobbyKind)Mathf.RoundToInt(e.value);
                        Household.I.Child.SetHobby(hobby);
                        Debug.Log($"[EffectApplier] 趣味を設定: {hobby}");
                    }
                    else
                    {
                        Debug.LogWarning("[EffectApplier] SetHobby: Household.I.Child が見つかりません");
                    }
                    break;

                case EffectKind.AddTrait:
                    if (e.asset is TraitDefinition traitDef)
                    {
                        ApplyTraitToTarget(traitDef, e.target);
                    }
                    else
                    {
                        Debug.LogWarning($"[EffectApplier] AddTrait: asset が TraitDefinition ではないか空です ({e.asset})");
                    }
                    break;

                case EffectKind.AddCondition:
                    if (e.asset is ConditionDefinition condDef)
                    {
                        int days = e.duration == EffectDuration.Days ? e.days : -1;
                        ApplyConditionToTarget(condDef, days, e.target);
                    }
                    else
                    {
                        Debug.LogWarning($"[EffectApplier] AddCondition: asset が ConditionDefinition ではないか空です ({e.asset})");
                    }
                    break;

                case EffectKind.ScheduleEvent:
                    if (e.asset is EventData schedEvent)
                    {
                        int today = GameState.I != null ? Mathf.FloorToInt(GameState.I.AgeDays) : 0;
                        int targetDay = today + e.days;
                        if (StoryState.I != null)
                        {
                            StoryState.I.Schedule(schedEvent.id, targetDay);
                            Debug.Log($"[EffectApplier] イベント予約: '{schedEvent.id}' を {targetDay}日目（今日+{e.days}日）に予約しました");
                        }
                        else
                        {
                            Debug.LogWarning("[EffectApplier] ScheduleEvent: StoryState.I が見つかりません");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[EffectApplier] ScheduleEvent: asset が EventData ではないか空です ({e.asset})");
                    }
                    break;

                case EffectKind.ShowStory:
                    if (e.asset is TutorialData tutData)
                    {
                        if (TutorialManager.I != null)
                        {
                            TutorialManager.I.ShowOnce(tutData);
                        }
                        else
                        {
                            Debug.LogWarning("[EffectApplier] ShowStory: TutorialManager.I が見つかりません");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[EffectApplier] ShowStory: asset が TutorialData ではないか空です ({e.asset})");
                    }
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
                    AddChildStress(e.value, childStatus);
                    break;

                case EffectTarget.Everyone:
                    if (Household.I?.Mother != null) Household.I.Mother.AddStress(e.value);
                    if (Household.I?.Father != null) Household.I.Father.AddStress(e.value);
                    AddChildStress(e.value, childStatus);
                    break;
            }
        }

        private static void ApplyHealthEffect(Effect e)
        {
            if (e.target != EffectTarget.Child && e.target != EffectTarget.Everyone) return;

            // 本編では本物の子ども（ChildStatus）へ。補正は ChildStatus.AddHealth の中で掛かる
            var child = CoreChild();
            if (child != null)
            {
                int before = child.Health;
                child.AddHealth(e.value);
                Debug.Log($"[EffectApplier] 子どもの体調: {before} ➔ {child.Health} (変化量: {e.value:+#;-#;0})");
                return;
            }

            // テストシーン（Household が無い）だけ仮の子どもへ
            var childStatus = GetChildStatus();
            if (childStatus != null)
            {
                float before = childStatus.Health;
                childStatus.AddHealth(e.value);
                float after = childStatus.Health;
                Debug.Log($"[EffectApplier] 子どもの体調（テスト用の子ども）: {before:0} ➔ {after:0} (変化量: {e.value:+#;-#;0})");
            }
        }

        /// <summary>子どものストレス。本編では ChildStatus（補正は AddStress の中で掛かる）、テストシーンでは仮の子どもへ。</summary>
        private static void AddChildStress(float value, IChildStatus testChild)
        {
            var child = CoreChild();
            if (child != null)
            {
                int before = child.Stress;
                child.AddStress(value);
                Debug.Log($"[EffectApplier] 子ストレス: {before} ➔ {child.Stress} (変化量: {value:+#;-#;0})");
                return;
            }

            if (testChild != null)
            {
                float before = testChild.Stress;
                testChild.AddStress(value);
                Debug.Log($"[EffectApplier] 子ストレス（テスト用の子ども）: {before:0} ➔ {testChild.Stress:0} (変化量: {value:+#;-#;0})");
            }
        }

        /// <summary>
        /// 成績（Grades）・趣味（Hobby）。子どもだけが持つ値なので、対象が子ども／家族全員のときだけ効く。
        /// 補正（特性・状態）は ChildStatus.AddGrade / AddHobbyExp の中で掛かるので、ここでは掛けない（traits.md 3章）。
        /// </summary>
        private static void ApplyChildGrowthEffect(Effect e)
        {
            if (e.target != EffectTarget.Child && e.target != EffectTarget.Everyone)
            {
                Debug.Log($"[EffectApplier] 効果 '{e.kind}' は子どもだけの値なので、対象 {e.target} には何もしません");
                return;
            }

            var child = CoreChild();
            if (child == null)
            {
                Debug.Log($"[EffectApplier] 効果 '{e.kind}' (Value: {e.value})：子ども（ChildStatus）が無いので記録のみ");
                return;
            }

            if (e.kind == EffectKind.Grades)
            {
                int before = child.Grade;
                child.AddGrade(e.value);
                Debug.Log($"[EffectApplier] 成績: {before} ➔ {child.Grade} (変化量: {e.value:+#;-#;0})");
            }
            else
            {
                child.AddHobbyExp(e.value);
                Debug.Log($"[EffectApplier] 趣味の経験値: {e.value:+#;-#;0}（{child.Hobby} Lv{child.HobbyLevel}）");
            }
        }

        /// <summary>本編の子ども（Household の ChildStatus）。無ければ null（テストシーン）。</summary>
        private static ChildStatus CoreChild()
        {
            if (Household.I == null) return null;
            var child = Household.I.Child;
            return child != null ? child : null;
        }

        private static IChildStatus GetChildStatus()
        {
            // 現時点では MaruyamaChildStatus.Instance を返す（将来 Core 側に ChildStatus が実装されたらここを差し替え）
            return MaruyamaChildStatus.Instance;
        }

        private static void ApplyTraitToTarget(TraitDefinition trait, EffectTarget target)
        {
            if (Household.I == null)
            {
                Debug.Log($"[EffectApplier] Household 未配置のため特性付与ログのみ: {trait.displayName} -> {target}");
                return;
            }

            switch (target)
            {
                case EffectTarget.Mother:
                    Household.I.Mother?.AddTrait(trait);
                    break;
                case EffectTarget.Father:
                    Household.I.Father?.AddTrait(trait);
                    break;
                case EffectTarget.Parents:
                    Household.I.Mother?.AddTrait(trait);
                    Household.I.Father?.AddTrait(trait);
                    break;
                case EffectTarget.Child:
                    Household.I.Child?.AddTrait(trait);
                    break;
                case EffectTarget.Everyone:
                    Household.I.Mother?.AddTrait(trait);
                    Household.I.Father?.AddTrait(trait);
                    Household.I.Child?.AddTrait(trait);
                    break;
                case EffectTarget.Household:
                    Debug.LogWarning("[EffectApplier] AddTrait: 世帯(Household)には特性を付けられません。対象を母・父・子などに指定してください。");
                    break;
            }
        }

        private static void ApplyConditionToTarget(ConditionDefinition cond, int days, EffectTarget target)
        {
            if (Household.I == null)
            {
                Debug.Log($"[EffectApplier] Household 未配置のため状態付与ログのみ: {cond.displayName} -> {target}");
                return;
            }

            switch (target)
            {
                case EffectTarget.Mother:
                    Household.I.Mother?.AddCondition(cond, days);
                    break;
                case EffectTarget.Father:
                    Household.I.Father?.AddCondition(cond, days);
                    break;
                case EffectTarget.Parents:
                    Household.I.Mother?.AddCondition(cond, days);
                    Household.I.Father?.AddCondition(cond, days);
                    break;
                case EffectTarget.Child:
                    Household.I.Child?.AddCondition(cond, days);
                    break;
                case EffectTarget.Everyone:
                    Household.I.Mother?.AddCondition(cond, days);
                    Household.I.Father?.AddCondition(cond, days);
                    Household.I.Child?.AddCondition(cond, days);
                    break;
                case EffectTarget.Household:
                    Debug.LogWarning("[EffectApplier] AddCondition: 世帯(Household)には状態を付けられません。対象を母・父・子などに指定してください。");
                    break;
            }
        }
    }
}
