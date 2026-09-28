using System.Collections.Generic;
using UnityEngine;

namespace Maruyama.Events
{
    public enum EventCategory
    {
        Demerit, // デメリットイベント（⚠️ デメリット）
        Merit    // メリットイベント（✅ メリット）
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
