using System.Collections.Generic;
using Maruyama.Core;
using Maruyama.UI;
using UnityEngine;
using Arisaka; // MealGrade

namespace Maruyama.Events
{
    /// <summary>
    /// メリット・デメリットイベントを管理するマネージャー。
    ///
    /// ■ 動作方針（月ごとの抽選）
    /// - 食事選択完了時（ScheduleForMeal）に、その月の抽選を行う。
    /// - 悪いイベント（Demerit）と良いイベント（Merit）は **別々に** 抽選する。
    ///   どちらか片方だけ起きる月も、何も起きない月もある。
    /// - 当たった分だけ、その月の中のランダムな日に予約する。予約日はすべて別の日にする。
    /// - 予約日が来たら、その日の時点の条件（年齢・ストレスなど）で出せるイベントを選んで表示する。
    ///   表示するのは当たった側のカードだけ。
    ///
    /// ■ 当たる確率（％）
    ///   (基本確率 ＋ EventRateModifiers.TotalPercent) × ストレス段階の倍率   を 0〜上限 に収めたもの。
    /// - 食事の補正（MealPlan の BadEventRate / GoodEventRate）はここに足される。
    /// - ストレス段階は、家族のうち一番ストレスが高い人の段階を使う。
    /// - 数値はすべてたたき台。Inspector で調整する。
    ///
    /// ■ 固定イベント（trigger = Fixed）
    /// - 抽選とは別に、決まった月日に必ず起きる（誕生日・初めての言葉・お年玉など）。
    /// - 日付（月・日）と満年齢の範囲はイベント側（EventData）が持つ。範囲の毎年、または1回だけ。
    /// - 起きたかどうかは「イベントid@歳」で記録し、セーブする。ロードしても二重に起きない。
    ///
    /// ■ ダイアログの順番待ち
    /// - 同じ日に複数のイベントが重なったら、悪い1・良い1を1組にして順番に出す（捨てない）。
    /// - ダイアログが開いている間に予約日が来たものも、閉じたあとに出す。
    ///
    /// ■ セーブ（ISaveParticipant）
    /// - 固定イベントの記録、今月の抽選済みの印と予約、直前に出したイベント、発生率の補正を保存する。
    ///
    /// ■ その他
    /// - イベント表示中は EventDialogUI.OnEnable → GameClock.PushPause で時間を停止する。
    /// - 同じ種類のイベントが続けて出ないよう、直前に出したイベントは他に候補があれば除く（抽選のみ）。
    /// </summary>
    public class EventManager : MonoBehaviour, ISaveParticipant
    {
        public static EventManager Instance { get; private set; }

        [Header("登録イベント一覧（シーンに直接登録したもの）")]
        [SerializeField] private List<EventData> demeritEvents = new List<EventData>();
        [SerializeField] private List<EventData> meritEvents = new List<EventData>();

        [Header("Resources から自動で読み込む")]
        [Tooltip("ON のとき、Resources/<下のパス> にある EventData を category に従って候補へ足す（上の一覧と重複したものは1つにまとめる）。イベントを増やすときはこのフォルダにアセットを置くだけでよい。")]
        [SerializeField] private bool autoLoadFromResources = true;
        [SerializeField] private string resourcesPath = "Events";

        [Header("参照")]
        [SerializeField] private EventDialogUI dialogUI;

        [Header("月ごとの抽選（たたき台）")]
        [Tooltip("悪いイベントを抽選する回数の上限（1か月あたり）。1回ごとに独立して当たり外れを決める。")]
        [Min(0)] [SerializeField] private int maxDemeritPerMonth = 2;
        [Tooltip("悪いイベントの基本確率（％。1回の抽選あたり）。")]
        [Range(0f, 100f)] [SerializeField] private float baseDemeritChancePercent = 35f;
        [Tooltip("良いイベントを抽選する回数の上限（1か月あたり）。")]
        [Min(0)] [SerializeField] private int maxMeritPerMonth = 1;
        [Tooltip("良いイベントの基本確率（％。1回の抽選あたり）。")]
        [Range(0f, 100f)] [SerializeField] private float baseMeritChancePercent = 40f;
        [Tooltip("確率の上限（％）。どれだけ補正が重なっても、これを超えない。")]
        [Range(0f, 100f)] [SerializeField] private float maxChancePercent = 90f;

        [Header("ストレス段階ごとの倍率（家族で一番ストレスが高い人の段階。良好／普通／危険／限界の順）")]
        [Tooltip("悪いイベントの確率にかける倍率。ストレスが高いほど悪いことが起きやすい。")]
        [SerializeField] private float[] demeritStressMultiplier = { 0.7f, 1.0f, 1.5f, 2.0f };
        [Tooltip("良いイベントの確率にかける倍率。既定はすべて 1（ストレスで減らさない）。ストレスが高いときの救済は、イベント側の発生条件（StressLevelCondition）で出す。")]
        [SerializeField] private float[] meritStressMultiplier = { 1.0f, 1.0f, 1.0f, 1.0f };

        // ---- 内部状態 ----

        private struct Slot
        {
            public int ageDay;               // 発生日（通算日数）
            public EventCategory category;   // 悪い／良い
        }

        /// <summary>今月に予約された発生枠</summary>
        private readonly List<Slot> scheduledSlots = new List<Slot>();

        /// <summary>抽選済みの月（"年_月"）。同じ月に2回抽選しないために使う</summary>
        private string lastRolledMonthKey = "";

        private string lastDemeritId = "";
        private string lastMeritId = "";

        /// <summary>クールダウンのあるイベントが最後に出た日（通算日数）。id → 日</summary>
        private readonly Dictionary<string, int> lastFiredDay = new Dictionary<string, int>();

        private readonly List<EventData> demeritPool = new List<EventData>();
        private readonly List<EventData> meritPool = new List<EventData>();
        private readonly List<EventData> fixedPool = new List<EventData>();

        /// <summary>起きた固定イベントの記録（"イベントid@歳"）</summary>
        private readonly HashSet<string> firedFixed = new HashSet<string>();

        private struct DialogRequest
        {
            public EventData demerit;
            public EventData merit;
        }

        /// <summary>出番待ちのダイアログ</summary>
        private readonly Queue<DialogRequest> pendingDialogs = new Queue<DialogRequest>();

        // その日に出すイベントの集め場所（毎日作り直さず使い回す）
        private readonly List<EventData> todayDemerits = new List<EventData>();
        private readonly List<EventData> todayMerits = new List<EventData>();

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
            BuildPools();
        }

        private void Start()
        {
            SubscribeTickers();
        }

        private void OnEnable()
        {
            SubscribeTickers();
            SaveSystem.Register(this);
        }

        private void OnDisable()
        {
            UnsubscribeTickers();
            SaveSystem.Unregister(this);
        }

        private void OnDestroy()
        {
            UnsubscribeTickers();
            if (Instance == this) Instance = null;
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

        // ---- 候補プール ----

        /// <summary>シーン登録分と Resources 分をまとめて、悪い／良いの候補リストを作る。</summary>
        private void BuildPools()
        {
            demeritPool.Clear();
            meritPool.Clear();
            fixedPool.Clear();

            foreach (var e in demeritEvents) AddToPool(e);
            foreach (var e in meritEvents) AddToPool(e);

            if (autoLoadFromResources)
            {
                var loaded = Resources.LoadAll<EventData>(resourcesPath);
                foreach (var e in loaded) AddToPool(e);
            }

            Debug.Log($"[EventManager] 候補イベント: デメリット {demeritPool.Count} 件 / メリット {meritPool.Count} 件 / 固定 {fixedPool.Count} 件");
        }

        /// <summary>固定イベントは固定用に、それ以外は category に従って振り分ける。同じアセットは1回だけ入れる。</summary>
        private void AddToPool(EventData e)
        {
            if (e == null) return;
            var pool = e.IsFixed ? fixedPool : (e.category == EventCategory.Demerit ? demeritPool : meritPool);
            if (!pool.Contains(e)) pool.Add(e);
        }

        // ---- 月ごとの抽選（食事選択完了時に MealSystemManager から呼ぶ）----

        /// <summary>
        /// その月の悪い／良いイベントを別々に抽選し、当たった分の発生日を予約する。
        /// 食事の補正（BadEventRate / GoodEventRate）は、食事選択時に EffectApplier が
        /// EventRateModifiers に登録済みなので、ここで自動的に反映される。
        /// MealSystemManager の OnPlanSelected から呼ぶこと。
        /// </summary>
        /// <param name="grade">選択された食事グレード（ログ用。確率は補正の合計から決まる）</param>
        /// <param name="year">対象の年（歳）</param>
        /// <param name="month">対象の月</param>
        public void ScheduleForMeal(MealGrade grade, int year, int month)
        {
            string monthKey = $"{year}_{month}";
            if (lastRolledMonthKey == monthKey)
            {
                Debug.Log($"[EventManager] {year}歳{month}月はすでに抽選済みのため、何もしません。");
                return;
            }
            lastRolledMonthKey = monthKey;
            scheduledSlots.Clear();

            // 確率の元になるストレス段階（家族で一番高い人）
            StressLevel level = WorstStressLevel(out string who);

            float demeritChance = ChancePercent(baseDemeritChancePercent, EffectKind.BadEventRate, demeritStressMultiplier, level);
            float meritChance   = ChancePercent(baseMeritChancePercent,   EffectKind.GoodEventRate, meritStressMultiplier,   level);

            int demeritHits = RollHits(maxDemeritPerMonth, demeritChance);
            int meritHits   = RollHits(maxMeritPerMonth,   meritChance);

            // 予約できる日：今日より後の日（月初に選んだなら2日目以降）
            var candidateDays = new List<int>();
            int today = 1;
            if (GameState.I != null)
            {
                GameDate.FromAgeDays(GameState.I.AgeDays, out _, out _, out int d);
                today = d;
            }
            int daysInMonth = GameDate.DaysInMonth(month);
            for (int d = Mathf.Max(2, today + 1); d <= daysInMonth; d++) candidateDays.Add(d);
            Shuffle(candidateDays);

            // すべて別の日に予約する（同じ日に2つ重ねない）。日数が足りなければ余った分は諦める
            int slotIndex = 0;
            for (int i = 0; i < demeritHits && slotIndex < candidateDays.Count; i++, slotIndex++)
            {
                scheduledSlots.Add(new Slot { ageDay = GameDate.ToAgeDays(year, month, candidateDays[slotIndex]), category = EventCategory.Demerit });
            }
            for (int i = 0; i < meritHits && slotIndex < candidateDays.Count; i++, slotIndex++)
            {
                scheduledSlots.Add(new Slot { ageDay = GameDate.ToAgeDays(year, month, candidateDays[slotIndex]), category = EventCategory.Merit });
            }

            string slotLog = scheduledSlots.Count == 0 ? "なし" : string.Join(", ", scheduledSlots.ConvertAll(s =>
            {
                GameDate.FromAgeDays(s.ageDay, out _, out int em, out int ed);
                return $"{em}月{ed}日({(s.category == EventCategory.Demerit ? "悪" : "良")})";
            }));
            Debug.Log($"[EventManager] {year}歳{month}月 抽選 食事={grade} / ストレス最大={who}({level}) / " +
                      $"悪 {demeritChance:0.#}%×最大{maxDemeritPerMonth}回→{demeritHits}件 / 良 {meritChance:0.#}%×最大{maxMeritPerMonth}回→{meritHits}件 / 予約: {slotLog}");
        }

        /// <summary>
        /// 1回の抽選で当たる確率（％）。
        /// (基本 ＋ 補正の合計) × ストレス段階の倍率 を 0〜上限 に収める。
        /// </summary>
        private float ChancePercent(float basePercent, EffectKind rateKind, float[] stressMultiplier, StressLevel level)
        {
            float modifier = EventRateModifiers.TotalPercent(rateKind);
            float mul = MultiplierFor(stressMultiplier, level);
            return Mathf.Clamp((basePercent + modifier) * mul, 0f, maxChancePercent);
        }

        private static float MultiplierFor(float[] table, StressLevel level)
        {
            int i = (int)level;
            if (table == null || i < 0 || i >= table.Length) return 1f;
            return Mathf.Max(0f, table[i]);
        }

        /// <summary>maxRolls 回、それぞれ chancePercent の確率で当たり外れを決め、当たった数を返す。</summary>
        private static int RollHits(int maxRolls, float chancePercent)
        {
            int hits = 0;
            for (int i = 0; i < maxRolls; i++)
            {
                if (Random.value * 100f < chancePercent) hits++;
            }
            return hits;
        }

        /// <summary>家族のうち一番ストレスが高い人の段階。世帯が無いときは「普通」。</summary>
        private static StressLevel WorstStressLevel(out string who)
        {
            who = "不明";
            if (Household.I == null) return StressLevel.普通;

            PersonStatus worst = null;
            foreach (var m in Household.I.Members)
            {
                if (m == null) continue;
                if (worst == null || m.Stress > worst.Stress) worst = m;
            }
            if (worst == null) return StressLevel.普通;

            who = $"{worst.DisplayName}:{worst.Stress}";
            return worst.StressLevel;
        }

        private static void Shuffle(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        // ---- 日次チェック ----

        private void HandleDayPassed(int totalAgeDays)
        {
            todayDemerits.Clear();
            todayMerits.Clear();

            // 1. 月ごとの抽選で、この日に予約された枠
            bool demeritDue = false, meritDue = false;
            for (int i = scheduledSlots.Count - 1; i >= 0; i--)
            {
                if (scheduledSlots[i].ageDay != totalAgeDays) continue;
                if (scheduledSlots[i].category == EventCategory.Demerit) demeritDue = true; else meritDue = true;
                scheduledSlots.RemoveAt(i);
            }
            if (demeritDue)
            {
                var e = PickValidEvent(demeritPool, ref lastDemeritId);
                if (e != null) todayDemerits.Add(e);
            }
            if (meritDue)
            {
                var e = PickValidEvent(meritPool, ref lastMeritId);
                if (e != null) todayMerits.Add(e);
            }

            // 2. 固定イベント（この日が月日・年齢の範囲に合うもの）
            CollectFixedEvents(totalAgeDays);

            if (todayDemerits.Count == 0 && todayMerits.Count == 0)
            {
                if (demeritDue || meritDue)
                {
                    Debug.Log($"[EventManager] day{totalAgeDays}: 予約日到達。ただし条件を満たすイベントがありませんでした。");
                }
                return;
            }

            EnqueueDialogs(todayDemerits, todayMerits);
        }

        /// <summary>
        /// 固定イベントのうち、今日が発生日で、まだ起きていないものを今日の一覧へ足す。
        /// 起きた記録（イベントid@歳）はここでつける。
        /// </summary>
        private void CollectFixedEvents(int totalAgeDays)
        {
            if (fixedPool.Count == 0) return;
            GameDate.FromAgeDays(totalAgeDays, out int year, out int month, out int day);

            foreach (var e in fixedPool)
            {
                if (e == null) continue;
                if (e.fixedMonth != month || e.fixedDay != day) continue;

                int lo = Mathf.Min(e.fixedMinAgeYears, e.fixedMaxAgeYears);
                int hi = Mathf.Max(e.fixedMinAgeYears, e.fixedMaxAgeYears);
                if (year < lo || year > hi) continue;

                string key = $"{e.id}@{year}";
                if (firedFixed.Contains(key)) continue;
                if (!e.CheckConditions()) continue;

                firedFixed.Add(key);
                (e.category == EventCategory.Demerit ? todayDemerits : todayMerits).Add(e);
                Debug.Log($"[EventManager] day{totalAgeDays}: 固定イベント '{e.id}'（{year}歳{month}月{day}日）");
            }
        }

        // ---- ダイアログの順番待ち ----

        /// <summary>
        /// 悪い1・良い1を1組にして順番待ちに入れ、開けるなら先頭を出す。
        /// 片方が余ったら、余った側だけのダイアログにする。
        /// </summary>
        private void EnqueueDialogs(List<EventData> demerits, List<EventData> merits)
        {
            int n = Mathf.Max(demerits.Count, merits.Count);
            for (int i = 0; i < n; i++)
            {
                pendingDialogs.Enqueue(new DialogRequest
                {
                    demerit = i < demerits.Count ? demerits[i] : null,
                    merit = i < merits.Count ? merits[i] : null,
                });
            }
            TryShowNext();
        }

        private void TryShowNext()
        {
            if (isDialogOpen || pendingDialogs.Count == 0) return;
            var req = pendingDialogs.Dequeue();
            ShowDialog(req.demerit, req.merit);
        }

        private System.Collections.IEnumerator ShowNextNextFrame()
        {
            // ダイアログを閉じる処理が終わってから、次を出す
            yield return null;
            TryShowNext();
        }

        // ---- イベント発火 ----

        [ContextMenu("Debug/今すぐイベントを出す（悪い＋良い）")]
        private void DebugTriggerNow()
        {
            TriggerEventPair();
        }

        [ContextMenu("Debug/今すぐ悪いイベントだけ出す")]
        private void DebugTriggerDemerit()
        {
            if (isDialogOpen) return;
            ShowDialog(PickValidEvent(demeritPool, ref lastDemeritId), null);
        }

        [ContextMenu("Debug/今すぐ良いイベントだけ出す")]
        private void DebugTriggerMerit()
        {
            if (isDialogOpen) return;
            ShowDialog(null, PickValidEvent(meritPool, ref lastMeritId));
        }

        /// <summary>
        /// デメリット＋メリットのペアを強制的に出す（テスト・デバッグ用）。
        /// 通常の発生（月ごとの抽選）では使わない。
        /// </summary>
        public void TriggerEventPair()
        {
            if (isDialogOpen)
            {
                Debug.LogWarning("[EventManager] TriggerEventPair: 既にダイアログが開いています。無視します。");
                return;
            }

            ShowDialog(PickValidEvent(demeritPool, ref lastDemeritId), PickValidEvent(meritPool, ref lastMeritId));
        }

        private void ShowDialog(EventData demerit, EventData merit)
        {
            if (demerit == null && merit == null)
            {
                Debug.Log("[EventManager] 発生可能なイベントがありませんでした。");
                return;
            }

            if (dialogUI == null)
            {
                dialogUI = FindFirstObjectByType<EventDialogUI>(FindObjectsInactive.Include);
            }

            if (dialogUI == null)
            {
                Debug.LogError("[EventManager] EventDialogUI が見つかりません。");
                return;
            }

            isDialogOpen = true;
            dialogUI.Show(demerit, merit, OnDialogClosed);
            Debug.Log("[EventManager] イベントダイアログを表示しました。");
        }

        private void OnDialogClosed()
        {
            isDialogOpen = false;
            Debug.Log("[EventManager] イベントダイアログが閉じられました。");

            if (pendingDialogs.Count > 0 && isActiveAndEnabled)
            {
                StartCoroutine(ShowNextNextFrame());
            }
        }

        // ---- 選ぶ ----

        /// <summary>
        /// 条件を満たすイベントの中から、weight に比例して1つ選ぶ。
        /// 直前に出したイベントは、他に候補があれば除く。
        /// </summary>
        private EventData PickValidEvent(List<EventData> pool, ref string lastId)
        {
            var valid = new List<EventData>();
            foreach (var evt in pool)
            {
                if (evt != null && evt.weight > 0f && evt.CheckConditions() && !IsCoolingDown(evt)) valid.Add(evt);
            }
            if (valid.Count == 0) return null;

            if (valid.Count > 1)
            {
                string last = lastId;
                valid.RemoveAll(e => e.id == last);
            }

            float total = 0f;
            foreach (var e in valid) total += e.weight;

            float r = Random.value * total;
            EventData picked = valid[valid.Count - 1];
            foreach (var e in valid)
            {
                if (r < e.weight) { picked = e; break; }
                r -= e.weight;
            }

            lastId = picked.id;
            if (picked.cooldownDays > 0 && GameState.I != null) lastFiredDay[picked.id] = (int)GameState.I.AgeDays;
            return picked;
        }

        /// <summary>クールダウン中（前回出てからの日数が cooldownDays に届いていない）か。</summary>
        private bool IsCoolingDown(EventData evt)
        {
            if (evt.cooldownDays <= 0 || GameState.I == null) return false;
            if (!lastFiredDay.TryGetValue(evt.id, out int last)) return false;
            return (int)GameState.I.AgeDays - last < evt.cooldownDays;
        }

        // ---- セーブ・ロード（ISaveParticipant）----

        [System.Serializable]
        private class SlotSave
        {
            public int ageDay;
            public int category;   // EventCategory の数値
        }

        [System.Serializable]
        private class EventSaveData
        {
            public List<string> firedFixed = new List<string>();
            public string lastRolledMonthKey = "";
            public List<SlotSave> slots = new List<SlotSave>();
            public string lastDemeritId = "";
            public string lastMeritId = "";
            public List<string> cooldowns = new List<string>();   // "イベントid@最後に出た日"
            public List<EventRateModifiers.SavedEntry> rates = new List<EventRateModifiers.SavedEntry>();
        }

        public string SaveKey => "event";

        /// <summary>他の人の担当分は 100〜。丸山のイベントは 110（家族・制度・食事が戻ったあと）。</summary>
        public int RestoreOrder => 110;

        public string Capture()
        {
            var d = new EventSaveData
            {
                firedFixed = new List<string>(firedFixed),
                lastRolledMonthKey = lastRolledMonthKey,
                lastDemeritId = lastDemeritId,
                lastMeritId = lastMeritId,
                rates = EventRateModifiers.Export(),
            };
            foreach (var kv in lastFiredDay) d.cooldowns.Add($"{kv.Key}@{kv.Value}");
            foreach (var slot in scheduledSlots)
            {
                d.slots.Add(new SlotSave { ageDay = slot.ageDay, category = (int)slot.category });
            }
            return JsonUtility.ToJson(d);
        }

        public void Restore(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            var d = JsonUtility.FromJson<EventSaveData>(json);
            if (d == null) return;

            firedFixed.Clear();
            if (d.firedFixed != null) foreach (var k in d.firedFixed) firedFixed.Add(k);

            lastRolledMonthKey = d.lastRolledMonthKey ?? "";
            lastDemeritId = d.lastDemeritId ?? "";
            lastMeritId = d.lastMeritId ?? "";

            lastFiredDay.Clear();
            if (d.cooldowns != null)
            {
                foreach (var c in d.cooldowns)
                {
                    int at = c.LastIndexOf('@');
                    if (at > 0 && int.TryParse(c.Substring(at + 1), out int day)) lastFiredDay[c.Substring(0, at)] = day;
                }
            }

            scheduledSlots.Clear();
            if (d.slots != null)
            {
                foreach (var s in d.slots)
                {
                    scheduledSlots.Add(new Slot { ageDay = s.ageDay, category = (EventCategory)s.category });
                }
            }

            EventRateModifiers.Import(d.rates);

            // ロード前に溜まっていた出番待ちは捨てる（時間が止まっている間に戻す）
            pendingDialogs.Clear();
        }
    }
}
