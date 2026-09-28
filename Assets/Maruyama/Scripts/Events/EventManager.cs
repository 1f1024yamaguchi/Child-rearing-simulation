using System.Collections.Generic;
using Maruyama.UI;
using UnityEngine;

namespace Maruyama.Events
{
    /// <summary>
    /// 月次イベント等の発火タイミングで、条件を満たすデメリットイベント・メリットイベントを選出し、
    /// EventDialogUI へ発生を要求するマネージャー。
    /// </summary>
    public class EventManager : MonoBehaviour
    {
        public static EventManager Instance { get; private set; }

        [Header("登録イベント一覧")]
        [SerializeField] private List<EventData> demeritEvents = new List<EventData>();
        [SerializeField] private List<EventData> meritEvents = new List<EventData>();

        [Header("参照")]
        [SerializeField] private EventDialogUI dialogUI;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            if (MonthTicker.I != null)
            {
                MonthTicker.I.OnMonthChanged += HandleMonthChanged;
            }
        }

        private void OnDisable()
        {
            if (MonthTicker.I != null)
            {
                MonthTicker.I.OnMonthChanged -= HandleMonthChanged;
            }
        }

        private void HandleMonthChanged(int year, int month)
        {
            Debug.Log($"[EventManager] 月変更を検知 ({year}歳 {month}月)。イベント判定を行います。");
            TriggerEventPair();
        }

        /// <summary>
        /// 手動または月変更イベントから呼ばれ、デメリット＋メリットのペアイベントを発生させる。
        /// </summary>
        public void TriggerEventPair()
        {
            EventData selectedDemerit = PickValidEvent(demeritEvents);
            EventData selectedMerit = PickValidEvent(meritEvents);

            if (selectedDemerit == null && selectedMerit == null)
            {
                Debug.Log("[EventManager] 発生可能なイベントがありませんでした。");
                return;
            }

            if (dialogUI == null)
            {
                dialogUI = FindFirstObjectByType<EventDialogUI>(FindObjectsInactive.Include);
            }

            if (dialogUI != null)
            {
                dialogUI.Show(selectedDemerit, selectedMerit);
            }
            else
            {
                Debug.LogError("[EventManager] EventDialogUI が見つかりません。");
            }
        }

        private EventData PickValidEvent(List<EventData> pool)
        {
            var validList = new List<EventData>();
            foreach (var evt in pool)
            {
                if (evt != null && evt.CheckConditions())
                {
                    validList.Add(evt);
                }
            }

            if (validList.Count == 0) return null;
            int randomIndex = Random.Range(0, validList.Count);
            return validList[randomIndex];
        }
    }
}
