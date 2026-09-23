using System.Collections.Generic;
using UnityEngine;

public enum ParentRole { Mother, Father }

/// 家族のメンバーをまとめて持つ。誰がどこにいるかを知る唯一の場所。
public class Household : MonoBehaviour
{
    public static Household I { get; private set; }

    [SerializeField] private ParentStatus mother;
    [SerializeField] private ParentStatus father;
    [Tooltip("子どものステータスコンポーネント")]
    [SerializeField] private ChildStatus child;

    public ParentStatus Mother => mother;
    public ParentStatus Father => father;
    /// 子どものステータス。
    public ChildStatus Child => child;

    private void Awake() { I = this; }

    public ParentStatus Get(ParentRole role)
    {
        return role == ParentRole.Mother ? mother : father;
    }

    /// 親だけの列挙（null を除く）。
    public IEnumerable<ParentStatus> Parents
    {
        get
        {
            if (mother != null) yield return mother;
            if (father != null) yield return father;
        }
    }

    /// 親＋子の全員の列挙（null を除く）。
    public IEnumerable<PersonStatus> Members
    {
        get
        {
            if (mother != null) yield return mother;
            if (father != null) yield return father;
            if (child  != null) yield return child;
        }
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

    /// 親2人のライフ⇔ワークバランスの平均（0=ライフ / 1=ワーク）。
    /// ChildGrowthSystem が毎日の成長計算で使う。
    public float AverageParentWorkLifeBalance
    {
        get
        {
            float sum = 0f;
            int count = 0;
            if (mother != null) { sum += mother.WorkLifeBalance; count++; }
            if (father != null) { sum += father.WorkLifeBalance; count++; }
            return count > 0 ? sum / count : 0.5f;
        }
    }

    /// 親2人のストレスの平均（0〜100）。
    /// ChildGrowthSystem が毎日の成長計算で使う。
    public float AverageParentStress
    {
        get
        {
            float sum = 0f;
            int count = 0;
            if (mother != null) { sum += mother.Stress; count++; }
            if (father != null) { sum += father.Stress; count++; }
            return count > 0 ? sum / count : 0f;
        }
    }
}