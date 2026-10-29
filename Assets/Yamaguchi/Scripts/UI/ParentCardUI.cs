using UnityEngine;
using UnityEngine.UI;

public class ParentCardUI : MonoBehaviour
{
    [SerializeField] private ParentRole role = ParentRole.Mother;
    [SerializeField] private Slider balanceSlider;
    [SerializeField] private StatusListUI statusList;
    [SerializeField] private Slider stressSlider;

    [Header("特性・状態")]
    [Tooltip("特性・状態の行を管理する PersonTraitRowsUI（任意）")]
    [SerializeField] private PersonTraitRowsUI traitRows;

    private ParentStatus parent;
    private bool subscribed;

    // Household.Awake より後に実行されるので Start で取得する
    private void Start()
    {
        parent = (Household.I != null) ? Household.I.Get(role) : null;
        if (parent == null)
        {
            Debug.LogWarning($"ParentCardUI: {role} が見つかりません。", this);
            return;
        }
        balanceSlider.SetValueWithoutNotify(parent.WorkLifeBalance);
        Subscribe();
        ShowSalary(parent.MonthlySalary);
        ShowDailyWage(parent.DailyWage);      // 追加
        ShowPending(parent.PendingWage);
        ShowStress(parent.Stress); //ストレスの表示
        ShowWorkMode(parent.WorkMode);

        // 特性行を parent に接続する
        traitRows?.Bind(parent);
    }

    private void OnEnable()  { if (parent != null) { Subscribe(); ShowWorkMode(parent.WorkMode); } }
    private void OnDisable() { if (parent != null) Unsubscribe(); }

    private void Subscribe()
    {
        if (subscribed) return;
        subscribed = true;
        balanceSlider.onValueChanged.AddListener(parent.SetBalance);
        parent.OnSalaryChanged      += ShowSalary;
        parent.OnDailyWageChanged   += ShowDailyWage;
        parent.OnPendingWageChanged += ShowPending;
        parent.OnStressChanged      += ShowStress;
        parent.OnWorkModeChanged    += ShowWorkMode;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        subscribed = false;
        balanceSlider.onValueChanged.RemoveListener(parent.SetBalance);
        parent.OnSalaryChanged      -= ShowSalary;
        parent.OnDailyWageChanged   -= ShowDailyWage;
        parent.OnPendingWageChanged -= ShowPending;
        parent.OnStressChanged      -= ShowStress;
        parent.OnWorkModeChanged    -= ShowWorkMode;
    }

    private void ShowSalary(int salary)
    {
        statusList.SetStatus("salary", "月給", $"{salary:N0}");
    }

    private void ShowDailyWage(int wage)
    {
        statusList.SetStatus("dailyWage", "日給", $"{wage:N0}");
    }

    private void ShowPending(float pending)
    {
        statusList.SetStatus("pendingWage", "積立", $"{pending:N0}");
    }

    private void ShowStress(int stress)
    {
        if (stressSlider != null)
            stressSlider.SetValueWithoutNotify(stress / 100f);
    }

    private void ShowWorkMode(WorkMode mode)
    {
        if (balanceSlider != null)
        {
            balanceSlider.interactable = mode == WorkMode.Normal;
            balanceSlider.SetValueWithoutNotify(parent.WorkLifeBalance);
        }
        string label = mode == WorkMode.MaternityLeave ? "産休中"
            : mode == WorkMode.ParentalLeave ? "育休中"
            : mode == WorkMode.ShortTime ? "時短勤務中" : "通常勤務";
        statusList?.SetStatus("workMode", "勤務", label);
    }
}