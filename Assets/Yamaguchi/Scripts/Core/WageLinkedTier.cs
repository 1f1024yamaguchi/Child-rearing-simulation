using UnityEngine;

public enum WageBase
{
    SalaryBeforeLeave,
    CurrentWage,
}

[System.Serializable]
public class WageLinkedTier
{
    public string label;
    [Min(1)] public int fromDay = 1;
    [Min(-1)] public int toDay = -1;
    [Range(0f, 1f)] public float rate = 0.67f;
    public WageBase basis = WageBase.SalaryBeforeLeave;

    public bool Contains(int day) => day >= fromDay && (toDay < 0 || day <= toDay);
}
