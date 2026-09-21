using UnityEngine;
using UnityEngine.UI;

public class ParentCardUI : MonoBehaviour
{
    [SerializeField] private ParentRole role = ParentRole.Mother;
    [SerializeField] private Slider balanceSlider;
    [SerializeField] private StatusListUI statusList;
    [SerializeField] private Slider stressSlider;

    private ParentStatus parent;

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
    }

    private void OnEnable()  { if (parent != null) Subscribe(); }
    private void OnDisable() { if (parent != null) Unsubscribe(); }

    private void Subscribe()
    {
        balanceSlider.onValueChanged.AddListener(parent.SetBalance);
        parent.OnSalaryChanged += ShowSalary;
        parent.OnDailyWageChanged  += ShowDailyWage;
        parent.OnPendingWageChanged += ShowPending;
        parent.OnStressChanged += ShowStress;
    }

    private void Unsubscribe()
    {
        balanceSlider.onValueChanged.RemoveListener(parent.SetBalance);
        parent.OnSalaryChanged -= ShowSalary;
        parent.OnDailyWageChanged  -= ShowDailyWage;
        parent.OnPendingWageChanged -= ShowPending;
        parent.OnStressChanged -= ShowStress;

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
        if(stressSlider != null)
        {
            stressSlider.SetValueWithoutNotify(stress / 100f);
        }
    }
}