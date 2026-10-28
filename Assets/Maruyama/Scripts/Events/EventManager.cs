using System.Collections.Generic;
using Maruyama.UI;
using UnityEngine;
using Arisaka; // MealGrade

namespace Maruyama.Events
{
    /// <summary>
    /// 病気のメリット・デメリットイベントを管理するマネージャー。
    ///
    /// ■ 動作方針
    /// - 月変更時（OnMonthChanged）ではなく、食事選択完了時（ScheduleForMeal）に
    ///   その月内のランダムな発生日を予約する。
    /// - DayTicker.OnDayPassed で日付を確認し、予約日が来たときに EventDialogUI を表示する。
    /// - 発生対象年齢は 0〜1歳のみ（2歳以降は予約・発生ともに行わない）。
    /// - イベント表示中は EventDialogUI.OnEnable → GameClock.PushPause で時間を停止する。
    /// - イベント表示中に同じイベントが重複して発生しないよう isDialogOpen フラグを管理する。
    ///
    /// ■ 食事グレードと月間発生回数
    /// - Cheap（安価）  : 月2回（異なる2日）
    /// - Normal（普通） : 月1回
    /// - Expensive（贅沢）: 月0回（スケジュールしない）
    /// </summary>
    public class EventManager : MonoBehaviour
    {
        public static EventManager Instance { get; private set; }

        [Header("登録イベント一覧")]
        [SerializeField] private List<EventData> demeritEvents = new List<EventData>();
        [SerializeField] private List<EventData> meritEvents = new List<EventData>();

        [Header("参照")]
        [SerializeField] private EventDialogUI dialogUI;

        [Header("年齢制限（病気イベント）")]
        [Tooltip("病気イベントを発生させる最大年齢（歳）。この値以下の年齢でのみ発生する。")]
        [SerializeField] private int maxAgeYearsForSickEvent = 1; // 0歳・1歳のみ

        // ---- 内部状態 ----

        /// <summary>現在の月にスケジュールされた発生日（通算日数）のリスト</summary>
        private List<int> scheduledAgeDays = new List<int>();

        /// <summary>現在月のキー（"年_月" 形式）。月切り替え検出に使う</summary>
        private string currentMonthKey = "";

        /// <summary>イベントダイアログが開いている間 true</summary>
        private bool isDialogOpen = false;

        private bool subscribed = false;

        // ---- Unity ライフサイクル ----

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            SubscribeTickers();
        }

        private void OnEnable()
        {
            SubscribeTickers();
        }

        private void OnDisable()
        {
            UnsubscribeTickers();
        }

        private void OnDestroy()
        {
            UnsubscribeTickers();
        }

        private void SubscribeTickers()
        {
            if (subscribed) return;
            if (DayTicker.I == null)
            {
                Debug.LogWarning("[EventManager] DayTicker が見つかりません。Start で再試行します。");
                return;
            }
            DayTicker.I.OnDayPassed += HandleDayPassed;
            subscribed = true;
        }

        private void UnsubscribeTickers()
        {
            if (!subscribed) return;
            if (DayTicker.I != null)
            {
                DayTicker.I.OnDayPassed -= HandleDayPassed;
            }
            subscribed = false;
        }

        // ---- スケジューリング API（食事選択完了時に MealSystemManager から呼ぶ）----

        /// <summary>
        /// 食事グレードに応じて、今月の病気イベント発生日をランダムに予約する。
        /// MealSystemManager の OnPlanSelected コールバック完了後に呼ぶこと。
        /// </summary>
        /// <param name="grade">選択された食事グレード</param>
        /// <param name="year">発生対象の年（歳）</param>
        /// <param name="month">発生対象の月</param>
        public void ScheduleForMeal(MealGrade grade, int year, int month)
        {
            // 2歳以降は予約しない
            if (year > maxAgeYearsForSickEvent)
            {
                Debug.Log($"[EventManager] {year}歳は対象年齢外のため、病気イベントをスケジュールしません。");
                return;
            }

            string monthKey = $"{year}_{month}";

            // 月の切り替わり時に前月の予約をリセット
            if (currentMonthKey != monthKey)
            {
                scheduledAgeDays.Clear();
                currentMonthKey = monthKey;
            }

            // 発生回数を決定
            int count;
            switch (grade)
            {
                case MealGrade.Cheap:     count = 2; break;
                case MealGrade.Normal:    count = 1; break;
                case MealGrade.Expensive: count = 0; break;
                default:                  count = 1; break;
            }

            if (count == 0)
            {
                Debug.Log($"[EventManager] {year}歳{month}月: 贅沢食事のため病気イベントなし。");
                return;
            }

            // 月の日数を取得してランダムな発生日を決定
            int daysInMonth = GameDate.DaysInMonth(month);
            var candidateDays = new List<int>();
            for (int d = 1; d <= daysInMonth; d++)
            {
                // 1日目（食事選択日）は除外して直後発生を防ぐ
                if (d > 1) candidateDays.Add(d);
            }

            // Shuffle して先頭 count 個を取る（Fisher-Yates）
            for (int i = candidateDays.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int tmp = candidateDays[i];
                candidateDays[i] = candidateDays[j];
                candidateDays[j] = tmp;
            }

            scheduledAgeDays.Clear();
            int take = Mathf.Min(count, candidateDays.Count);
            for (int i = 0; i < take; i++)
            {
                int ageDayForEvent = GameDate.ToAgeDays(year, month, candidateDays[i]);
                scheduledAgeDays.Add(ageDayForEvent);
            }
            scheduledAgeDays.Sort();

            var log = string.Join(", ", scheduledAgeDays.ConvertAll(d =>
            {
                GameDate.FromAgeDays(d, out int ey, out int em, out int ed);
                return $"{ey}歳{em}月{ed}日(day{d})";
            }));
            Debug.Log($"[EventManager] {year}歳{month}月 ({grade}) 病気イベント予約: [{log}]");
        }

        // ---- 日次チェック ----

        private void HandleDayPassed(int totalAgeDays)
        {
            // 予約リストにこの日があるか確認
            if (!scheduledAgeDays.Contains(totalAgeDays)) return;

            scheduledAgeDays.Remove(totalAgeDays);

            // 年齢チェック（0〜1歳のみ）
            GameDate.FromAgeDays(totalAgeDays, out int curYear, out _, out _);
            if (curYear > maxAgeYearsForSickEvent)
            {
                Debug.Log($"[EventManager] day{totalAgeDays}: {curYear}歳は対象外。予約キャンセル。");
                return;
            }

            // ダイアログが既に開いていれば発火しない（重複防止）
            if (isDialogOpen)
            {
                Debug.Log($"[EventManager] day{totalAgeDays}: イベントダイアログ表示中のため発火をスキップ。");
                return;
            }

            Debug.Log($"[EventManager] day{totalAgeDays}: 予約日到達。イベントを発火します。");
            TriggerEventPair();
        }

        // ---- イベント発火 ----

        [ContextMenu("Debug/今すぐイベントを出す")]
        private void DebugTriggerNow()
        {
            TriggerEventPair();
        }

        /// <summary>
        /// デメリット＋メリットのペアイベントを発生させる。
        /// </summary>
        public void TriggerEventPair()
        {
            if (isDialogOpen)
            {
                Debug.LogWarning("[EventManager] TriggerEventPair: 既にダイアログが開いています。無視します。");
                return;
            }

            EventData selectedDemerit = PickValidEvent(demeritEvents);
            EventData selectedMerit   = PickValidEvent(meritEvents);

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
                isDialogOpen = true;
                dialogUI.Show(selectedDemerit, selectedMerit, OnDialogClosed);
                Debug.Log("[EventManager] イベントダイアログを表示しました。");
            }
            else
            {
                Debug.LogError("[EventManager] EventDialogUI が見つかりません。");
            }
        }

        private void OnDialogClosed()
        {
            isDialogOpen = false;
            Debug.Log("[EventManager] イベントダイアログが閉じられました。");
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
