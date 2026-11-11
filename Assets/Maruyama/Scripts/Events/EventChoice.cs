using System;
using System.Collections.Generic;
using Maruyama.Core;
using UnityEngine;

namespace Maruyama.Events
{
    [Serializable]
    public class EventChoice
    {
        [Tooltip("選択肢ID（ストーリー分岐・履歴記録用。空の場合は自動採番またはフォールバック）")]
        public string id = string.Empty;

        public string label;                 // ボタン表示ラベル（例: "病院に行く"）
        public List<Effect> effects;         // この選択肢を押した時に適用される Effect リスト
        public ExpenseCategory ledgerCategory = ExpenseCategory.その他;   // お金が動くとき、家計簿に載せる種類（病院なら 医療）
        public bool insuredMedicalExpense;   // Money の負の効果を保険診療の自己負担として助成対象にする

        [Tooltip("この選択肢を押せる条件（すべて満たしたとき押せる。空ならいつでも押せる）")]
        public List<EventConditionSO> showConditions = new List<EventConditionSO>();

        /// <summary>
        /// 条件を満たして選択可能かを判定する。満たさない場合は最初に失敗した理由を返す。
        /// </summary>
        public bool IsAvailable(out string failReason)
        {
            failReason = string.Empty;
            if (showConditions == null || showConditions.Count == 0) return true;

            foreach (var cond in showConditions)
            {
                if (cond == null) continue;
                if (!cond.IsSatisfied())
                {
                    if (cond is IDescribable desc)
                    {
                        failReason = desc.Describe();
                    }
                    else
                    {
                        failReason = cond.name;
                    }
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 選択肢IDを取得する。空の場合は "c" + (index + 1) を fallback として返し、警告を出す。
        /// </summary>
        public string GetOrFallbackId(int index)
        {
            if (!string.IsNullOrEmpty(id)) return id;
            string fallback = $"c{index + 1}";
            // 表示やオートのたびに出ると埋もれるので、同じ選択肢（ラベル＋番号）につき1回だけ警告する
            if (warnedEmptyIds.Add($"{label}#{index}"))
            {
                Debug.LogWarning($"[EventChoice] 選択肢 '{label}' の id が空です。'{fallback}' を使います。" +
                                 "メニュー「子育て/イベント/選択肢に ID を付ける」で付けてください。");
            }
            return fallback;
        }

        /// <summary>警告を出し済みの選択肢（ラベル#番号）。ゲーム中だけ覚えておけばよい。</summary>
        private static readonly HashSet<string> warnedEmptyIds = new HashSet<string>();
    }
}
