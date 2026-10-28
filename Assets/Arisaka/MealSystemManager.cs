using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Maruyama.Core;

namespace Arisaka
{
    /// <summary>
    /// 食事システム全体の管理マネージャー。
    /// 毎月1日に最新の資産額を表示した食事選択ダイアログを発生させ、
    /// 選択された食事プランの月額費用をその月の日数で日割りした金額を一日ごとに所持金から引き落とす。
    /// （docs/design/food-system.md 準拠）
    /// </summary>
    public class MealSystemManager : MonoBehaviour, ISaveParticipant
    {
        public static MealSystemManager Instance { get; private set; }

        [Header("現在の設定")]
        [SerializeField] private MealGrade currentGrade = MealGrade.Normal;
        private MealGrade? lastChosenGrade = null; // 初回は null（青枠なし）

        private string lastTriggeredMonthKey = "";
        private bool hasTriggeredInitial = false;

        // 日割り食費の管理フィールド
        private int lastDeductedAgeDay = -1;  // 最後に日割り食費を引き落とした通算日数
        private int currentMonthKey = -1;     // 年*100 + 月
        private int paidThisMonth = 0;        // 今月引き落とした累計食費額

        private bool selectionPending = false; // 今月の食事画面を出したが、まだ選ばれていない

        private List<MealPlan> plans;

        public MealGrade CurrentGrade => currentGrade;
        public MealGrade? LastChosenGrade => lastChosenGrade;

        public string SaveKey => "meal";
        public int RestoreOrder => 100;          // 山口の分（10〜40）より後

        private void OnEnable()  => SaveSystem.Register(this);
        private void OnDisable() => SaveSystem.Unregister(this);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            plans = MealPlan.CreateDefaultPlans();
        }

        private void Start()
        {
            // MonthTicker の月変更イベントを購読
            if (MonthTicker.I != null)
            {
                MonthTicker.I.OnMonthChanged += OnMonthChanged;
            }

            // DayTicker の日経過イベントを購読
            if (DayTicker.I != null)
            {
                DayTicker.I.OnDayPassed += OnDayPassed;
            }

            // ゲーム開始時（初月）の食事選択チェック
            CheckAndTriggerInitialDialog();
        }

        private void OnDestroy()
        {
            if (MonthTicker.I != null)
            {
                MonthTicker.I.OnMonthChanged -= OnMonthChanged;
            }

            if (DayTicker.I != null)
            {
                DayTicker.I.OnDayPassed -= OnDayPassed;
            }
        }

        /// <summary>
        /// 初回（ゲーム開始時・0歳1月1日）の食事選択ダイアログ表示
        /// </summary>
        private void CheckAndTriggerInitialDialog()
        {
            if (hasTriggeredInitial) return;

            if (GameState.I != null)
            {
                GameDate.FromAgeDays(GameState.I.AgeDays, out int y, out int m, out int d);
                // 1日目かつ初月（0歳1月）であれば初回の食事選択を表示
                if (d == 1 && y == 0 && m == 1)
                {
                    hasTriggeredInitial = true;
                    lastTriggeredMonthKey = $"{y}_{m}";
                    selectionPending = true;
                    StartCoroutine(ProcessMonthlyRoutine(y, m, isInitial: true));
                }
            }
        }

        /// <summary>
        /// MonthTicker からの月変更通知（毎月1日）
        /// </summary>
        private void OnMonthChanged(int year, int month)
        {
            TriggerMonthlyMealProcess(year, month);
        }

        /// <summary>
        /// DayTicker からの日経過通知（毎日呼ばれる）
        /// </summary>
        private void OnDayPassed(int totalDays)
        {
            // 1. 前日（過ごし終えた日）の日割り食費を引き落とす
            // 1日目に使った食費は2日目（totalDays = 1）になった瞬間に引かれて2日目に表示され、
            // 月末（例えば31日）に使った食費は次の月の1日目（totalDays = 31）になった瞬間に引かれる。
            int consumedAgeDay = totalDays - 1;
            if (consumedAgeDay >= 0)
            {
                GameDate.FromAgeDays(consumedAgeDay, out int cYear, out int cMonth, out int cDay);
                DeductDailyMealCost(cYear, cMonth, cDay, consumedAgeDay);
            }

            // 2. 迎えた新しい日が「1日」なら、新しい月の食事選択ダイアログをトリガーする
            GameDate.FromAgeDays(totalDays, out int nextYear, out int nextMonth, out int nextDay);
            if (nextDay == 1)
            {
                TriggerMonthlyMealProcess(nextYear, nextMonth);
            }
        }

        /// <summary>
        /// 毎月1日の月次食事処理をトリガーする
        /// </summary>
        public void TriggerMonthlyMealProcess(int year, int month)
        {
            string monthKey = $"{year}_{month}";
            if (lastTriggeredMonthKey == monthKey)
            {
                // すでに今月分の処理を行っていればスキップ
                return;
            }

            lastTriggeredMonthKey = monthKey;
            selectionPending = true;
            StartCoroutine(ProcessMonthlyRoutine(year, month, isInitial: false));
        }

        /// <summary>
        /// 月次コルーチン：
        /// 1. 同一フレーム内の他の月次イベント（給料日 HouseholdIncome.Payday 等）の処理完了を1フレーム待つ
        /// 2. 最新資産額で食事選択ダイアログを表示
        /// （ダイアログ自体が OnEnable / OnDisable で PushPause / PopPause を行います）
        /// </summary>
        private IEnumerator ProcessMonthlyRoutine(int year, int month, bool isInitial)
        {
            yield return null;

            // 最新資産額で食事選択ダイアログを表示
            // （初回は null を渡して青枠なし、2回目以降は前回選択したグレードを渡して薄い青枠と「先月に選択」を表示）
            MealSelectionDialogUI.ShowDialog(isInitial ? null : lastChosenGrade, OnPlanSelected);
        }

        /// <summary>
        /// 前日（過ごし終えた日: year, month, day）の食費日割り額を所持金から引き落とす。
        /// 月末に合計が月額費用と完全に一致するよう累積差分で計算する。
        /// </summary>
        public void DeductDailyMealCost(int year, int month, int day, int ageDay)
        {
            if (lastDeductedAgeDay == ageDay) return; // 本日分は既に引き落とし済み

            var plan = GetCurrentPlan();
            if (plan == null) return;

            int daysInMonth = GameDate.DaysInMonth(month);

            // 月が替わった場合は累積支払額をリセット
            int monthKey = year * 100 + month;
            if (currentMonthKey != monthKey)
            {
                currentMonthKey = monthKey;
                paidThisMonth = 0;
            }

            // day日目終了時点での本来の目標累計支払額
            int targetTotalSoFar = Mathf.RoundToInt((float)plan.costPerMonth * day / daysInMonth);
            int dailyCost = targetTotalSoFar - paidThisMonth;
            if (dailyCost <= 0) dailyCost = 0;

            paidThisMonth += dailyCost;
            lastDeductedAgeDay = ageDay;

            var costEffect = plan.CreateDailyCostEffect(dailyCost);
            EffectApplier.Apply(new[] { costEffect }, new LedgerSource($"食費（{plan.title}）", ExpenseCategory.食費));

            Debug.Log($"[MealSystemManager] {year}歳 {month}月{day}日分食費引き落とし: {dailyCost:N0}円 (当月累計: {paidThisMonth:N0}/{plan.costPerMonth:N0}円, 所持金残高: {GameState.I.Money:N0}円)");
        }

        private void OnPlanSelected(MealPlan plan)
        {
            selectionPending = false;
            currentGrade = plan.grade;
            lastChosenGrade = plan.grade;
            Debug.Log($"[MealSystemManager] 今月の食事プランが '{plan.title}' に決定されました。（費用はその月の日数で日割りされ、翌日以降に一日ごとに引き落とされます）");
        }

        private MealPlan GetCurrentPlan()
        {
            if (plans == null || plans.Count == 0)
            {
                plans = MealPlan.CreateDefaultPlans();
            }

            return plans.Find(p => p.grade == currentGrade) ?? plans[1]; // 既定は「普通」
        }

        // ─── セーブ・ロード（ISaveParticipant）───────────────────

        [System.Serializable]
        private class MealSaveData
        {
            public int    currentGrade;
            public int    lastChosenGrade = -1;      // -1 = null（まだ一度も選んでいない）
            public string lastTriggeredMonthKey = "";
            public bool   hasTriggeredInitial;
            public int    lastDeductedAgeDay = -1;
            public int    currentMonthKey = -1;
            public int    paidThisMonth;
            public bool   selectionPending;
        }

        public string Capture()
        {
            var d = new MealSaveData
            {
                currentGrade          = (int)currentGrade,
                lastChosenGrade       = lastChosenGrade.HasValue ? (int)lastChosenGrade.Value : -1,
                lastTriggeredMonthKey = lastTriggeredMonthKey,
                hasTriggeredInitial   = hasTriggeredInitial,
                lastDeductedAgeDay    = lastDeductedAgeDay,
                currentMonthKey       = currentMonthKey,
                paidThisMonth         = paidThisMonth,
                selectionPending      = selectionPending
            };
            return JsonUtility.ToJson(d);
        }

        public void Restore(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            var d = JsonUtility.FromJson<MealSaveData>(json);
            if (d == null) return;

            // ① Play 直後に予約・表示された食事画面を止めて閉じる（閉じると時間停止も解除される）
            StopAllCoroutines();
            var open = FindObjectOfType<MealSelectionDialogUI>();   // 表示中のものだけ見つかる
            if (open != null) open.gameObject.SetActive(false);

            // ② 値を戻す
            currentGrade          = (MealGrade)d.currentGrade;
            lastChosenGrade       = d.lastChosenGrade >= 0 ? (MealGrade?)d.lastChosenGrade : null;
            lastTriggeredMonthKey = d.lastTriggeredMonthKey ?? "";
            hasTriggeredInitial   = d.hasTriggeredInitial;
            lastDeductedAgeDay    = d.lastDeductedAgeDay;
            currentMonthKey       = d.currentMonthKey;
            paidThisMonth         = d.paidThisMonth;
            selectionPending      = d.selectionPending;

            // ③ 今月の選択がまだなら、画面を出し直す（3章の決定）
            if (selectionPending && GameState.I != null)
            {
                GameDate.FromAgeDays(GameState.I.AgeDays, out int y, out int m, out int _);
                StartCoroutine(ProcessMonthlyRoutine(y, m, isInitial: false));   // lastChosenGrade が null なら青枠なし
            }
        }

        // ---------------- 自動常駐（どのシーンでも動作するよう設定） ----------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            EnsureManagerExists();
            SceneManager.sceneLoaded += (scene, mode) => EnsureManagerExists();
        }

        private static void EnsureManagerExists()
        {
            if (Instance != null) return;
            if (FindObjectOfType<MealSystemManager>() != null) return;

            var go = new GameObject("MealSystemManager");
            go.AddComponent<MealSystemManager>();
        }
    }
}
