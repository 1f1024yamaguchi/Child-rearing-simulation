using System;
using System.Collections.Generic;
using Maruyama.Core;
using UnityEngine;

/// <summary>
/// 支援制度の状態を1か所で持つ（support-apply.md 6章）。画面（サポート画面・カレンダー）はこれを読むだけ。
/// - DayTicker.OnDayPassed：日付と条件で変わる状態を判定（申請できる／期限切れ／支給停止…）
/// - MonthTicker.OnMonthChanged：受給中の制度に「今の年齢の支給額」を払う
/// 効果の適用は必ず EffectApplier.Apply(...)。GameState を直接触らない。
/// </summary>
public class SupportManager : MonoBehaviour, ISaveParticipant
{
    public static SupportManager I { get; private set; }

    public string SaveKey => "support";
    public int RestoreOrder => 30;

    private void OnEnable()
    {
        SaveSystem.Register(this);
        SaveSystem.OnLoaded += HandleSaveLoaded;
    }

    private void OnDisable()
    {
        SaveSystem.Unregister(this);
        SaveSystem.OnLoaded -= HandleSaveLoaded;
    }

    [Header("制度の一覧（並び順＝表示順）")]
    [SerializeField] private List<SupportScheme> schemes = new List<SupportScheme>();

    [Header("動作確認用（右クリックメニューから実行）")]
    [SerializeField] private SupportScheme debugScheme;

    private readonly Dictionary<string, SupportSchemeRuntime> runtimes = new Dictionary<string, SupportSchemeRuntime>();

    public event Action<SupportScheme, SupportState> OnStateChanged;
    public event Action<SupportScheme> OnPaid;
    public event Action<SupportScheme> OnExpired;

    public IReadOnlyList<SupportScheme> Schemes => schemes;

    /// <summary>id から制度を取得する。見つからなければ null。</summary>
    public SupportScheme GetScheme(string id)
    {
        if (string.IsNullOrEmpty(id) || schemes == null) return null;
        return schemes.Find(s => s != null && s.id == id);
    }

    // ------------------------------------------------------------
    // ライフサイクル
    // ------------------------------------------------------------

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }
        I = this;
        ValidateSchemes();
    }

    /// <summary>id が空・重複の制度を警告する。id が空の制度は以降の処理から除外される。</summary>
    private void ValidateSchemes()
    {
        var seen = new HashSet<string>();
        foreach (var s in schemes)
        {
            if (s == null) continue;
            if (string.IsNullOrEmpty(s.id))
            {
                Debug.LogWarning($"SupportManager: id が空の制度があります（{s.name}）。この制度は処理されません", s);
                continue;
            }
            if (!seen.Add(s.id))
                Debug.LogWarning($"SupportManager: 制度の id が重複しています：{s.id}（{s.name}）", s);
        }
    }

    private static bool IsValid(SupportScheme s) => s != null && !string.IsNullOrEmpty(s.id);

    private void Start()
    {
        if (DayTicker.I != null) DayTicker.I.OnDayPassed += HandleDayPassed;
        else Debug.LogError("SupportManager: DayTicker が見つかりません");

        if (MonthTicker.I != null) MonthTicker.I.OnMonthChanged += HandleMonthChanged;
        else Debug.LogError("SupportManager: MonthTicker が見つかりません");

        EnsurePostpartumLeave();
        EvaluateAll();   // 全制度の状態を今日で1回計算
    }

    private void OnDestroy()
    {
        if (DayTicker.I != null) DayTicker.I.OnDayPassed -= HandleDayPassed;
        if (MonthTicker.I != null) MonthTicker.I.OnMonthChanged -= HandleMonthChanged;
        if (I == this) I = null;
    }

    private void HandleSaveLoaded()
    {
        EnsurePostpartumLeave();
        EvaluateAll();
    }

    // ------------------------------------------------------------
    // 読み取り窓口
    // ------------------------------------------------------------

    public IEnumerable<SupportScheme> GetByCategory(SupportCategory c)
    {
        foreach (var s in schemes)
        {
            if (IsValid(s) && s.category == c) yield return s;
        }
    }

    public SupportState GetState(SupportScheme s) => GetRuntime(s).state;

    /// <summary>受給中（Active）の制度の一覧。並びは Schemes と同じ。</summary>
    public IEnumerable<SupportScheme> ActiveSchemes()
    {
        foreach (var s in schemes)
        {
            if (IsValid(s) && GetRuntime(s).state == SupportState.Active) yield return s;
        }
    }

    /// <summary>
    /// 今月、支出の肩代わり（covers）が効いている制度の一覧（expenses.md 4章）。
    /// 受給中（Active）かつ covers があり、申請した月より後の月のもの（毎月の支給と同じく「翌月から」）。
    /// 支給停止（Suspended）中は肩代わりしない。covers の期間の判定は使う側（ExpenseManager）で行う。
    /// </summary>
    public IEnumerable<SupportScheme> CoveringSchemes()
    {
        int monthIndex = MonthIndexOf(Today());
        foreach (var s in schemes)
        {
            if (!IsValid(s) || s.covers == null || s.covers.Count == 0) continue;
            var rt = GetRuntime(s);
            if (rt.state == SupportState.Active && monthIndex > rt.appliedMonthIndex) yield return s;
        }
    }

    /// <summary>制度の実行中の状態。無ければ今日の日付と条件から計算して作る。</summary>
    public SupportSchemeRuntime GetRuntime(SupportScheme s)
    {
        if (!IsValid(s))
        {
            // id が空の制度はエラーで止めず、保存しない仮の状態を返す
            Debug.LogWarning($"SupportManager: id が空の制度は扱えません（{(s != null ? s.name : "null")}）", s);
            return new SupportSchemeRuntime { id = "", state = SupportState.Locked };
        }
        if (!runtimes.TryGetValue(s.id, out var rt))
        {
            rt = new SupportSchemeRuntime { id = s.id, state = SupportState.Locked };
            runtimes[s.id] = rt;
            rt.state = ComputeAvailability(s, rt, Today(), GameContext.Current());
            if (rt.state == SupportState.Expired) rt.expiredNotified = true;   // 作った時点で過ぎていた期限は通知しない
        }
        return rt;
    }

    /// <summary>満たしていない申請条件（カードに「条件：年収910万円以下」と出す用）。</summary>
    public IEnumerable<RequirementSO> UnmetRequirements(SupportScheme s)
    {
        var ctx = GameContext.Current();
        foreach (var r in s.applyRequirements)
        {
            if (r != null && !r.IsSatisfied(ctx)) yield return r;
        }
    }

    /// <summary>今の支給の段（条件も満たすもの）。無ければ null。</summary>
    public PaymentTier CurrentTier(SupportScheme s)
    {
        int i = FindTierIndex(s, Today(), GameContext.Current());
        return i >= 0 ? s.monthlyTiers[i] : null;
    }

    /// <summary>次の段（今日より後に始まる段のうち、いちばん近いもの）。無ければ null。</summary>
    public PaymentTier NextTier(SupportScheme s)
    {
        int today = Today();
        PaymentTier best = null;
        int bestFrom = int.MaxValue;
        foreach (var t in s.monthlyTiers)
        {
            if (t == null) continue;
            int from = t.fromAge.ToAgeDays();
            if (from > today && from < bestFrom) { best = t; bestFrom = from; }
        }
        return best;
    }

    public bool CanApply(SupportScheme s)
    {
        if (s == null || GetState(s) != SupportState.Available ||
            !RequirementsMet(s, GameContext.Current()) || !WorkModeRequirementsMet(s))
            return false;
        return true;
    }

    public bool CanReturn(SupportScheme s)
    {
        if (!IsValid(s) || !s.changesWorkMode || GetState(s) != SupportState.Active || Household.I == null)
            return false;
        var parent = Household.I.Get(s.workTarget);
        return parent != null && parent.WorkMode == s.workMode;
    }

    public WageLinkedTier CurrentWageLinkedTier(SupportScheme s)
    {
        if (!IsValid(s)) return null;
        var rt = GetRuntime(s);
        return WageTierForDay(s, Today() - rt.workStartedAgeDays + 1);
    }

    public int CurrentWageLinkedDay(SupportScheme s)
    {
        if (!IsValid(s)) return 0;
        return Mathf.Max(0, Today() - GetRuntime(s).workStartedAgeDays + 1);
    }

    public int CurrentWageLinkedDailyAmount(SupportScheme s)
    {
        if (!IsValid(s)) return 0;
        var rt = GetRuntime(s);
        var tier = CurrentWageLinkedTier(s);
        if (tier == null) return 0;
        float amount = 0f;
        foreach (var parent in WageParents(s))
        {
            if (parent != null && WageModeSatisfied(s, parent))
                amount += DailyWageBase(parent, tier.basis, rt.salaryBeforeLeave) * tier.rate;
        }
        return Mathf.RoundToInt(amount);
    }

    public bool IsWageLinkedEligible(SupportScheme s)
    {
        if (!IsValid(s)) return false;
        foreach (var parent in WageParents(s))
        {
            if (parent != null && WageModeSatisfied(s, parent)) return true;
        }
        return false;
    }

    public bool CanRenew(SupportScheme s)
    {
        var rt = GetRuntime(s);
        if (rt.state != SupportState.Active || s.deadlineType != DeadlineType.Yearly) return false;
        int today = Today();
        int deadline = YearlyDeadline(s, rt.renewedYear + 1);
        return today >= deadline - s.renewWindowDays && today <= deadline;
    }

    public bool CanResume(SupportScheme s)
    {
        return GetState(s) == SupportState.Suspended;
    }

    /// <summary>期限まであと何日。期限が無い（または対象外の状態）なら -1。</summary>
    public int DaysUntilDeadline(SupportScheme s)
    {
        int d = NextDeadlineAgeDays(s);
        return d < 0 ? -1 : Mathf.Max(0, d - Today());
    }

    /// <summary>
    /// 次の期限の日（通算日数）。期限が無ければ -1。
    /// Once：申請前（Locked / Available）の間だけ。Yearly：受給中（Active）の間だけ今年分が返る。
    /// </summary>
    public int NextDeadlineAgeDays(SupportScheme s)
    {
        var rt = GetRuntime(s);
        switch (s.deadlineType)
        {
            case DeadlineType.Once:
                if (rt.state == SupportState.Locked || rt.state == SupportState.Available)
                    return s.DeadlineAgeDays;
                return -1;
            case DeadlineType.Yearly:
                if (rt.state == SupportState.Active)
                    return YearlyDeadline(s, rt.renewedYear + 1);
                return -1;
            default:
                return -1;
        }
    }

    // ------------------------------------------------------------
    // 操作（申請・更新・再開）
    // ------------------------------------------------------------

    public bool Apply(SupportScheme s)
    {
        if (s == null || !CanApply(s)) return false;

        var rt = GetRuntime(s);
        int today = Today();
        rt.appliedAgeDays = today;
        rt.appliedMonthIndex = MonthIndexOf(today);
        rt.renewedYear = FirstRenewedYear(s, today);   // 毎年の手続きの1回目の期限（案C）

        if (s.changesWorkMode)
        {
            var parent = Household.I.Get(s.workTarget);
            parent.SetWorkMode(s.workMode);
            rt.salaryBeforeLeave = parent.SalaryBeforeLeave;
        }
        else if (HasWageLinkedTiers(s) && s.wageTarget == WageTarget.WorkTarget &&
                 HasSalaryBeforeLeaveTier(s))
        {
            var parent = Household.I.Get(s.workTarget);
            if (parent != null) rt.salaryBeforeLeave = parent.SalaryBeforeLeave;
        }
        if (HasWageLinkedTiers(s)) rt.workStartedAgeDays = today;

        // 申請したときに1回だけ起きること（OnApply の本体／Monthly の手間のストレスなど）
        int money = MoneyOf(s.effects);
        EffectApplier.Apply(s.effects, new LedgerSource(s.title, ExpenseCategory.支援制度));

        if (s.effectTiming == EffectTiming.OnApply)
        {
            float linkedAmount = CalculateOnApplyWageAmount(s, today);
            rt.wageLinkedPendingAmount += linkedAmount;
            rt.lastPaidAmount = money;
            SetState(s, rt, SupportState.Received);
            string payoutTiming = linkedAmount > 0f ? "翌月に給付" : "受け取り済み";
            Debug.Log($"[SupportManager] {s.title}：申請しました（{payoutTiming}）{FormatMoney(money + Mathf.RoundToInt(linkedAmount))}");
        }
        else
        {
            SetState(s, rt, SupportState.Active);   // 毎月の支給は申請した月の翌月から
            Debug.Log($"[SupportManager] {s.title}：申請しました（受給中・翌月から支給）");
        }
        if (s.changesWorkMode) EvaluateAll();
        return true;
    }

    public bool ReturnFromWorkMode(SupportScheme s)
    {
        if (!CanReturn(s)) return false;
        var parent = Household.I.Get(s.workTarget);
        parent.SetWorkMode(WorkMode.Normal);
        SetState(s, GetRuntime(s), SupportState.Ended);
        EvaluateAll();
        Debug.Log($"[SupportManager] {s.title}：働き方を通常に戻しました");
        return true;
    }

    /// <summary>毎年の手続き（更新）。今年分の手続き済みにする。</summary>
    public bool Renew(SupportScheme s)
    {
        if (s == null || !CanRenew(s)) return false;
        var rt = GetRuntime(s);
        rt.renewedYear += 1;
        Debug.Log($"[SupportManager] {s.title}：{rt.renewedYear}歳の手続きを済ませました");
        OnStateChanged?.Invoke(s, rt.state);   // 状態は Active のまま。表示（期限の行）を更新させる
        return true;
    }

    /// <summary>支給停止からの再開。止まっていた月の分をまとめて適用する。</summary>
    public bool Resume(SupportScheme s)
    {
        if (s == null || !CanResume(s)) return false;
        var rt = GetRuntime(s);

        int owedCount = rt.owedTierIndexes.Count;
        int owedMoney = 0;
        foreach (int idx in rt.owedTierIndexes)
        {
            if (idx < 0 || idx >= s.monthlyTiers.Count) continue;
            var tier = s.monthlyTiers[idx];
            owedMoney += tier.MoneyAmount();
            EffectApplier.Apply(tier.monthlyEffects, new LedgerSource(s.title, ExpenseCategory.支援制度));
        }
        rt.owedTierIndexes.Clear();
        rt.renewedYear = FirstRenewedYear(s, Today());
        rt.suspendedAgeDays = -1;
        SetState(s, rt, SupportState.Active);
        Debug.Log($"[SupportManager] {s.title}：再開しました。止まっていた {owedCount} か月分をまとめて適用 {FormatMoney(owedMoney)}");
        return true;
    }

    // ------------------------------------------------------------
    // セーブ・ロード
    // ------------------------------------------------------------

    public List<SupportSchemeRuntime> ToSave()
    {
        var list = new List<SupportSchemeRuntime>();
        foreach (var s in schemes)
        {
            if (!IsValid(s)) continue;
            var rt = GetRuntime(s);
            list.Add(JsonUtility.FromJson<SupportSchemeRuntime>(JsonUtility.ToJson(rt)));   // 深いコピー
        }
        return list;
    }

    /// <summary>セーブにない制度（後から追加したアセット）は、今日の日付と条件から状態を計算して作る。</summary>
    public void Restore(List<SupportSchemeRuntime> saved)
    {
        runtimes.Clear();
        if (saved != null)
        {
            foreach (var rt in saved)
            {
                if (rt != null && !string.IsNullOrEmpty(rt.id)) runtimes[rt.id] = rt;
            }
        }
        foreach (var s in schemes)
        {
            if (!IsValid(s)) continue;
            var rt = GetRuntime(s);   // 無ければここで計算して作られる
            OnStateChanged?.Invoke(s, rt.state);
        }
        EnsurePostpartumLeave();
    }

    [Serializable]
    private class SupportSaveData
    {
        public List<SupportSchemeRuntime> runtimes = new List<SupportSchemeRuntime>();
    }

    public string Capture()
    {
        var data = new SupportSaveData
        {
            runtimes = ToSave()
        };
        return JsonUtility.ToJson(data);
    }

    public void Restore(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        var data = JsonUtility.FromJson<SupportSaveData>(json);
        Restore(data?.runtimes);
    }

    // ------------------------------------------------------------
    // 時間で進める
    // ------------------------------------------------------------

    private void HandleDayPassed(int ageDays)
    {
        var mother = Household.I != null ? Household.I.Mother : null;
        if (mother != null && mother.WorkMode == WorkMode.MaternityLeave &&
            ageDays > new GameDay(0, 2, 25).ToAgeDays())
            mother.SetWorkMode(WorkMode.Normal);

        EvaluateAll();
        AccrueWageLinkedBenefits(ageDays);
    }

    private void EvaluateAll()
    {
        int today = Today();
        var ctx = GameContext.Current();
        foreach (var s in schemes)
        {
            if (!IsValid(s)) continue;
            EvaluateOne(s, GetRuntime(s), today, ctx);
        }
    }

    /// <summary>support-apply.md 2.1 の表のうち、日付と条件で変わるもの。</summary>
    private void EvaluateOne(SupportScheme s, SupportSchemeRuntime rt, int today, GameContext ctx)
    {
        switch (rt.state)
        {
            case SupportState.Locked:
            case SupportState.Available:
                SetState(s, rt, ComputeAvailability(s, rt, today, ctx));
                break;

            case SupportState.Active:
                if (HasEnded(s, today))
                {
                    EndWorkMode(s);
                    SetState(s, rt, SupportState.Ended);
                }
                else if (s.deadlineType == DeadlineType.Yearly)
                {
                    int deadline = YearlyDeadline(s, rt.renewedYear + 1);
                    if (today > deadline)   // 期限日の翌日になった（未更新）
                    {
                        rt.suspendedAgeDays = deadline + 1;
                        SetState(s, rt, SupportState.Suspended);
                    }
                }
                break;

            case SupportState.Suspended:
                // 期限日から lapseYears 年たったら受給資格を失う
                if (today >= (rt.suspendedAgeDays - 1) + s.lapseYears * GameDate.DaysPerYear)
                {
                    rt.owedTierIndexes.Clear();
                    SetState(s, rt, SupportState.Lost);
                }
                break;
        }
    }

    /// <summary>申請前（Locked / Available / Expired）の状態を、日付と条件から決める。</summary>
    private SupportState ComputeAvailability(SupportScheme s, SupportSchemeRuntime rt, int today, GameContext ctx)
    {
        if (s.effectTiming == EffectTiming.Monthly && HasEnded(s, today))
            return SupportState.Ended;   // 申請前でも受給期間が終わっていれば受給終了
        if (s.deadlineType == DeadlineType.Once && today > s.DeadlineAgeDays)
            return SupportState.Expired;   // 期限日のその日のうちは申請できる
        if (today < s.StartAgeDays) return SupportState.Locked;
        if (!RequirementsMet(s, ctx) || !WorkModeRequirementsMet(s)) return SupportState.Locked;
        return SupportState.Available;
    }

    private void HandleMonthChanged(int year, int month)
    {
        EvaluateAll();   // その日の状態（支給停止など）を確定させてから支給する

        int today = Today();
        int monthIndex = year * 12 + (month - 1);
        var ctx = GameContext.Current();

        foreach (var s in schemes)
        {
            if (!IsValid(s)) continue;
            var rt = GetRuntime(s);
            PayWageLinkedPending(s, rt, monthIndex);
            if (s.effectTiming != EffectTiming.Monthly) continue;

            if (rt.state == SupportState.Active)
            {
                if (HasEnded(s, today))
                {
                    EndWorkMode(s);
                    SetState(s, rt, SupportState.Ended);
                    continue;
                }
                if (HasWageLinkedTiers(s)) continue;
                // 支給表が空で covers だけの制度（無償化など）はお金を払わない。肩代わりは ExpenseManager が行う
                if (IsCoversOnly(s)) continue;
                PayMonth(s, rt, monthIndex, today, ctx);
            }
            else if (rt.state == SupportState.Suspended)
            {
                // 止まっている間は、払うはずだった段を積んでおく（再開時にまとめて払う）
                if (monthIndex > rt.appliedMonthIndex)
                {
                    int idx = FindTierIndex(s, today, ctx);
                    if (idx >= 0) rt.owedTierIndexes.Add(idx);
                }
            }
        }
    }

    /// <summary>毎月の支給（3.2）。申請した月の翌月から。同じ月に2回払わない。</summary>
    private void PayMonth(SupportScheme s, SupportSchemeRuntime rt, int monthIndex, int today, GameContext ctx)
    {
        if (monthIndex <= rt.appliedMonthIndex) return;   // 申請した月と、それ以前の分は払わない
        if (monthIndex <= rt.lastPaidMonthIndex) return;

        int idx = FindTierIndex(s, today, ctx);
        rt.lastPaidMonthIndex = monthIndex;

        if (idx < 0)
        {
            rt.lastPaidAmount = 0;   // 当てはまる段がない月は 0（状態は Active のまま）
            Debug.Log($"[SupportManager] {s.title}：今月は対象外（支給 0）");
            OnStateChanged?.Invoke(s, rt.state);   // カードの「今月は対象外」表示を更新させる
            return;
        }

        var tier = s.monthlyTiers[idx];
        int money = tier.MoneyAmount();
        EffectApplier.Apply(tier.monthlyEffects, new LedgerSource(s.title, ExpenseCategory.支援制度));
        rt.lastPaidAmount = money;
        Debug.Log($"[SupportManager] {s.title} 段:{tier.DisplayLabel()} {FormatMoney(money)}");
        OnPaid?.Invoke(s);
        OnStateChanged?.Invoke(s, rt.state);
    }

    private void AccrueWageLinkedBenefits(int ageDays)
    {
        if (Household.I == null) return;
        foreach (var s in schemes)
        {
            if (!IsValid(s) || s.effectTiming != EffectTiming.Monthly || !HasWageLinkedTiers(s))
                continue;
            var rt = GetRuntime(s);
            if (rt.state != SupportState.Active || ageDays <= rt.appliedAgeDays)
                continue;
            if (s.wageBenefitUntil.ToAgeDays() > 0 && ageDays > s.wageBenefitUntil.ToAgeDays())
                continue;

            foreach (var parent in WageParents(s))
            {
                if (parent == null || !WageModeSatisfied(s, parent)) continue;
                var tier = WageTierForDay(s, ageDays - rt.workStartedAgeDays);
                if (tier == null) continue;
                rt.wageLinkedPendingAmount += DailyWageBase(parent, tier.basis, rt.salaryBeforeLeave) * tier.rate;
            }
        }
    }

    private void PayWageLinkedPending(SupportScheme s, SupportSchemeRuntime rt, int monthIndex)
    {
        if (!HasWageLinkedTiers(s) || rt.wageLinkedPendingAmount <= 0f ||
            monthIndex <= rt.wageLinkedLastPaidMonthIndex)
            return;

        int amount = Mathf.RoundToInt(rt.wageLinkedPendingAmount);
        rt.wageLinkedPendingAmount = 0f;
        rt.wageLinkedLastPaidMonthIndex = monthIndex;
        rt.lastPaidMonthIndex = monthIndex;
        rt.lastPaidAmount = amount;
        if (amount > 0)
        {
            EffectApplier.Apply(
                new[] { new Effect { kind = EffectKind.Money, value = amount } },
                new LedgerSource(s.title + "（非課税）", ExpenseCategory.支援制度));
            OnPaid?.Invoke(s);
        }
        OnStateChanged?.Invoke(s, rt.state);
    }

    private float CalculateOnApplyWageAmount(SupportScheme s, int today)
    {
        if (!HasWageLinkedTiers(s)) return 0f;
        int maxDay = 0;
        foreach (var tier in s.wageLinkedTiers)
        {
            if (tier != null)
                maxDay = Mathf.Max(maxDay, tier.toDay > 0 ? tier.toDay : today - s.StartAgeDays + 1);
        }
        int days = Mathf.Max(0, Mathf.Min(maxDay, today - s.StartAgeDays + 1));

        float amount = 0f;
        var rt = GetRuntime(s);
        foreach (var parent in WageParents(s))
        {
            if (parent == null) continue;
            for (int day = 1; day <= days; day++)
            {
                var tier = WageTierForDay(s, day);
                if (tier != null)
                    amount += DailyWageBase(parent, tier.basis, rt.salaryBeforeLeave) * tier.rate;
            }
        }
        return amount;
    }

    private void EndWorkMode(SupportScheme s)
    {
        if (!s.changesWorkMode || Household.I == null) return;
        var parent = Household.I.Get(s.workTarget);
        if (parent != null && parent.WorkMode == s.workMode)
            parent.SetWorkMode(WorkMode.Normal);
    }

    private void EnsurePostpartumLeave()
    {
        var mother = Household.I != null ? Household.I.Mother : null;
        if (mother != null && Today() <= new GameDay(0, 2, 25).ToAgeDays() &&
            mother.WorkMode == WorkMode.Normal)
            mother.SetWorkMode(WorkMode.MaternityLeave);
    }

    private static bool WageModeSatisfied(SupportScheme s, ParentStatus parent)
    {
        if (s.changesWorkMode) return parent.WorkMode == s.workMode;
        return !s.requiresWorkMode || parent.WorkMode == s.requiredWorkMode;
    }

    private static IEnumerable<ParentStatus> WageParents(SupportScheme s)
    {
        if (Household.I == null) yield break;
        if (s.wageTarget == WageTarget.BothParents)
        {
            foreach (var parent in Household.I.Parents) yield return parent;
            yield break;
        }
        yield return Household.I.Get(s.workTarget);
    }

    private static WageLinkedTier WageTierForDay(SupportScheme s, int day)
    {
        if (s.wageLinkedTiers == null) return null;
        foreach (var tier in s.wageLinkedTiers)
        {
            if (tier != null && tier.Contains(day)) return tier;
        }
        return null;
    }

    private static float DailyWageBase(ParentStatus parent, WageBase basis, int salaryBeforeLeave = 0)
    {
        return basis == WageBase.SalaryBeforeLeave
            ? (salaryBeforeLeave > 0 ? salaryBeforeLeave : parent.SalaryBeforeLeave)
                / (float)ParentStatus.WageDaysPerMonth
            : parent.DailyWage;
    }

    private static bool HasWageLinkedTiers(SupportScheme s) =>
        s.wageLinkedTiers != null && s.wageLinkedTiers.Count > 0;

    private static bool HasSalaryBeforeLeaveTier(SupportScheme s)
    {
        foreach (var tier in s.wageLinkedTiers)
        {
            if (tier != null && tier.basis == WageBase.SalaryBeforeLeave) return true;
        }
        return false;
    }

    // ------------------------------------------------------------
    // 内部ヘルパー
    // ------------------------------------------------------------

    private void SetState(SupportScheme s, SupportSchemeRuntime rt, SupportState next)
    {
        if (rt.state == next) return;
        var prev = rt.state;
        rt.state = next;
        Debug.Log($"[SupportManager] {s.title}：{prev} → {next}");
        OnStateChanged?.Invoke(s, next);

        if (next == SupportState.Expired && !rt.expiredNotified)
        {
            rt.expiredNotified = true;
            OnExpired?.Invoke(s);   // 通知は1回だけ
        }
    }

    private static bool RequirementsMet(SupportScheme s, GameContext ctx)
    {
        foreach (var r in s.applyRequirements)
        {
            if (r != null && !r.IsSatisfied(ctx)) return false;
        }
        return true;
    }

    private static bool WorkModeRequirementsMet(SupportScheme s)
    {
        if (Household.I == null) return !s.changesWorkMode && !s.requiresWorkMode;
        if (s.changesWorkMode)
        {
            var parent = Household.I.Get(s.workTarget);
            if (parent == null || parent.WorkMode != WorkMode.Normal) return false;
        }
        if (s.requiresWorkMode)
        {
            var parent = Household.I.Get(s.workTarget);
            if (parent == null || parent.WorkMode != s.requiredWorkMode) return false;
        }
        return true;
    }

    /// <summary>今日に当てはまる段（期間内かつ追加条件を満たす最初のもの）の番号。無ければ -1。</summary>
    private static int FindTierIndex(SupportScheme s, int today, GameContext ctx)
    {
        for (int i = 0; i < s.monthlyTiers.Count; i++)
        {
            var t = s.monthlyTiers[i];
            if (t == null) continue;
            if (t.Contains(today) && t.RequirementsSatisfied(ctx)) return i;
        }
        return -1;
    }

    /// <summary>
    /// 支給表の最後の toAge（covers があればその toAge も含めたいちばん遅い日）を過ぎたか。
    /// 支給表も covers も空の制度は終わらない。
    /// </summary>
    private static bool HasEnded(SupportScheme s, int today)
    {
        int last = -1;
        if (s.monthlyTiers != null)
        {
            foreach (var t in s.monthlyTiers)
            {
                if (t != null) last = Mathf.Max(last, t.toAge.ToAgeDays());
            }
        }
        if (s.covers != null)
        {
            foreach (var c in s.covers)
            {
                if (c != null) last = Mathf.Max(last, c.toAge.ToAgeDays());
            }
        }
        if (s.changesWorkMode)
            last = Mathf.Max(last, s.workModeUntil.ToAgeDays());
        if (HasWageLinkedTiers(s) && s.wageBenefitUntil.ToAgeDays() > 0)
            last = Mathf.Max(last, s.wageBenefitUntil.ToAgeDays());
        return last >= 0 && today > last;
    }

    /// <summary>支給表が空で covers だけを持つ制度か。</summary>
    private static bool IsCoversOnly(SupportScheme s)
    {
        return (s.monthlyTiers == null || s.monthlyTiers.Count == 0)
            && s.covers != null && s.covers.Count > 0;
    }

    /// <summary>
    /// 申請・再開したときに「どの歳まで手続き済み」とみなすか（support-apply.md 2.3・案C）。
    /// その歳の期限まで renewWindowDays より長く残っていれば、その歳の期限を最初の期限にする（＝前の歳まで済み）。
    /// 残りが短い・もう過ぎているなら、その歳は済みとして次の歳の期限を最初にする。
    /// </summary>
    private static int FirstRenewedYear(SupportScheme s, int today)
    {
        int year = YearOf(today);
        if (s.deadlineType != DeadlineType.Yearly) return year;
        int thisYearDeadline = YearlyDeadline(s, year);
        return (thisYearDeadline - today > s.renewWindowDays) ? year - 1 : year;
    }

    private static int YearlyDeadline(SupportScheme s, int year)
    {
        return GameDate.ToAgeDays(year, s.yearly.month, s.yearly.day);
    }

    private static int Today()
    {
        return GameState.I != null ? Mathf.FloorToInt(GameState.I.AgeDays) : 0;
    }

    private static int YearOf(int ageDays) => ageDays / GameDate.DaysPerYear;

    private static int MonthIndexOf(int ageDays)
    {
        GameDate.FromAgeDays(ageDays, out int y, out int m, out int d);
        return y * 12 + (m - 1);
    }

    private static int MoneyOf(List<Effect> effects)
    {
        float sum = 0f;
        if (effects != null)
        {
            foreach (var e in effects)
            {
                if (e != null && e.kind == EffectKind.Money) sum += e.value;
            }
        }
        return Mathf.RoundToInt(sum);
    }

    private static string FormatMoney(int money)
    {
        return money == 0 ? "" : $"お金 {money:+#,0;-#,0}";
    }

    // ------------------------------------------------------------
    // 動作確認用
    // ------------------------------------------------------------

    [ContextMenu("Debug/debugScheme を申請する")]
    private void DebugApply()
    {
        if (debugScheme == null) { Debug.LogWarning("SupportManager: debugScheme が空です"); return; }
        bool ok = Apply(debugScheme);
        Debug.Log($"[SupportManager] Apply({debugScheme.title}) = {ok}（現在 {GetState(debugScheme)}）");
    }

    [ContextMenu("Debug/全制度の状態をログに出す")]
    private void DebugDump()
    {
        foreach (var s in schemes)
        {
            if (!IsValid(s)) continue;
            Debug.Log($"[SupportManager] {s.title}（{s.id}）: {GetState(s)}");
        }
    }
}
