using UnityEngine;

public enum ParentRole { Mother, Father }

/// 家族のメンバーをまとめて持つ。誰がどこにいるかを知る唯一の場所。
public class Household : MonoBehaviour
{
    public static Household I { get; private set; }

    [SerializeField] private ParentStatus mother;
    [SerializeField] private ParentStatus father;

    public ParentStatus Mother => mother;
    public ParentStatus Father => father;

    private void Awake() { I = this; }

    public ParentStatus Get(ParentRole role)
    {
        return role == ParentRole.Mother ? mother : father;
    }

    /// 稼ぎ手全員の月給の合計
    public int TotalMonthlySalary
    {
        get
        {
            int sum = 0;
            if (mother != null) sum += mother.MonthlySalary;
            if (father != null) sum += father.MonthlySalary;
            return sum;
        }
    }
}