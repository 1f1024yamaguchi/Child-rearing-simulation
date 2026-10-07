using System;
using System.Collections.Generic;
using UnityEngine;
using Maruyama.Core;

namespace Arisaka
{
    /// <summary>
    /// 食事グレード（安価・普通・高価）
    /// </summary>
    public enum MealGrade
    {
        Cheap,     // 安価
        Normal,    // 普通
        Expensive  // 高価
    }

    /// <summary>
    /// 食事プランの定義データ。
    /// docs/design/food-system.md および effects.md に準拠。
    /// 費用はその月の日数で日割りされ、一日ごとに所持金から引き落とされる。
    /// </summary>
    [Serializable]
    public class MealPlan
    {
        public MealGrade grade;
        public string title;            // "安価", "普通", "高価"
        public int costPerMonth;        // 30000, 90000, 150000
        public string stressText;       // "ストレス値 +5", "ストレス値 -2", "ストレス値 -5"
        public string eventRateText;    // "Badイベント発生率+5%", "イベント発生率　変化なし", "Goodイベント発生率+5%"
        public Color iconBgColor;       // アイコン枠の背景色
        public Color stressTextColor;   // ストレス値テキストの色（プラス: 赤, マイナス: 回復の緑）
        public Color eventRateTextColor;// イベント発生率テキストの色（Bad: 赤, 変化なし: 通常色, Good: 緑）
        
        /// <summary>選択時に適用される効果（ストレス・イベント率補正）</summary>
        public List<Effect> selectEffects = new List<Effect>();

        /// <summary>視認性の高い警告・悪化の赤色</summary>
        public static readonly Color NegativeRed = new Color(0.85f, 0.20f, 0.20f, 1f);
        /// <summary>回復・癒やしをイメージさせる緑っぽい色</summary>
        public static readonly Color HealingGreen = new Color(0.14f, 0.62f, 0.32f, 1f);
        /// <summary>変化なし用の通常ダーク色</summary>
        public static readonly Color NeutralDark = new Color(0.20f, 0.20f, 0.20f, 1f);

        /// <summary>
        /// 一日ごとに引き落とされる日割り食費の Effect を生成する
        /// </summary>
        public Effect CreateDailyCostEffect(int dailyCost)
        {
            return new Effect
            {
                kind = EffectKind.Money,
                value = -dailyCost,
                target = EffectTarget.Household,
                duration = EffectDuration.Instant
            };
        }

        /// <summary>
        /// （旧仕様互換）月額費用の Effect を生成する
        /// </summary>
        public Effect CreateMonthlyCostEffect()
        {
            return new Effect
            {
                kind = EffectKind.Money,
                value = -costPerMonth,
                target = EffectTarget.Household,
                duration = EffectDuration.Instant
            };
        }

        /// <summary>
        /// 既定の3つの食事プランを生成して返す。
        /// </summary>
        public static List<MealPlan> CreateDefaultPlans()
        {
            return new List<MealPlan>
            {
                // 1. 安価: 30,000円/月, ストレス値+5(全員), Badイベント発生率+5%
                new MealPlan
                {
                    grade = MealGrade.Cheap,
                    title = "安価",
                    costPerMonth = 30000,
                    stressText = "ストレス値 +5",
                    stressTextColor = NegativeRed,
                    eventRateText = "Badイベント発生率+5%",
                    eventRateTextColor = NegativeRed,
                    iconBgColor = new Color(0.85f, 0.87f, 0.90f, 1f), // 灰色
                    selectEffects = new List<Effect>
                    {
                        new Effect { kind = EffectKind.Stress, value = 5f, target = EffectTarget.Everyone, duration = EffectDuration.Instant },
                        new Effect { kind = EffectKind.BadEventRate, value = 5f, target = EffectTarget.Household, duration = EffectDuration.ThisMonth }
                    }
                },

                // 2. 普通: 90,000円/月, ストレス値-2(全員), イベント発生率　変化なし
                new MealPlan
                {
                    grade = MealGrade.Normal,
                    title = "普通",
                    costPerMonth = 90000,
                    stressText = "ストレス値 -2",
                    stressTextColor = HealingGreen,
                    eventRateText = "イベント発生率　変化なし",
                    eventRateTextColor = NeutralDark,
                    iconBgColor = new Color(1.0f, 0.94f, 0.82f, 1f), // 薄黄色・クリーム色
                    selectEffects = new List<Effect>
                    {
                        new Effect { kind = EffectKind.Stress, value = -2f, target = EffectTarget.Everyone, duration = EffectDuration.Instant },
                        new Effect { kind = EffectKind.BadEventRate, value = 0f, target = EffectTarget.Household, duration = EffectDuration.ThisMonth }
                    }
                },

                // 3. 高価: 150,000円/月, ストレス値-5(全員), Goodイベント発生率+5%
                new MealPlan
                {
                    grade = MealGrade.Expensive,
                    title = "高価",
                    costPerMonth = 150000,
                    stressText = "ストレス値 -5",
                    stressTextColor = HealingGreen,
                    eventRateText = "Goodイベント発生率+5%",
                    eventRateTextColor = HealingGreen,
                    iconBgColor = new Color(1.0f, 0.74f, 0.75f, 1f), // 薄赤色・ピンク
                    selectEffects = new List<Effect>
                    {
                        new Effect { kind = EffectKind.Stress, value = -5f, target = EffectTarget.Everyone, duration = EffectDuration.Instant },
                        new Effect { kind = EffectKind.GoodEventRate, value = 5f, target = EffectTarget.Household, duration = EffectDuration.ThisMonth }
                    }
                }
            };
        }
    }
}
