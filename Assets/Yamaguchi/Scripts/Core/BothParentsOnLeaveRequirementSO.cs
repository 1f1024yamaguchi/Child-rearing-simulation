using UnityEngine;

[CreateAssetMenu(menuName = "子育て/申請条件/両親が育休中")]
public class BothParentsOnLeaveRequirementSO : RequirementSO
{
    public override bool IsSatisfied(GameContext ctx)
    {
        return ctx != null
            && ctx.MotherWorkMode == WorkMode.ParentalLeave
            && ctx.FatherWorkMode == WorkMode.ParentalLeave;
    }

    public override string Describe() => "両親とも育児休業中";
}
