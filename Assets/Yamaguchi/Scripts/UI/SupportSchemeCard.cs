using System;
using System.Collections.Generic;
using Maruyama.Core;
using Maruyama.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 制度カード1枚の表示とボタン（support-apply.md 7.1〜7.2）。
/// Bind(scheme) で表示を始め、SupportManager.OnStateChanged / DayTicker で自分の表示だけ更新する。
/// カードを作り直さない。文字列は SupportScheduleText が作る。
/// </summary>
public class SupportSchemeCard : MonoBehaviour
{
    /// <summary>主ボタンの種類（状態で決まる）。</summary>
    public enum PrimaryAction { None, Apply, Renew, Resume, Return }

    [Header("基本")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descText;

    [Header("状態バッジ")]
    [SerializeField] private Image badgeImage;
    [SerializeField] private TMP_Text badgeText;

    [Header("効果チップ")]
    [SerializeField] private RectTransform chipRow;
    [SerializeField] private EffectChipUI chipPrefab;   // Assets/Maruyama/Prefabs/Events/EffectChip.prefab
    [SerializeField] private string monthlyPrefix = "毎月 ";
    [SerializeField] private float monthlyChipExtraWidth = 40f;

    [Header("行")]
    [SerializeField] private TMP_Text tierText;
    [SerializeField] private TMP_Text deadlineText;
    [SerializeField] private Color deadlineColor = new Color(0.25f, 0.25f, 0.25f);
    [SerializeField] private Color urgentColor = new Color(0.85f, 0.15f, 0.15f);
    [SerializeField, Min(0)] private int urgentDays = 7;

    [Header("ボタン")]
    [SerializeField] private Button primaryButton;      // 申請する／更新する／手続きする
    [SerializeField] private TMP_Text primaryLabel;
    [SerializeField] private Button calendarButton;     // カレンダーに追加（ステップ5まで Debug.Log のみ）

    [Header("バッジの色")]
    [SerializeField] private Color grayColor      = new Color(0.60f, 0.62f, 0.65f);
    [SerializeField] private Color orangeColor    = new Color(0.97f, 0.62f, 0.15f);
    [SerializeField] private Color greenColor     = new Color(0.25f, 0.65f, 0.35f);
    [SerializeField] private Color redOrangeColor = new Color(0.90f, 0.42f, 0.18f);
    [SerializeField] private Color redColor       = new Color(0.80f, 0.20f, 0.20f);

    /// <summary>主ボタンが押された。購読者がいなければカードが SupportManager を直接呼ぶ（確認ダイアログができたら購読側で差し替える）。</summary>
    public event Action<SupportScheme, PrimaryAction> OnPrimaryClicked;

    public SupportScheme Scheme { get; private set; }

    private PrimaryAction primaryAction = PrimaryAction.None;
    private bool subscribed;

    // ------------------------------------------------------------
    // ライフサイクル
    // ------------------------------------------------------------

    private void Awake()
    {
        // onClick はシリアライズされないので実行時に配線する
        if (primaryButton != null) primaryButton.onClick.AddListener(HandlePrimaryClicked);
        if (calendarButton != null) calendarButton.onClick.AddListener(HandleCalendarClicked);
    }

    private void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    private void Start()
    {
        Subscribe();   // OnEnable の時点で DayTicker / SupportManager が無かったとき用
    }

    private void OnDisable() => Unsubscribe();

    private void OnDestroy()
    {
        Unsubscribe();
        if (primaryButton != null) primaryButton.onClick.RemoveListener(HandlePrimaryClicked);
        if (calendarButton != null) calendarButton.onClick.RemoveListener(HandleCalendarClicked);
    }

    private void Subscribe()
    {
        if (subscribed) return;
        if (SupportManager.I == null || DayTicker.I == null) return;
        SupportManager.I.OnStateChanged += HandleStateChanged;
        DayTicker.I.OnDayPassed += HandleDayPassed;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (SupportManager.I != null) SupportManager.I.OnStateChanged -= HandleStateChanged;
        if (DayTicker.I != null) DayTicker.I.OnDayPassed -= HandleDayPassed;
        subscribed = false;
    }

    // ------------------------------------------------------------
    // 表示
    // ------------------------------------------------------------

    public void Bind(SupportScheme scheme)
    {
        Scheme = scheme;
        if (scheme == null) return;

        if (titleText != null) titleText.text = scheme.title;
        if (descText != null) descText.text = scheme.description;
        if (icon != null)
        {
            icon.sprite = scheme.icon;
            icon.enabled = scheme.icon != null;
        }

        BuildChips(scheme);   // 効果はアセットのデータなので、最初に1回だけ作る
        Subscribe();
        Refresh();
    }

    /// <summary>状態で変わる部分（バッジ・期限の行・支給表の行・ボタン）だけ更新する。</summary>
    public void Refresh()
    {
        if (Scheme == null || SupportManager.I == null) return;

        var mgr = SupportManager.I;
        var state = mgr.GetState(Scheme);

        if (badgeText != null) badgeText.text = SupportScheduleText.BadgeLabel(Scheme);
        if (badgeImage != null) badgeImage.color = BadgeColor(state);

        string tier = SupportScheduleText.TierLine(Scheme);
        if (tierText != null)
        {
            tierText.text = tier;
            tierText.gameObject.SetActive(!string.IsNullOrEmpty(tier));
        }

        if (deadlineText != null)
        {
            deadlineText.text = SupportScheduleText.DeadlineLine(Scheme);
            deadlineText.color = SupportScheduleText.IsUrgent(Scheme, urgentDays) ? urgentColor : deadlineColor;
        }

        UpdateButtons(mgr, state);
    }

    private void UpdateButtons(SupportManager mgr, SupportState state)
    {
        primaryAction = PrimaryAction.None;
        string label = "";

        if (mgr.CanReturn(Scheme))      { primaryAction = PrimaryAction.Return; label = Scheme.workMode == WorkMode.ShortTime ? "時短をやめる" : "復帰する"; }
        else if (mgr.CanApply(Scheme))  { primaryAction = PrimaryAction.Apply;  label = "申請する"; }
        else if (mgr.CanRenew(Scheme))  { primaryAction = PrimaryAction.Renew;  label = "更新する"; }
        else if (mgr.CanResume(Scheme)) { primaryAction = PrimaryAction.Resume; label = "手続きする"; }

        if (primaryButton != null) primaryButton.gameObject.SetActive(primaryAction != PrimaryAction.None);
        if (primaryLabel != null) primaryLabel.text = label;

        // カレンダーに予定を入れられるのは、まだこれから申請・手続きする制度だけ
        bool plannable = state == SupportState.Locked || state == SupportState.Available
                         || state == SupportState.Active || state == SupportState.Suspended;
        if (calendarButton != null)
        {
            calendarButton.gameObject.SetActive(plannable);
            var calLabel = calendarButton.GetComponentInChildren<TMP_Text>();
            if (calLabel != null)
            {
                bool isAdded = CalendarManager.I != null && CalendarManager.I.HasPlanForScheme(Scheme.id);
                calLabel.text = isAdded ? "追加済み ✓" : "カレンダーに追加";
            }
        }
    }

    private Color BadgeColor(SupportState state)
    {
        switch (state)
        {
            case SupportState.Available: return orangeColor;
            case SupportState.Received:
            case SupportState.Active:    return greenColor;
            case SupportState.Suspended: return redOrangeColor;
            case SupportState.Expired:
            case SupportState.Lost:      return redColor;
            default:                     return grayColor;   // Locked / Ended
        }
    }

    // ------------------------------------------------------------
    // 効果チップ（effects.md 5章）
    // ------------------------------------------------------------

    private void BuildChips(SupportScheme scheme)
    {
        if (chipRow == null || chipPrefab == null) return;

        for (int i = chipRow.childCount - 1; i >= 0; i--)
            Destroy(chipRow.GetChild(i).gameObject);

        // 申請したとき1回の効果（OnApply の本体／Monthly の手間のストレスなど）
        AddChips(scheme.effects, false);

        // Monthly は毎月の効果（支給表の最初の段）に「毎月」を付ける。段ごとの違いは支給表の行で見せる
        if (scheme.effectTiming == EffectTiming.Monthly && scheme.monthlyTiers != null)
        {
            foreach (var t in scheme.monthlyTiers)
            {
                if (t == null || t.monthlyEffects == null || t.monthlyEffects.Count == 0) continue;
                AddChips(t.monthlyEffects, true);
                break;
            }
        }
    }

    private void AddChips(List<Effect> effects, bool monthly)
    {
        if (effects == null) return;
        foreach (var e in effects)
        {
            if (e == null) continue;
            var chip = Instantiate(chipPrefab, chipRow);
            chip.SetEffect(e);

            if (!monthly) continue;
            var label = chip.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = monthlyPrefix + label.text;
            if (chip.transform is RectTransform rt)
                rt.sizeDelta = new Vector2(rt.sizeDelta.x + monthlyChipExtraWidth, rt.sizeDelta.y);
        }
    }

    // ------------------------------------------------------------
    // イベント
    // ------------------------------------------------------------

    private void HandleStateChanged(SupportScheme s, SupportState state)
    {
        if (s == Scheme) Refresh();
    }

    private void HandleDayPassed(int ageDays) => Refresh();   // 「あと○日」を進める

    private void HandlePrimaryClicked()
    {
        if (Scheme == null || primaryAction == PrimaryAction.None) return;

        if (OnPrimaryClicked != null)
        {
            OnPrimaryClicked.Invoke(Scheme, primaryAction);
            return;
        }

        var mgr = SupportManager.I;
        if (mgr == null) return;
        switch (primaryAction)
        {
            case PrimaryAction.Apply:  mgr.Apply(Scheme);  break;
            case PrimaryAction.Renew:  mgr.Renew(Scheme);  break;
            case PrimaryAction.Resume: mgr.Resume(Scheme); break;
            case PrimaryAction.Return: mgr.ReturnFromWorkMode(Scheme); break;
        }
    }

    private void HandleCalendarClicked()
    {
        if (Scheme == null) return;

        var mgr = SupportManager.I;
        int deadline = (mgr != null) ? mgr.NextDeadlineAgeDays(Scheme) : -1;
        if (deadline < 0)
        {
            if (Scheme.deadlineType == DeadlineType.Once)
            {
                deadline = Scheme.DeadlineAgeDays;
            }
            else
            {
                int today = (GameState.I != null) ? Mathf.FloorToInt(GameState.I.AgeDays) : 0;
                deadline = Mathf.Max(Scheme.StartAgeDays, today);
            }
        }

        var kind = Scheme.deadlineType == DeadlineType.Yearly ? PlanKind.Renew : PlanKind.Apply;
        CalendarUI.AddPlanFromScheme(Scheme, deadline, kind);

        // トースト通知（あれば）
        var toast = FindObjectOfType<SupportToast>();
        if (toast != null)
        {
            toast.Show($"カレンダーに「{Scheme.title}」の期限を追加しました");
        }
        else
        {
            Debug.Log($"[SupportSchemeCard] カレンダーに追加：{Scheme.title} (期限: {GameDate.Format(deadline)})");
        }

        Refresh();
    }
}
