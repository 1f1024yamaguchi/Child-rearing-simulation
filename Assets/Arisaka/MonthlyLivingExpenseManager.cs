using System;
using System.Collections.Generic;
using System.Text;
using Maruyama.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arisaka
{
    /// <summary>
    /// 月別生活支出マネージャー（有坂担当）。
    /// budget01.csv〜budget12.csv（東京都区部）の実データに基づき、
    /// 月（1〜12月）に応じた変動支出（光熱・水道、家具・家事用品、被服及び履物、保健医療、交通、通信、教養娯楽、諸雑費）を
    /// 各月の基準値から ±5% のランダムな含み（振れ幅）を持たせて毎月末（MonthTicker.OnMonthEnding）に引き落として家計簿に記録する。
    /// （※教育費は撤廃。既存の居住・家賃、保育、その他学校関連等は変更せず維持）
    /// </summary>
    public class MonthlyLivingExpenseManager : MonoBehaviour, ISaveParticipant
    {
        public static MonthlyLivingExpenseManager Instance { get; private set; }

        [Serializable]
        public class MonthlyExpenseDefinition
        {
            public string name;
            public ExpenseCategory category;
            public int[] monthlyCosts; // index 0 = 1月, ..., 11 = 12月

            public MonthlyExpenseDefinition(string name, ExpenseCategory category, int[] monthlyCosts)
            {
                this.name = name;
                this.category = category;
                this.monthlyCosts = monthlyCosts;
            }

            public int GetCostForMonth(int month)
            {
                if (monthlyCosts == null || monthlyCosts.Length == 0) return 0;
                int index = Mathf.Clamp(month - 1, 0, monthlyCosts.Length - 1);
                return monthlyCosts[index];
            }

            /// <summary>
            /// 基準値から ±varianceRate（既定 5%）の範囲内でランダムに変動した金額を返す
            /// </summary>
            public int GetCostWithVariance(int month, float varianceRate = 0.05f)
            {
                int baseCost = GetCostForMonth(month);
                if (baseCost <= 0) return 0;
                int minCost = Mathf.RoundToInt(baseCost * (1f - varianceRate));
                int maxCost = Mathf.RoundToInt(baseCost * (1f + varianceRate));
                return UnityEngine.Random.Range(minCost, maxCost + 1);
            }
        }

        // budget01.csv〜budget12.csv（東京都区部）に基づく全8項目のデータテーブル（教育費は撤廃）
        private static readonly List<MonthlyExpenseDefinition> ExpenseDefinitions = new List<MonthlyExpenseDefinition>
        {
            // 1. 光熱・水道（光熱費）
            new MonthlyExpenseDefinition("光熱・水道", ExpenseCategory.光熱費, new int[]
            {
                24394, 28971, 27836, 25870, 21028, 19656, 17951, 19446, 23680, 18854, 19523, 22037
            }),

            // 2. 家具・家事用品（独立項目）
            new MonthlyExpenseDefinition("家具・家事用品", ExpenseCategory.家具家事用品, new int[]
            {
                11972, 14650, 14111, 10126, 19450, 19926, 11486, 11693, 10665, 11336, 12361, 24217
            }),

            // 3. 被服及び履物（独立項目）
            new MonthlyExpenseDefinition("被服及び履物", ExpenseCategory.被服履物, new int[]
            {
                11332, 8770, 10988, 11715, 15127, 12828, 9518, 10925, 9993, 10647, 15585, 13923
            }),

            // 4. 保健医療（医療）
            new MonthlyExpenseDefinition("保健医療", ExpenseCategory.医療, new int[]
            {
                11487, 10730, 11397, 14335, 19722, 10528, 12091, 15582, 13765, 15126, 20551, 16788
            }),

            // 5. 交通（独立項目）
            new MonthlyExpenseDefinition("交通", ExpenseCategory.交通, new int[]
            {
                4318, 2849, 11001, 10454, 8195, 5811, 10629, 7299, 11218, 10331, 4022, 7699
            }),

            // 6. 通信（通信）
            new MonthlyExpenseDefinition("通信", ExpenseCategory.通信, new int[]
            {
                12611, 13864, 10836, 13379, 11497, 13987, 12632, 12535, 13373, 13065, 13135, 12374
            }),

            // 7. 教養娯楽（独立項目）
            new MonthlyExpenseDefinition("教養娯楽", ExpenseCategory.教養娯楽, new int[]
            {
                28139, 32818, 38339, 36584, 34737, 36269, 34356, 42371, 34825, 33868, 34877, 35667
            }),

            // 8. 諸雑費（独立項目）
            new MonthlyExpenseDefinition("諸雑費", ExpenseCategory.諸雑費, new int[]
            {
                23729, 24300, 28737, 30114, 25561, 25334, 35687, 29147, 30753, 23477, 33051, 32636
            })
        };

        [Header("実行状態")]
        [SerializeField] private string lastSettledKey = ""; // 年_月 のキー（二重引き落とし防止）

        public string SaveKey => "monthly_living_expenses";
        public int RestoreOrder => 36; // ExpenseManager (35) の直後

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

            SuppressDuplicateYamaguchiExpenses();
        }

        private void Start()
        {
            if (MonthTicker.I != null)
            {
                MonthTicker.I.OnMonthEnding += HandleMonthEnding;
            }

            SuppressDuplicateYamaguchiExpenses();
        }

        private void OnDestroy()
        {
            if (MonthTicker.I != null)
            {
                MonthTicker.I.OnMonthEnding -= HandleMonthEnding;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 既存の ExpenseManager から、旧固定値の電気代・ガス水道・通信費を除外する。
        /// （※居住・家賃、保育料・副食費、その他学校関連はそのまま残す）
        /// </summary>
        public void SuppressDuplicateYamaguchiExpenses()
        {
            if (ExpenseManager.I != null && ExpenseManager.I.Items != null)
            {
                int removed = ExpenseManager.I.Items.RemoveAll(item =>
                    item != null && (item.id == "electricity" || item.id == "gas_water" || item.id == "phone"));

                if (removed > 0)
                {
                    Debug.Log($"[MonthlyLivingExpenseManager] 既存の固定光熱費・通信費 ({removed} 件) を除外し、月別実データ支出へ置き換えました。");
                }
            }
        }

        /// <summary>
        /// 毎月末（MonthTicker.OnMonthEnding）に当月の支出を引き落とし、家計簿に記録する。
        /// </summary>
        private void HandleMonthEnding(int year, int month)
        {
            string key = $"{year}_{month}";
            if (lastSettledKey == key)
            {
                return; // すでに今月分は精算済み
            }
            lastSettledKey = key;

            // 二重引き落とし防止の再確認
            SuppressDuplicateYamaguchiExpenses();

            ExecuteMonthlyLivingExpenses(year, month);
        }

        /// <summary>
        /// 指定された月の変動支出8項目を引き落とす（各項目は基準値 ±5% のランダム変動）
        /// </summary>
        public void ExecuteMonthlyLivingExpenses(int year, int month)
        {
            int totalCost = 0;
            var sb = new StringBuilder();
            sb.AppendLine($"<color=#29D4F5>[MonthlyLivingExpenseManager]</color> {year}歳{month}月 生活変動支出引き落とし（budget{month:D2}.csv 東京都区部準拠・±5%ランダム変動）:");

            foreach (var def in ExpenseDefinitions)
            {
                int baseCost = def.GetCostForMonth(month);
                if (baseCost <= 0) continue;

                // 基準値から ±5% の含み（ランダム変動）を与えて実際の額を決定
                int cost = def.GetCostWithVariance(month, 0.05f);

                var effect = new Effect
                {
                    kind = EffectKind.Money,
                    target = EffectTarget.Household,
                    value = -cost,
                    duration = EffectDuration.Instant
                };

                var source = new LedgerSource(def.name, def.category);
                EffectApplier.Apply(new[] { effect }, source);

                totalCost += cost;
                int diff = cost - baseCost;
                string diffStr = diff >= 0 ? $"+{diff:N0}" : $"{diff:N0}";
                sb.AppendLine($"  - [{def.category}] {def.name}: -{cost:N0}円 (基準: {baseCost:N0}円 / 変動: {diffStr}円)");
            }

            sb.AppendLine($"  合計引き落とし額: -{totalCost:N0}円 → 所持金残高: {(GameState.I != null ? GameState.I.Money : 0):N0}円");
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// 指定した月の8項目の基準合計金額を取得する
        /// </summary>
        public static int GetTotalCostForMonth(int month)
        {
            int sum = 0;
            foreach (var def in ExpenseDefinitions)
            {
                sum += def.GetCostForMonth(month);
            }
            return sum;
        }

        /// <summary>
        /// 全8項目の定義リストを取得する
        /// </summary>
        public static IReadOnlyList<MonthlyExpenseDefinition> GetDefinitions() => ExpenseDefinitions;

        // ─── セーブ・ロード（ISaveParticipant）───────────────────

        [Serializable]
        private class LivingExpenseSaveData
        {
            public string lastSettledKey;
        }

        public string Capture()
        {
            var data = new LivingExpenseSaveData
            {
                lastSettledKey = lastSettledKey
            };
            return JsonUtility.ToJson(data);
        }

        public void Restore(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            var data = JsonUtility.FromJson<LivingExpenseSaveData>(json);
            if (data != null)
            {
                lastSettledKey = data.lastSettledKey ?? "";
            }
            SuppressDuplicateYamaguchiExpenses();
        }

        // ─── デバッグ確認用 ContextMenu ─────────────────────────

        [ContextMenu("Debug/今月の変動支出をログ（引かない）")]
        private void LogThisMonthLivingExpenses()
        {
            int y = 0, m = 1, d = 1;
            if (GameState.I != null) GameDate.FromAgeDays(GameState.I.AgeDays, out y, out m, out d);

            var sb = new StringBuilder();
            sb.AppendLine($"[MonthlyLivingExpenseManager] 今月の生活変動支出予定（{y}歳{m}月 / budget{m:D2}.csv 東京都区部）:");
            int total = 0;
            foreach (var def in ExpenseDefinitions)
            {
                int cost = def.GetCostForMonth(m);
                total += cost;
                sb.AppendLine($"  [{def.category}] {def.name}: {cost:N0}円");
            }
            sb.AppendLine($"  合計: {total:N0}円");
            Debug.Log(sb.ToString());
        }

        [ContextMenu("Debug/全12か月の支出一覧をログ")]
        private void LogAll12MonthsTable()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[MonthlyLivingExpenseManager] 全12か月の月別生活変動支出テーブル（budget01〜12.csv 東京都区部）:");
            for (int m = 1; m <= 12; m++)
            {
                int sum = GetTotalCostForMonth(m);
                sb.AppendLine($"  {m,2}月: 合計 {sum,7:N0}円");
            }
            Debug.Log(sb.ToString());
        }

        // ─── 自動常駐設定 ───────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            EnsureManagerExists();
            SceneManager.sceneLoaded += (scene, mode) => EnsureManagerExists();
        }

        private static void EnsureManagerExists()
        {
            if (Instance != null) return;
            if (FindFirstObjectByType<MonthlyLivingExpenseManager>() != null) return;

            var go = new GameObject("MonthlyLivingExpenseManager");
            go.AddComponent<MonthlyLivingExpenseManager>();
            DontDestroyOnLoad(go);
        }
    }
}

