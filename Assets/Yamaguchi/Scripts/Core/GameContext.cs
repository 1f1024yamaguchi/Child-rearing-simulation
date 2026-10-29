/// <summary>
/// 申請条件の判定に使う値の入れ物（support-apply.md 4.2）。
/// 値の取り方が変わってもここだけ直せばよい。
/// 今後足すもの：子どもの人数、ひとり親か、住んでいる自治体、親の雇用形態…
/// </summary>
public class GameContext
{
    public int ChildAgeDays;   // 子どもの年齢（通算日数）← GameState.AgeDays
    public int AnnualIncome;   // 世帯の年収 ← IncomeHistory.AnnualIncome
    public int Money;          // 所持金 ← GameState.Money
    public WorkMode MotherWorkMode;
    public WorkMode FatherWorkMode;

    /// <summary>今のゲームから値を集めて返す。</summary>
    public static GameContext Current()
    {
        var ctx = new GameContext();
        if (GameState.I != null)
        {
            ctx.ChildAgeDays = UnityEngine.Mathf.FloorToInt(GameState.I.AgeDays);
            ctx.Money = GameState.I.Money;
        }
        ctx.AnnualIncome = IncomeHistory.I.AnnualIncome;
        if (Household.I != null)
        {
            if (Household.I.Mother != null) ctx.MotherWorkMode = Household.I.Mother.WorkMode;
            if (Household.I.Father != null) ctx.FatherWorkMode = Household.I.Father.WorkMode;
        }
        return ctx;
    }
}
