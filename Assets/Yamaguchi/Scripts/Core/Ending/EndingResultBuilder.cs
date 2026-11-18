using UnityEngine;

/// <summary>
/// Main シーンにある値から EndingResult を作る（result-scene.md 3章）。計算はここに1か所。
/// 見つからない部品の項目は 0・空にして警告を出す。
/// </summary>
public static class EndingResultBuilder
{
    /// <summary>働き方の区切り（review-records.md 3.4。たたき台）。</summary>
    public const float LifeSide = 0.35f, WorkSide = 0.65f;

    public static EndingResult Build(GameEndReason reason)
    {
        var r = new EndingResult
        {
            reason = reason,
            finishedAt = System.DateTime.Now.ToString("yyyy/MM/dd HH:mm"),
        };

        var gs = GameState.I;
        if (gs != null)
        {
            r.endAgeDays = Mathf.FloorToInt(gs.AgeDays);
            r.finalMoney = gs.Money;
        }
        else Debug.LogWarning("[EndingResultBuilder] GameState がありません。");

        // お金（Ledger は static なのでシーンが変わっても残るが、ここで写しておく）
        var ledger = Ledger.I;
        r.totalChildCost = ledger.TotalChildCost;
        r.totalSupport = ledger.TotalSupport;
        int lastAge = r.endAgeDays / 365;
        for (int age = 0; age <= lastAge; age++) r.totalIncome += ledger.YearSummary(age).income;

        // 子ども
        var child = Household.I != null ? Household.I.Child : null;
        r.childName = StoryTextFormatter.ChildName;
        if (child != null)
        {
            r.grade = child.Grade;
            r.health = child.Health;
            r.childStress = child.Stress;
            r.hobby = child.Hobby == HobbyKind.なし ? "" : HobbyDisplayName.Get(child.Hobby);
            r.hobbyLevel = child.HobbyLevel;
            foreach (var t in child.Traits.All)
                if (t != null) r.traits.Add(string.IsNullOrEmpty(t.displayName) ? t.id : t.displayName);
        }
        else Debug.LogWarning("[EndingResultBuilder] 子どもが見つかりません。");

        // 親
        if (Household.I != null)
        {
            r.mother = ParentOf(Household.I.Mother);
            r.father = ParentOf(Household.I.Father);
        }

        // 将来の夢・なった職業
        var ds = DreamSystem.I;
        if (ds != null && ds.HasDream)
        {
            r.dreamId = ds.Dream.id;
            r.dreamName = ds.Dream.DisplayNameOrName;
        }
        if (reason == GameEndReason.GoalReached && ds != null)
        {
            // 進路の職業の候補から判定する（ending.md 5.1）。進路に候補が無ければ全部の職業から（夢と同じ）
            var path = PathSystem.I != null ? PathSystem.I.CareerPath : null;
            bool hasCandidates = path != null && path.careers != null && path.careers.Count > 0;
            var fallback = path != null && path.fallbackCareer != null ? path.fallbackCareer : ds.Fallback;
            var career = CareerJudge.Judge(hasCandidates ? (System.Collections.Generic.IEnumerable<CareerDefinition>)path.careers : ds.AllCareers,
                                           fallback, CareerJudge.Input.From(child, GameContext.Current()));
            if (career != null)
            {
                r.careerId = career.id;
                r.careerName = career.DisplayNameOrName;
                r.epilogue = career.epilogue ?? "";
            }
        }

        // きっかけ
        if (reason == GameEndReason.Runaway)
        {
            var rw = Object.FindFirstObjectByType<RunawayWatcher>();
            if (rw != null) r.causeDays = rw.DangerDays;
            r.causeValue = r.childStress;
        }
        else if (reason == GameEndReason.Bankrupt)
        {
            var bw = Object.FindFirstObjectByType<BankruptcyWatcher>();
            if (bw != null) r.causeDays = bw.DeficitMonths;
            r.causeValue = -r.finalMoney;
        }
        return r;
    }

    private static EndingResult.ParentResult ParentOf(ParentStatus p)
    {
        var pr = new EndingResult.ParentResult();
        if (p == null) return pr;
        pr.exists = true;
        pr.rank = p.Rank.ToString();
        pr.salary = p.MonthlySalary;
        pr.stress = p.Stress;
        // 本来は月の記録の平均（review-records.md 3.4）。LifeRecorder ができるまでは終わった日の値
        float w = p.WorkLifeBalance;
        pr.workStyle = w <= LifeSide ? "ライフ寄り" : w >= WorkSide ? "ワーク寄り" : "半々";
        return pr;
    }
}
