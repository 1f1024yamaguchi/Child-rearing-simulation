using System.Collections.Generic;

/// <summary>
/// デバッグ用。支援制度の「申請できる」「毎年の手続き」「支給停止からの再開」を、プレイヤーの代わりに行う。
/// 操作はすべて SupportManager の窓口（Apply / Renew / Resume）を通す。
/// </summary>
public static class DebugSupportAutoApplier
{
    private static readonly List<SupportScheme> buffer = new List<SupportScheme>();

    /// <param name="includeWorkModeChange">育休・時短など、親の働き方が変わる制度も申請するか</param>
    /// <returns>行った操作の数</returns>
    public static int Run(bool includeWorkModeChange)
    {
        var sm = SupportManager.I;
        if (sm == null || Household.I == null) return 0;

        // 申請で状態が変わっても回している途中のリストが変わらないよう、写しを回す
        buffer.Clear();
        buffer.AddRange(sm.Schemes);

        int count = 0;
        foreach (var s in buffer)
        {
            if (s == null) continue;

            if (sm.CanApply(s))
            {
                if ((includeWorkModeChange || !s.changesWorkMode) && sm.Apply(s)) count++;
            }
            else if (sm.CanRenew(s))
            {
                if (sm.Renew(s)) count++;
            }
            else if (sm.CanResume(s))
            {
                if (sm.Resume(s)) count++;
            }
        }
        return count;
    }
}
