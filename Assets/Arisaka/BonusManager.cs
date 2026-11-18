using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Maruyama.Core;

namespace Arisaka
{
    /// <summary>
    /// 賞与1人分の支給明細データ。
    /// </summary>
    [Serializable]
    public struct BonusSlip
    {
        public ParentRole role;
        public int previousSalary;       // 参照した給与実績（5月または11月の額面）
        public int socialInsurance;      // 給料にかかる社会保険料
        public int amount1;              // ① 給料 − 社会保険料
        public int dependentsCount;      // 扶養親族等の数（0人 または 1人）
        public float taxRate;            // 算出率（源泉徴収税率）
        public int tax;                  // 源泉徴収税額 (① × 税率)
        public int netBonus;             // 手取り賞与額 (① − 税額)
        public int baseSalaryMonth;      // 参照した給与の月（5 または 11）
    }

    /// <summary>
    /// 賞与（ボーナス）支給管理マネージャー。
    /// 毎年6月と12月に情報を取得・賞与額を算定し、翌月の7月と1月にそれぞれ振込・家計簿記録・通知を行う。
    /// 
    /// 【給与実績の参照仕様】
    /// - 夏季賞与（6月算定 / 7月振込）：必ず【5月】の給与実績を参照して算定する。
    /// - 冬季賞与（12月算定 / 1月振込）：必ず【11月】の給与実績を参照して算定する。
    /// 
    /// 【進行スケジュール】
    /// - 6月1日：【5月】の給与実績、社保、扶養数から夏季賞与を計算。「7月支給予定」として保留。
    /// - 7月1日：保留された夏季賞与を実際に口座振込・家計簿記録・トースト通知。
    /// - 12月1日：【11月】の給与実績、社保、扶養数から冬季賞与を計算。「1月支給予定」として保留。
    /// - 1月1日：保留された冬季賞与を実際に口座振込・家計簿記録・トースト通知。
    /// 
    /// 【計算仕様（syouyo-zeiritu.md 準拠）】
    /// 1. 各親の該当月給料（5月または11月）から社会保険料を引いた金額（①）を算出。
    /// 2. 扶養親族等の数を判定：
    ///    - 子どもが16歳未満：0人
    ///    - 子どもが16歳以上：給与が多い親に 1人、他方の親に 0人
    /// 3. ①と扶養親族数から賞与に対する源泉徴収税率（甲欄）を導出。
    /// 4. 税額（① × 税率）を①から引いた手取り賞与額を確定する。
    /// </summary>
    public class BonusManager : MonoBehaviour, ISaveParticipant
    {
        public static BonusManager Instance { get; private set; }

        [Header("保留中・支給状態")]
        [SerializeField] private bool hasPendingBonus = false;
        [SerializeField] private int pendingBonusMonth = 0; // 6 (夏季) または 12 (冬季)
        [SerializeField] private BonusSlip pendingFatherSlip;
        [SerializeField] private BonusSlip pendingMotherSlip;
        [SerializeField] private string lastCalculatedKey = ""; // 年_月 のキー（二重計算防止）
        [SerializeField] private string lastPaidKey = "";       // 年_月 のキー（二重支給防止）

        [Header("給与実績の記録（5月・11月）")]
        [SerializeField] private int fatherSalaryMay = -1;
        [SerializeField] private int motherSalaryMay = -1;
        [SerializeField] private int fatherSalaryNov = -1;
        [SerializeField] private int motherSalaryNov = -1;

        // 前月の給与額面キャッシュ（汎用）
        private int lastFatherSalary = -1;
        private int lastMotherSalary = -1;

        public string SaveKey => "bonus";
        public int RestoreOrder => 105;

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
        }

        private void Start()
        {
            if (MonthTicker.I != null)
            {
                MonthTicker.I.OnMonthChanged += OnMonthChanged;
            }

            // 開始時点の給与をキャッシュ
            CacheCurrentSalaries(-1);
        }

        private void OnDestroy()
        {
            if (MonthTicker.I != null)
            {
                MonthTicker.I.OnMonthChanged -= OnMonthChanged;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 毎月1日の月変更時に呼ばれる。
        /// 1. 7月または1月であれば、保留中の賞与を振込・記録・通知する。
        /// 2. 6月または12月であれば、5月または11月の給与実績から賞与を計算して保留する。
        /// </summary>
        private void OnMonthChanged(int year, int month)
        {
            StartCoroutine(ProcessMonthlyRoutine(year, month));
        }

        private IEnumerator ProcessMonthlyRoutine(int year, int month)
        {
            yield return null; // 1フレーム待機（同一フレームの給与計算等の完了を待つ）

            // 1. 振り込み・記録・通知処理（7月または1月）
            if (month == 7 || month == 1)
            {
                int targetPendingMonth = month == 7 ? 6 : 12;
                if (hasPendingBonus && pendingBonusMonth == targetPendingMonth)
                {
                    string payoutKey = $"{year}_{month}";
                    if (lastPaidKey != payoutKey)
                    {
                        lastPaidKey = payoutKey;
                        ExecutePayout(year, month);
                    }
                }
            }

            // 2. 情報取得・算定処理（6月または12月）
            if (month == 6 || month == 12)
            {
                string calcKey = $"{year}_{month}";
                if (lastCalculatedKey != calcKey)
                {
                    lastCalculatedKey = calcKey;
                    CalculateAndHoldBonus(year, month);
                }
            }

            // 今月確定した給与をキャッシュ（5月・11月の実績もここで記録）
            CacheCurrentSalaries(month);
        }

        /// <summary>
        /// 6月・12月に情報を取得し、賞与額・税額を算定して翌月の振込用に保留する。
        /// ※給与実績は必ず夏季賞与は【5月】、冬季賞与は【11月】のものを参照する。
        /// </summary>
        public void CalculateAndHoldBonus(int year, int month)
        {
            if (Household.I == null || GameState.I == null) return;

            // 1. 参照対象月の決定（6月なら5月実績、12月なら11月実績）
            int targetSalaryMonth = (month == 6) ? 5 : 11;

            int fatherSalary = GetSalaryForTargetMonth(ParentRole.Father, targetSalaryMonth);
            int motherSalary = GetSalaryForTargetMonth(ParentRole.Mother, targetSalaryMonth);

            // 2. 子どもの年齢と扶養親族数の判定
            // 「なお子供が16歳未満であれば扶養親族数は０。１６歳以上では扶養親族数は１となる。
            //   扶養親族数のカウントは父母どちらか一方でカウントされるが、給与が多い方にカウントされるように。」
            GameDate.FromAgeDays(GameState.I.AgeDays, out int childAgeYears, out _, out _);
            bool isChild16OrOlder = childAgeYears >= 16;

            int fatherDependents = 0;
            int motherDependents = 0;

            if (isChild16OrOlder)
            {
                if (fatherSalary >= motherSalary)
                {
                    fatherDependents = 1;
                    motherDependents = 0;
                }
                else
                {
                    fatherDependents = 0;
                    motherDependents = 1;
                }
            }

            // 3. 父母それぞれの賞与を計算（参照月情報を明記）
            pendingFatherSlip = CalculateBonusSlip(ParentRole.Father, fatherSalary, fatherDependents, targetSalaryMonth);
            pendingMotherSlip = CalculateBonusSlip(ParentRole.Mother, motherSalary, motherDependents, targetSalaryMonth);

            hasPendingBonus = true;
            pendingBonusMonth = month;

            string season = month == 6 ? "夏季" : "冬季";
            int payoutMonth = month == 6 ? 7 : 1;
            int totalNet = pendingFatherSlip.netBonus + pendingMotherSlip.netBonus;

            Debug.Log($"<color=#29D4F5>[BonusManager]</color> {year}歳{month}月: {season}賞与の情報を取得・算定しました（給与実績は【{targetSalaryMonth}月】を参照、{payoutMonth}月に振込予定）\n" +
                      $"【父】参照給与({targetSalaryMonth}月): {pendingFatherSlip.previousSalary:N0}円, 社保: {pendingFatherSlip.socialInsurance:N0}円, ①: {pendingFatherSlip.amount1:N0}円, " +
                      $"扶養: {pendingFatherSlip.dependentsCount}人, 税率: {pendingFatherSlip.taxRate * 100:F3}%, 税額: {pendingFatherSlip.tax:N0}円 ➔ 手取り予定: {pendingFatherSlip.netBonus:N0}円\n" +
                      $"【母】参照給与({targetSalaryMonth}月): {pendingMotherSlip.previousSalary:N0}円, 社保: {pendingMotherSlip.socialInsurance:N0}円, ①: {pendingMotherSlip.amount1:N0}円, " +
                      $"扶養: {pendingMotherSlip.dependentsCount}人, 税率: {pendingMotherSlip.taxRate * 100:F3}%, 税額: {pendingMotherSlip.tax:N0}円 ➔ 手取り予定: {pendingMotherSlip.netBonus:N0}円\n" +
                      $"世帯受取予定額: {totalNet:N0}円 (子年齢: {childAgeYears}歳)");
        }

        /// <summary>
        /// 7月・1月に、保留されていた賞与を口座に振り込み、家計簿に記録し、通知を表示する。
        /// </summary>
        private void ExecutePayout(int year, int month)
        {
            int totalNetBonus = pendingFatherSlip.netBonus + pendingMotherSlip.netBonus;

            // 1. お金の振込（EffectApplier 経由で GameState と Ledger に反映）
            if (pendingFatherSlip.netBonus > 0)
            {
                var fatherEffect = new Effect
                {
                    kind = EffectKind.Money,
                    target = EffectTarget.Household,
                    value = pendingFatherSlip.netBonus,
                    duration = EffectDuration.Instant
                };
                EffectApplier.Apply(new[] { fatherEffect }, new LedgerSource("賞与（父）", ExpenseCategory.給料));
            }

            if (pendingMotherSlip.netBonus > 0)
            {
                var motherEffect = new Effect
                {
                    kind = EffectKind.Money,
                    target = EffectTarget.Household,
                    value = pendingMotherSlip.netBonus,
                    duration = EffectDuration.Instant
                };
                EffectApplier.Apply(new[] { motherEffect }, new LedgerSource("賞与（母）", ExpenseCategory.給料));
            }

            string season = pendingBonusMonth == 6 ? "夏季" : "冬季";
            int refMonth = pendingFatherSlip.baseSalaryMonth;

            // 2. ログ出力
            Debug.Log($"<color=#29D4F5>[BonusManager]</color> {year}歳{month}月: {season}賞与が振り込まれました！（参照給与実績: {refMonth}月分）\n" +
                      $"【父】手取り振込額: {pendingFatherSlip.netBonus:N0}円\n" +
                      $"【母】手取り振込額: {pendingMotherSlip.netBonus:N0}円\n" +
                      $"世帯受取合計: {totalNetBonus:N0}円 ➔ 家計簿に記録完了");

            // 3. トースト通知の表示
            string toastMsg = $"{season}賞与が振り込まれました（手取り: +{totalNetBonus:N0}円）";
            var toast = FindFirstObjectByType<SupportToast>();
            if (toast != null)
            {
                toast.Show(toastMsg);
            }

            // 4. 保留フラグをクリア
            hasPendingBonus = false;
            pendingBonusMonth = 0;
        }

        /// <summary>
        /// 親1人分の賞与明細を計算する。
        /// </summary>
        public BonusSlip CalculateBonusSlip(ParentRole role, int gross, int dependentsCount, int baseMonth = 0)
        {
            var slip = new BonusSlip
            {
                role = role,
                previousSalary = gross,
                dependentsCount = dependentsCount,
                baseSalaryMonth = baseMonth
            };

            if (gross <= 0)
            {
                return slip;
            }

            // 社会保険料の計算（TaxTable があればその率、なければ 15%）
            if (TaxManager.I != null && TaxManager.I.Table != null)
            {
                slip.socialInsurance = TaxCalculator.SocialInsurance(TaxManager.I.Table, gross);
            }
            else
            {
                slip.socialInsurance = Mathf.RoundToInt(gross * 0.15f);
            }

            // ① 給料から社会保険料を引いた金額
            slip.amount1 = Mathf.Max(0, gross - slip.socialInsurance);

            // ①と扶養親族数から税率を取得（syouyo-zeiritu.md 甲欄）
            slip.taxRate = BonusTaxTable.GetTaxRate(slip.amount1, dependentsCount);

            // ①にかけた税率（源泉徴収税額・1円未満切り捨て）
            slip.tax = Mathf.FloorToInt(slip.amount1 * slip.taxRate);

            // ①から税額を引いた金額（手取り賞与）
            slip.netBonus = Mathf.Max(0, slip.amount1 - slip.tax);

            return slip;
        }

        /// <summary>
        /// 指定された対象月（5月 または 11月）の給与実績を取得する。
        /// 実績が未記録の場合は現在の基本月給をフォールバックとして使用する。
        /// </summary>
        private int GetSalaryForTargetMonth(ParentRole role, int targetMonth)
        {
            int salary = -1;

            if (targetMonth == 5)
            {
                salary = (role == ParentRole.Father) ? fatherSalaryMay : motherSalaryMay;
            }
            else if (targetMonth == 11)
            {
                salary = (role == ParentRole.Father) ? fatherSalaryNov : motherSalaryNov;
            }

            // 5月・11月の専用キャッシュがあればそれを最優先
            if (salary > 0) return salary;

            // 直前月汎用キャッシュがあればそれを次点
            int cached = (role == ParentRole.Father) ? lastFatherSalary : lastMotherSalary;
            if (cached > 0) return cached;

            // いずれも未記録の場合は現在の基本月給を使用
            var parent = Household.I != null ? Household.I.Get(role) : null;
            return parent != null ? parent.MonthlySalary : 0;
        }

        private void CacheCurrentSalaries(int currentMonth)
        {
            if (Household.I == null) return;

            var father = Household.I.Father;
            var mother = Household.I.Mother;

            int fSal = father != null ? father.MonthlySalary : 0;
            int mSal = mother != null ? mother.MonthlySalary : 0;

            lastFatherSalary = fSal;
            lastMotherSalary = mSal;

            // 5月時点の給与実績を保存
            if (currentMonth == 5)
            {
                fatherSalaryMay = fSal;
                motherSalaryMay = mSal;
            }
            // 11月時点の給与実績を保存
            else if (currentMonth == 11)
            {
                fatherSalaryNov = fSal;
                motherSalaryNov = mSal;
            }
        }

        // ---------------- セーブ＆ロード (ISaveParticipant) ----------------

        [Serializable]
        private class BonusSaveData
        {
            public bool hasPendingBonus;
            public int pendingBonusMonth;
            public BonusSlip pendingFatherSlip;
            public BonusSlip pendingMotherSlip;
            public string lastCalculatedKey;
            public string lastPaidKey;
            public int fatherSalaryMay;
            public int motherSalaryMay;
            public int fatherSalaryNov;
            public int motherSalaryNov;
            public int lastFatherSalary;
            public int lastMotherSalary;
        }

        public string Capture()
        {
            var data = new BonusSaveData
            {
                hasPendingBonus = hasPendingBonus,
                pendingBonusMonth = pendingBonusMonth,
                pendingFatherSlip = pendingFatherSlip,
                pendingMotherSlip = pendingMotherSlip,
                lastCalculatedKey = lastCalculatedKey,
                lastPaidKey = lastPaidKey,
                fatherSalaryMay = fatherSalaryMay,
                motherSalaryMay = motherSalaryMay,
                fatherSalaryNov = fatherSalaryNov,
                motherSalaryNov = motherSalaryNov,
                lastFatherSalary = lastFatherSalary,
                lastMotherSalary = lastMotherSalary
            };
            return JsonUtility.ToJson(data);
        }

        public void Restore(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            var data = JsonUtility.FromJson<BonusSaveData>(json);
            if (data != null)
            {
                hasPendingBonus = data.hasPendingBonus;
                pendingBonusMonth = data.pendingBonusMonth;
                pendingFatherSlip = data.pendingFatherSlip;
                pendingMotherSlip = data.pendingMotherSlip;
                lastCalculatedKey = data.lastCalculatedKey ?? "";
                lastPaidKey = data.lastPaidKey ?? "";
                fatherSalaryMay = data.fatherSalaryMay;
                motherSalaryMay = data.motherSalaryMay;
                fatherSalaryNov = data.fatherSalaryNov;
                motherSalaryNov = data.motherSalaryNov;
                lastFatherSalary = data.lastFatherSalary;
                lastMotherSalary = data.lastMotherSalary;
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
            if (FindFirstObjectByType<BonusManager>() != null) return;

            var go = new GameObject("BonusManager");
            go.AddComponent<BonusManager>();
            DontDestroyOnLoad(go);
        }
    }
}
