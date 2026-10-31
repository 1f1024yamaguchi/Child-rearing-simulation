using System.Collections.Generic;
using UnityEngine;

namespace Maruyama.Events
{
    public enum EventCategory
    {
        Demerit, // デメリットイベント（⚠️ デメリット）
        Merit    // メリットイベント（✅ メリット）
    }

    /// <summary>イベントの起き方。</summary>
    public enum EventTrigger
    {
        Random, // 月ごとの抽選で起きる（悪い・良いを別々に抽選）
        Fixed,  // 決まった日付に必ず起きる（誕生日・初めての言葉など）
    }

    [CreateAssetMenu(fileName = "NewEventData", menuName = "ChildRearing/Event Data")]
    public class EventData : ScriptableObject
    {
        public string id;                           // 識別用ID
        public EventCategory category;              // メリット / デメリット
        public string title;                        // タイトル（例: "みらいが風邪をひいた"）
        [TextArea(2, 5)]
        public string description;                  // 説明文
        public Sprite iconSprite;                   // アイコン画像
        public List<EventChoice> choices = new List<EventChoice>(); // 選択肢リスト
        public List<EventConditionSO> conditions = new List<EventConditionSO>(); // 発生条件リスト

        [Tooltip("抽選で選ばれやすさ。大きいほど選ばれやすい（条件を満たしたイベントの中での比率）。0 なら選ばれない。trigger = Random のときだけ使う。")]
        [Min(0f)] public float weight = 1f;

        [Header("発生のしかた")]
        [Tooltip("Random = 月ごとの抽選。Fixed = 下の日付に必ず起きる。")]
        public EventTrigger trigger = EventTrigger.Random;

        [Header("固定イベントの日付（trigger = Fixed のときだけ使う）")]
        [Tooltip("起きる月（1〜12）")]
        [Range(1, 12)] public int fixedMonth = 1;
        [Tooltip("起きる日（1〜31）")]
        [Range(1, 31)] public int fixedDay = 1;
        [Tooltip("この歳から起きる（満年齢）")]
        [Min(0)] public int fixedMinAgeYears = 0;
        [Tooltip("この歳まで起きる（満年齢）。最小と同じ値なら1回だけ。違う値なら、その範囲の毎年起きる（誕生日など）。")]
        [Min(0)] public int fixedMaxAgeYears = 0;

        /// <summary>固定イベントか。</summary>
        public bool IsFixed => trigger == EventTrigger.Fixed;

        public bool CheckConditions()
        {
            if (conditions == null || conditions.Count == 0) return true;
            foreach (var cond in conditions)
            {
                if (cond != null && !cond.IsSatisfied()) return false;
            }
            return true;
        }
    }
}
