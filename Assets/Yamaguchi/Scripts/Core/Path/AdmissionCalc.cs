/// <summary>
/// 合格率と模試の判定（ending.md 3.5）。画面・セーブ・時間には触らない純粋なクラス。
/// 合格率 = Clamp(基準 + (成績 − ライン) × 傾き, 下限, 上限)
/// </summary>
public static class AdmissionCalc
{
    /// <summary>模試の判定の境目（表示用。抽選は合格率そのもの）。A,B,C,D の下限。</summary>
    public static float[] RankLowerBounds = { 0.8f, 0.6f, 0.4f, 0.2f };
    private static readonly string[] Ranks = { "A", "B", "C", "D", "E" };

    public static float PassRate(int grade, PathDefinition p)
    {
        if (p == null) return 0f;
        if (p.kind == PathKind.就職) return 1f;
        float r = p.baseRate + (grade - p.passLine) * p.slope;
        if (r < p.minRate) r = p.minRate;
        if (r > p.maxRate) r = p.maxRate;
        return r;
    }

    public static string MockRank(float rate)
    {
        for (int i = 0; i < RankLowerBounds.Length; i++)
            if (rate >= RankLowerBounds[i]) return Ranks[i];
        return Ranks[Ranks.Length - 1];
    }

    /// <summary>推薦に出願できるか。できなければ reason に理由。</summary>
    public static bool CanApplyRecommendation(ChildStatus c, AcademicRecord r, PathDefinition p, out string reason)
    {
        reason = "";
        if (c == null || p == null) { reason = "子ども・進路がありません"; return false; }
        if (c.Hobby == HobbyKind.なし || (p.requiredHobby != HobbyKind.なし && c.Hobby != p.requiredHobby))
        { reason = p.requiredHobby == HobbyKind.なし ? "趣味がない" : $"趣味が{p.requiredHobby}ではない"; return false; }
        if (c.HobbyLevel < p.requiredHobbyLevel) { reason = $"趣味Lv{p.requiredHobbyLevel}に届いていない"; return false; }
        int h = r != null ? r.Hyotei : 0;
        if (h < p.requiredHyotei) { reason = $"評定{p.requiredHyotei}に届いていない（いま{h}）"; return false; }
        return true;
    }
}
