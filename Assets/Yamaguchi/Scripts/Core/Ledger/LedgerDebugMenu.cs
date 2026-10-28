using System.Text;
using UnityEngine;

/// <summary>家計簿の確認用。GameManager に付けて、Inspector の歯車（⋮）メニューから実行する。</summary>
public class LedgerDebugMenu : MonoBehaviour
{
    [ContextMenu("Debug/家計簿：今月と先月をログ")]
    private void LogThisAndLastMonth()
    {
        if (GameState.I == null) { Debug.LogWarning("LedgerDebugMenu: GameState がありません"); return; }
        int y, m, d;
        GameDate.FromAgeDays(GameState.I.AgeDays, out y, out m, out d);
        int now = y * 12 + m - 1;
        LogMonth("今月", now);
        if (now > 0) LogMonth("先月", now - 1);
    }

    [ContextMenu("Debug/家計簿：合計をログ")]
    private void LogTotals()
    {
        var sb = new StringBuilder("家計簿 合計\n");
        foreach (ExpenseCategory c in System.Enum.GetValues(typeof(ExpenseCategory)))
        {
            sb.AppendLine($"  {c}: {Ledger.I.TotalOf(c):N0}円");
        }
        sb.AppendLine($"子育てにかかったお金: {Ledger.I.TotalChildCost:N0}円");
        sb.AppendLine($"受け取った支援の合計: {Ledger.I.TotalSupport:N0}円");
        Debug.Log(sb.ToString());
    }

    private static void LogMonth(string label, int monthIndex)
    {
        var sb = new StringBuilder($"家計簿 {label}（{monthIndex / 12}歳{monthIndex % 12 + 1}月）\n");
        int income = 0, expense = 0, covered = 0;
        foreach (var l in Ledger.I.LinesOfMonth(monthIndex))
        {
            sb.AppendLine($"  [{l.category}] {l.source}: {l.amount:+#,0;-#,0;0}円" + (l.covered > 0 ? $"（支援で {l.covered:N0}円 助かった）" : ""));
            if (l.amount > 0) income += l.amount; else expense += -l.amount;
            covered += l.covered;
        }
        sb.AppendLine($"  収入 {income:N0} / 支出 {expense:N0} / 支援で助かった {covered:N0} / 差し引き {income - expense:+#,0;-#,0;0}");
        Debug.Log(sb.ToString());
    }
}
