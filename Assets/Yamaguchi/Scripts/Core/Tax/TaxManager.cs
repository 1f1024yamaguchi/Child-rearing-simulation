using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 税金の進行（taxes.md）。GameManager に付ける。
/// - 給料日：HouseholdIncome から Withhold を呼んでもらい、社会保険料・所得税（概算）・住民税を出す
/// - 11月15日：年末調整の書類を出す画面を頼む（OnYearEndFormRequested）
/// - 12月の給料日のあと：年末調整で1年分の所得税を精算する
/// - 2月16日〜3月15日：確定申告（OnTaxReturnRequested）
/// - 6月の給料日から：前の年の所得で決まる住民税を12回に分けて引く
/// お金を動かすのは給料日と同じく Core の中で完結させる（GameState.AddMoney ＋ Ledger.Record）。
/// </summary>
public class TaxManager : MonoBehaviour, ISaveParticipant
{
    public static TaxManager I { get; private set; }

    [Header("データ")]
    [SerializeField] private TaxTable table;
    [SerializeField] private List<TaxDeduction> deductions = new List<TaxDeduction>();

    [Header("日程（taxes.md 2章）")]
    [SerializeField] private MonthDay yearEndFormDay   = new MonthDay { month = 11, day = 15 };
    [SerializeField] private MonthDay taxReturnStart   = new MonthDay { month = 2,  day = 16 };
    [SerializeField] private MonthDay taxReturnReminder = new MonthDay { month = 3,  day = 8 };
    [SerializeField] private MonthDay taxReturnEnd     = new MonthDay { month = 3,  day = 15 };
    [Tooltip("住民税が切り替わる月（この月の給料日から前の年の分を引く）")]
    [Range(1, 12)] [SerializeField] private int residentStartMonth = 6;

    // ------------------------------------------------------------
    // 状態（セーブ対象）
    // ------------------------------------------------------------
    private readonly List<TaxYearRecord> records = new List<TaxYearRecord>();
    private readonly List<TaxClaim> claims = new List<TaxClaim>();
    private readonly List<TaxYearFlags> flags = new List<TaxYearFlags>();
    private readonly List<ResidentTaxEntry> resident = new List<ResidentTaxEntry>();

    // ------------------------------------------------------------
    // イベント
    // ------------------------------------------------------------
    /// <summary>年末調整の書類を出す画面を開いてほしい（引数：税金の年）。</summary>
    public event Action<int> OnYearEndFormRequested;
    /// <summary>確定申告の画面を開いてほしい（税金の年, リマインダーか）。</summary>
    public event Action<int, bool> OnTaxReturnRequested;
    /// <summary>年末調整で精算した（税金の年, 世帯で戻った額。マイナスは追加で引かれた額）。</summary>
    public event Action<int, int> OnYearEndSettled;
    /// <summary>確定申告をした（税金の年, 世帯で戻った額）。</summary>
    public event Action<int, int> OnTaxReturnFiled;

    public TaxTable Table => table;
    public IReadOnlyList<TaxDeduction> Deductions => deductions;

    public string SaveKey => "tax";
    public int RestoreOrder => 25;   // 年収 20 のあと、制度 30 の前

    private bool subscribed;

    // ------------------------------------------------------------
    // ライフサイクル
    // ------------------------------------------------------------

    private void Awake() { I = this; }

    private void OnEnable() => SaveSystem.Register(this);

    private void OnDisable()
    {
        SaveSystem.Unregister(this);
        Unsubscribe();
    }

    private void OnDestroy()
    {
        if (I == this) I = null;
    }

    private void Start()
    {
        if (table == null) Debug.LogError("TaxManager: TaxTable が割り当てられていません（子育て → 税金 → まとめてセットアップ）");
        Subscribe();
    }

    private void Subscribe()
    {
        if (subscribed) return;
        if (DayTicker.I == null || MonthTicker.I == null)
        {
            Debug.LogError("TaxManager: DayTicker / MonthTicker が見つかりません");
            return;
        }
        DayTicker.I.OnDayPassed += HandleDay;
        MonthTicker.I.OnMonthSettled += HandleMonthSettled;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (DayTicker.I != null) DayTicker.I.OnDayPassed -= HandleDay;
        if (MonthTicker.I != null) MonthTicker.I.OnMonthSettled -= HandleMonthSettled;
        subscribed = false;
    }

    // ------------------------------------------------------------
    // 給料日の天引き（HouseholdIncome から呼ぶ）
    // ------------------------------------------------------------

    /// <summary>
    /// 給料日に親1人ぶんの天引きを計算し、1年の記録に足す。お金は動かさない（呼んだ側が手取りを足す）。
    /// 額面が 0 でも住民税は引かれる（前の年の所得で決まるため）。
    /// </summary>
    public PaySlip Withhold(ParentRole role, int gross, int year, int month)
    {
        gross = Mathf.Max(0, gross);
        var slip = new PaySlip
        {
            gross = gross,
            socialInsurance = TaxCalculator.SocialInsurance(table, gross),
        };
        slip.incomeTax = MonthlyIncomeTax(role, gross, slip.socialInsurance, year);
        slip.residentTax = ResidentInstallment(role, year, month);

        var rec = Record(role, year, true);
        rec.gross += gross;
        rec.socialInsurance += slip.socialInsurance;
        rec.withheld += slip.incomeTax;
        rec.payCount++;
        return slip;
    }

    /// <summary>毎月の所得税（概算）。その月の額面 × 12 で1年分を出して ÷ 12。</summary>
    private int MonthlyIncomeTax(ParentRole role, int gross, int social, int year)
    {
        if (gross <= 0) return 0;
        int extra = 0;
        // 今年の書類を出したあとなら今年の分、まだなら去年出した書類のうち今年も条件を満たすもの
        int formYear = GetFlags(year, false)?.yearEndFormDone == true ? year : year - 1;
        foreach (var c in claims)
        {
            if (c.year != formYear || c.role != (int)role || c.route != (int)DeductionRoute.YearEnd) continue;
            var d = Find(c.deductionId);
            if (d == null || d.kind == DeductionKind.Medical || !d.ChildAgeOk(year)) continue;
            extra += d.incomeTaxAmount;
        }
        int annual = TaxCalculator.IncomeTax(table, gross * 12, social * 12, extra);
        return Mathf.RoundToInt(annual / 12f);
    }

    // ------------------------------------------------------------
    // 住民税
    // ------------------------------------------------------------

    /// <summary>この月の給料日に引く住民税。6月〜翌5月で前の年の分を12回に分ける（端数は6月）。</summary>
    public int ResidentInstallment(ParentRole role, int year, int month)
    {
        int baseYear = month >= residentStartMonth ? year - 1 : year - 2;
        int index = (month - residentStartMonth + 12) % 12;   // 6月=0 … 5月=11
        int annual = ResidentAnnual(role, baseYear);
        int per = annual / 12;
        return index == 0 ? annual - per * 11 : per;
    }

    /// <summary>baseYear の所得で決まる住民税の年税額。最初に必要になったときに決めて覚える。</summary>
    public int ResidentAnnual(ParentRole role, int baseYear)
    {
        foreach (var e in resident)
        {
            if (e.role == (int)role && e.baseYear == baseYear) return e.annual;
        }

        int annual;
        var rec = Record(role, baseYear, false);
        if (rec != null && rec.gross > 0)
        {
            annual = TaxCalculator.ResidentTax(table, rec.gross, rec.socialInsurance, ClaimedAmount(role, baseYear, true, null));
        }
        else if (baseYear < 0)
        {
            // ゲーム開始前の年：今の月給 × 12 の見込み（基礎控除だけ）
            var parent = Household.I != null ? Household.I.Get(role) : null;
            int g = parent != null ? parent.MonthlySalary * 12 : 0;
            annual = TaxCalculator.ResidentTax(table, g, TaxCalculator.SocialInsurance(table, g), 0);
        }
        else
        {
            annual = 0;   // その年に給料が無かった
        }

        resident.Add(new ResidentTaxEntry { role = (int)role, baseYear = baseYear, annual = annual });
        return annual;
    }

    // ------------------------------------------------------------
    // 控除の候補
    // ------------------------------------------------------------

    /// <summary>
    /// その年に申請できる控除（まだ申請していないもの）。
    /// forTaxReturn=false：年末調整の書類に載るもの／true：確定申告で出せるもの（年末調整で出し忘れたものも含む）。
    /// </summary>
    public List<DeductionOption> GetOptions(int year, bool forTaxReturn)
    {
        var list = new List<DeductionOption>();
        foreach (var d in deductions)
        {
            if (d == null || string.IsNullOrEmpty(d.id)) continue;
            if (!forTaxReturn && d.route != DeductionRoute.YearEnd) continue;
            if (IsClaimed(d, year)) continue;

            var o = new DeductionOption { deduction = d };
            o.motherEligible = IsEligible(d, year, ParentRole.Mother, !forTaxReturn);
            o.fatherEligible = IsEligible(d, year, ParentRole.Father, !forTaxReturn);
            if (!o.motherEligible && !o.fatherEligible) continue;
            o.motherAmount = DeductionAmount(d, year, ParentRole.Mother, false);
            o.fatherAmount = DeductionAmount(d, year, ParentRole.Father, false);

            // 税金が多く減るほう＝所得が高いほう（累進）を勧める
            if (o.motherEligible && o.fatherEligible)
                o.recommended = Income(ParentRole.Father, year, !forTaxReturn) >= Income(ParentRole.Mother, year, !forTaxReturn)
                    ? ParentRole.Father : ParentRole.Mother;
            else
                o.recommended = o.motherEligible ? ParentRole.Mother : ParentRole.Father;
            list.Add(o);
        }
        return list;
    }

    public bool IsClaimed(TaxDeduction d, int year)
    {
        foreach (var c in claims)
        {
            if (c.year == year && c.deductionId == d.id) return true;
        }
        return false;
    }

    /// <summary>その人がその年にこの控除を使えるか。estimate=true は年の途中（年末調整の書類）用の見込み。</summary>
    public bool IsEligible(TaxDeduction d, int year, ParentRole role, bool estimate)
    {
        if (d == null) return false;
        if (Household.I != null && Household.I.Get(role) == null) return false;
        if (!d.ChildAgeOk(year)) return false;

        int own = Income(role, year, estimate);
        if (d.claimantIncomeMax > 0 && own > d.claimantIncomeMax) return false;

        if (d.kind == DeductionKind.Spouse)
        {
            var other = role == ParentRole.Mother ? ParentRole.Father : ParentRole.Mother;
            if (Household.I != null && Household.I.Get(other) == null) return false;
            if (Income(other, year, estimate) > d.spouseIncomeMax) return false;
        }
        if (d.kind == DeductionKind.Medical && DeductionAmount(d, year, role, false) <= 0) return false;

        if (d.extraRequirements != null && d.extraRequirements.Count > 0)
        {
            var ctx = GameContext.Current();
            foreach (var r in d.extraRequirements)
            {
                if (r != null && !r.IsSatisfied(ctx)) return false;
            }
        }
        return true;
    }

    /// <summary>所得から引く額。forResident=true は住民税用の額。</summary>
    public int DeductionAmount(TaxDeduction d, int year, ParentRole role, bool forResident)
    {
        if (d == null) return 0;
        if (d.kind == DeductionKind.Medical)
            return TaxCalculator.MedicalDeduction(d, MedicalPaid(year), Income(role, year, false));
        return forResident ? d.residentTaxAmount : d.incomeTaxAmount;
    }

    /// <summary>その年の医療費（家計簿の「医療」の支出。支援で肩代わりされた分は入らない）。</summary>
    public int MedicalPaid(int year)
    {
        long sum = 0;
        for (int m = 0; m < 12; m++)
        {
            foreach (var l in Ledger.I.LinesOfMonth(year * 12 + m))
            {
                if (l.category == ExpenseCategory.医療 && l.amount < 0) sum += -l.amount;
            }
        }
        return (int)Math.Min(sum, int.MaxValue);
    }

    /// <summary>合計所得（給与所得）。estimate=true なら、これまでの給料日の平均 × 12 で見込む。</summary>
    public int Income(ParentRole role, int year, bool estimate)
    {
        return TaxCalculator.SalaryIncome(table, AnnualGross(role, year, estimate));
    }

    public int AnnualGross(ParentRole role, int year, bool estimate)
    {
        var rec = Record(role, year, false);
        if (!estimate) return rec != null ? rec.gross : 0;
        if (rec != null && rec.payCount > 0) return Mathf.RoundToInt(rec.gross / (float)rec.payCount * 12f);
        var parent = Household.I != null ? Household.I.Get(role) : null;
        return parent != null ? parent.MonthlySalary * 12 : 0;
    }

    // ------------------------------------------------------------
    // 年末調整
    // ------------------------------------------------------------

    /// <summary>年末調整の書類を出す。picks は（控除, 誰の控除か）。空でもよい（＝出さない）。</summary>
    public void SubmitYearEndForm(int year, IEnumerable<(TaxDeduction deduction, ParentRole role)> picks)
    {
        var f = GetFlags(year, true);
        if (f.yearEndFormDone) return;
        AddClaims(year, picks, DeductionRoute.YearEnd);
        f.yearEndFormDone = true;
    }

    /// <summary>12月の給料日のあと。1年分の所得税を計算し、源泉徴収した額との差を戻す（取る）。</summary>
    public void SettleYearEnd(int year)
    {
        var f = GetFlags(year, true);
        if (f.yearEndSettled) return;
        f.yearEndFormDone = true;   // 書類を出さないまま12月になった
        f.yearEndSettled = true;

        int total = 0;
        foreach (ParentRole role in Enum.GetValues(typeof(ParentRole)))
        {
            var rec = Record(role, year, false);
            if (rec == null) continue;
            int tax = AnnualIncomeTax(role, year, null);
            int diff = rec.withheld - tax;
            rec.settledTax = tax;
            if (diff == 0) continue;
            ApplyMoney(diff, $"年末調整（{RoleLabel(role)}）");
            total += diff;
        }
        OnYearEndSettled?.Invoke(year, total);

        string msg = total > 0 ? $"年末調整：所得税が {total:N0}円 戻りました"
                   : total < 0 ? $"年末調整：所得税が {-total:N0}円 足りなかったので引かれました"
                   : "年末調整：所得税の過不足はありませんでした";
        Toast(msg);
    }

    // ------------------------------------------------------------
    // 確定申告
    // ------------------------------------------------------------

    /// <summary>確定申告の期間か（year は申告する年＝前の年）。</summary>
    public bool IsTaxReturnOpen(int year)
    {
        if (GameState.I == null) return false;
        var f = GetFlags(year, false);
        if (f != null && (f.taxReturnFiled || f.taxReturnClosed)) return false;
        GameDate.FromAgeDays(GameState.I.AgeDays, out int y, out int m, out int d);
        if (y != year + 1) return false;
        int today = m * 100 + d;
        return today >= taxReturnStart.month * 100 + taxReturnStart.day
            && today <= taxReturnEnd.month * 100 + taxReturnEnd.day;
    }

    /// <summary>この控除を選んで申告したら、世帯でいくら戻るかの見込み。</summary>
    public int PreviewTaxReturn(int year, IEnumerable<(TaxDeduction deduction, ParentRole role)> picks)
    {
        var extra = ToClaims(year, picks, DeductionRoute.TaxReturnOnly);
        int total = 0;
        foreach (ParentRole role in Enum.GetValues(typeof(ParentRole)))
        {
            var rec = Record(role, year, false);
            if (rec == null) continue;
            int before = rec.settledTax >= 0 ? rec.settledTax : AnnualIncomeTax(role, year, null);
            total += before - AnnualIncomeTax(role, year, extra);
        }
        return total;
    }

    /// <summary>この控除を選んで申告したら、翌年度の住民税（世帯・1年分）がいくら減るかの見込み。</summary>
    public int PreviewResidentReduction(int year, IEnumerable<(TaxDeduction deduction, ParentRole role)> picks)
    {
        var extra = ToClaims(year, picks, DeductionRoute.TaxReturnOnly);
        int total = 0;
        foreach (ParentRole role in Enum.GetValues(typeof(ParentRole)))
        {
            var rec = Record(role, year, false);
            if (rec == null || rec.gross <= 0) continue;
            int before = TaxCalculator.ResidentTax(table, rec.gross, rec.socialInsurance, ClaimedAmount(role, year, true, null));
            int after = TaxCalculator.ResidentTax(table, rec.gross, rec.socialInsurance, ClaimedAmount(role, year, true, extra));
            total += before - after;
        }
        return total;
    }

    /// <summary>確定申告をする。戻る額はその場でお金に入れる。</summary>
    public int FileTaxReturn(int year, IEnumerable<(TaxDeduction deduction, ParentRole role)> picks)
    {
        var f = GetFlags(year, true);
        if (f.taxReturnFiled || f.taxReturnClosed) return 0;

        // 先に今の年税額を確定させておく（年末調整より前に呼ばれた場合の保険）
        foreach (ParentRole role in Enum.GetValues(typeof(ParentRole)))
        {
            var rec = Record(role, year, false);
            if (rec != null && rec.settledTax < 0) rec.settledTax = AnnualIncomeTax(role, year, null);
        }

        AddClaims(year, picks, DeductionRoute.TaxReturnOnly);
        f.taxReturnFiled = true;

        int total = 0;
        foreach (ParentRole role in Enum.GetValues(typeof(ParentRole)))
        {
            var rec = Record(role, year, false);
            if (rec == null) continue;
            int tax = AnnualIncomeTax(role, year, null);
            int refund = rec.settledTax - tax;
            rec.settledTax = tax;
            if (refund == 0) continue;
            ApplyMoney(refund, $"確定申告の還付（{RoleLabel(role)}）");
            total += refund;
        }
        OnTaxReturnFiled?.Invoke(year, total);
        Toast(total > 0 ? $"確定申告：所得税が {total:N0}円 戻りました" : "確定申告をしました");
        return total;
    }

    // ------------------------------------------------------------
    // 計算のまとめ
    // ------------------------------------------------------------

    /// <summary>申請済みの控除（＋extra）を使った1年分の所得税。</summary>
    public int AnnualIncomeTax(ParentRole role, int year, List<TaxClaim> extra)
    {
        var rec = Record(role, year, false);
        if (rec == null) return 0;
        return TaxCalculator.IncomeTax(table, rec.gross, rec.socialInsurance, ClaimedAmount(role, year, false, extra));
    }

    private int ClaimedAmount(ParentRole role, int year, bool forResident, List<TaxClaim> extra)
    {
        int sum = 0;
        foreach (var c in claims) sum += ClaimValue(c, role, year, forResident);
        if (extra != null) foreach (var c in extra) sum += ClaimValue(c, role, year, forResident);
        return sum;
    }

    private int ClaimValue(TaxClaim c, ParentRole role, int year, bool forResident)
    {
        if (c == null || c.year != year || c.role != (int)role) return 0;
        var d = Find(c.deductionId);
        if (d == null) return 0;
        // 年末調整の時点の見込みで出していても、実際の所得で条件を外れたら使えない
        if (!IsEligible(d, year, role, false)) return 0;
        return DeductionAmount(d, year, role, forResident);
    }

    // ------------------------------------------------------------
    // 日・月の処理
    // ------------------------------------------------------------

    private void HandleDay(int ageDays)
    {
        GameDate.FromAgeDays(ageDays, out int y, out int m, out int d);

        if (Is(m, d, yearEndFormDay))
        {
            var f = GetFlags(y, true);
            if (!f.yearEndFormDone)
            {
                if (GetOptions(y, false).Count == 0) f.yearEndFormDone = true;   // 出せる控除が無い年は画面を出さない
                else OnYearEndFormRequested?.Invoke(y);
            }
        }

        int prev = y - 1;
        if (prev < 0) return;
        if (Is(m, d, taxReturnStart) || Is(m, d, taxReturnReminder))
        {
            if (IsTaxReturnOpen(prev) && GetOptions(prev, true).Count > 0)
                OnTaxReturnRequested?.Invoke(prev, Is(m, d, taxReturnReminder));
        }
        else if (IsDayAfter(m, d, taxReturnEnd))
        {
            var f = GetFlags(prev, true);
            if (!f.taxReturnFiled && !f.taxReturnClosed)
            {
                f.taxReturnClosed = true;
                if (GetOptions(prev, true).Count > 0)
                    Toast($"{prev}歳分の確定申告の期限が過ぎました");
            }
        }
    }

    private void HandleMonthSettled(int year, int month)
    {
        if (month == 12) SettleYearEnd(year);
        if (month == 1) Prune(year);
    }

    /// <summary>もう使わない古い記録を消す（住民税の元になる2年前までは残す）。</summary>
    private void Prune(int year)
    {
        int keepFrom = year - 2;
        records.RemoveAll(r => r.year < keepFrom);
        claims.RemoveAll(c => c.year < keepFrom - 1);   // 去年の書類を今年の源泉徴収で使うので1年多めに
        flags.RemoveAll(f => f.year < keepFrom);
        resident.RemoveAll(e => e.baseYear < keepFrom && e.baseYear >= 0);
    }

    // ------------------------------------------------------------
    // 小物
    // ------------------------------------------------------------

    public TaxDeduction Find(string id)
    {
        foreach (var d in deductions)
        {
            if (d != null && d.id == id) return d;
        }
        return null;
    }

    public TaxYearRecord Record(ParentRole role, int year, bool create)
    {
        foreach (var r in records)
        {
            if (r.year == year && r.role == (int)role) return r;
        }
        if (!create) return null;
        var rec = new TaxYearRecord { year = year, role = (int)role };
        records.Add(rec);
        return rec;
    }

    public TaxYearFlags GetFlags(int year, bool create)
    {
        foreach (var f in flags)
        {
            if (f.year == year) return f;
        }
        if (!create) return null;
        var nf = new TaxYearFlags { year = year };
        flags.Add(nf);
        return nf;
    }

    private void AddClaims(int year, IEnumerable<(TaxDeduction deduction, ParentRole role)> picks, DeductionRoute route)
    {
        foreach (var c in ToClaims(year, picks, route))
        {
            var d = Find(c.deductionId);
            if (d != null && !IsClaimed(d, year)) claims.Add(c);
        }
    }

    private static List<TaxClaim> ToClaims(int year, IEnumerable<(TaxDeduction deduction, ParentRole role)> picks, DeductionRoute route)
    {
        var list = new List<TaxClaim>();
        if (picks == null) return list;
        foreach (var (d, role) in picks)
        {
            if (d == null || string.IsNullOrEmpty(d.id)) continue;
            list.Add(new TaxClaim { year = year, deductionId = d.id, role = (int)role, route = (int)route });
        }
        return list;
    }

    private static void ApplyMoney(int amount, string sourceName)
    {
        if (amount == 0 || GameState.I == null) return;
        GameState.I.AddMoney(amount);
        Ledger.I.Record(new LedgerSource(sourceName, ExpenseCategory.税金社会保険), amount);
    }

    public static string RoleLabel(ParentRole role) => role == ParentRole.Mother ? "母" : "父";

    private static bool Is(int m, int d, MonthDay md) => m == md.month && d == md.day;

    private static bool IsDayAfter(int m, int d, MonthDay md)
    {
        int last = GameDate.DaysInMonth(md.month);
        if (md.day < last) return m == md.month && d == md.day + 1;
        return m == md.month % 12 + 1 && d == 1;
    }

    private void Toast(string message)
    {
        var toast = FindFirstObjectByType<SupportToast>();
        if (toast != null) toast.Show(message);
        Debug.Log($"[Tax] {message}");
    }

    // ------------------------------------------------------------
    // セーブ
    // ------------------------------------------------------------

    public string Capture()
    {
        var data = new TaxSaveData();
        data.records.AddRange(records);
        data.claims.AddRange(claims);
        data.flags.AddRange(flags);
        data.resident.AddRange(resident);
        return JsonUtility.ToJson(data);
    }

    public void Restore(string json)
    {
        records.Clear(); claims.Clear(); flags.Clear(); resident.Clear();
        if (string.IsNullOrEmpty(json)) return;
        var data = JsonUtility.FromJson<TaxSaveData>(json);
        if (data == null) return;
        if (data.records != null) records.AddRange(data.records);
        if (data.claims != null) claims.AddRange(data.claims);
        if (data.flags != null) flags.AddRange(data.flags);
        if (data.resident != null) resident.AddRange(data.resident);
    }

    // ------------------------------------------------------------
    // テスト用（Inspector の右クリック）
    // ------------------------------------------------------------

    [ContextMenu("テスト：年末調整の画面を開く（今年）")]
    private void DebugOpenYearEnd()
    {
        if (GameState.I == null) return;
        GameDate.FromAgeDays(GameState.I.AgeDays, out int y, out _, out _);
        OnYearEndFormRequested?.Invoke(y);
    }

    [ContextMenu("テスト：確定申告の画面を開く（去年分）")]
    private void DebugOpenTaxReturn()
    {
        if (GameState.I == null) return;
        GameDate.FromAgeDays(GameState.I.AgeDays, out int y, out _, out _);
        OnTaxReturnRequested?.Invoke(Mathf.Max(0, y - 1), false);
    }

    [ContextMenu("テスト：今年の年末調整を今すぐ精算")]
    private void DebugSettleNow()
    {
        if (GameState.I == null) return;
        GameDate.FromAgeDays(GameState.I.AgeDays, out int y, out _, out _);
        SettleYearEnd(y);
    }
}
