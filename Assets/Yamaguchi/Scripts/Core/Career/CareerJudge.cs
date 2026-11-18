using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 職業の判定だけを行う純粋なクラス（ending.md 5.1）。画面・セーブ・時間には触らない。
/// 将来の夢（中1・全部の職業から）と巣立ちの日（進路の候補から）で同じ判定を使い回す。
///
/// 点数 = 成績×a + 趣味Lv×b（趣味の種類が合うときだけ）+ 体調×c + 特性の加点
/// 子どものストレスは点数に入れない（ending.md 5.1 で決定）。
/// 中学・高校の学期末テストの不合格回数（AcademicRecord.FailCount）も条件に使える（maxFailCount）。
/// </summary>
public static class CareerJudge
{
    /// <summary>判定に使う子どもの値。ChildStatus から作るか、確認用に手で作る。</summary>
    public struct Input
    {
        public int grade;
        public int health;
        public HobbyKind hobby;
        public int hobbyLevel;
        public List<string> traitIds;   // 持っている特性の id
        public int failCount;           // 中学・高校の学期末テストで不合格だった回数（AcademicRecord.FailCount）
        public GameContext context;     // requirements の判定用（null なら requirements は満たしたものとする）

        public static Input From(ChildStatus child, GameContext ctx)
        {
            var input = new Input { traitIds = new List<string>(), context = ctx };
            if (child == null) return input;
            input.grade = child.Grade;
            input.health = child.Health;
            input.hobby = child.Hobby;
            input.hobbyLevel = child.HobbyLevel;
            if (TermTestSystem.I != null) input.failCount = TermTestSystem.I.Record.FailCount;
            if (child.Traits != null)
            {
                foreach (var t in child.Traits.All)
                {
                    if (t != null && !string.IsNullOrEmpty(t.id)) input.traitIds.Add(t.id);
                }
            }
            return input;
        }

        public bool HasTrait(TraitDefinition def)
        {
            return def != null && traitIds != null && traitIds.Contains(def.id);
        }
    }

    /// <summary>条件を満たすか。</summary>
    public static bool MeetsRequirements(CareerDefinition c, Input input)
    {
        if (c == null) return false;
        if (c.minGrade > 0 && input.grade < c.minGrade) return false;
        if (c.minHealth > 0 && input.health < c.minHealth) return false;
        if (c.maxFailCount >= 0 && input.failCount > c.maxFailCount) return false;
        if (c.minHobbyLevel > 0)
        {
            if (c.hobby == HobbyKind.なし) return false; // 種類が無いのに Lv を求めている（データの誤り）
            if (input.hobby != c.hobby || input.hobbyLevel < c.minHobbyLevel) return false;
        }
        if (c.requirements != null && input.context != null)
        {
            foreach (var r in c.requirements)
            {
                if (r != null && !r.IsSatisfied(input.context)) return false;
            }
        }
        return true;
    }

    /// <summary>点数。条件を満たさなければ float.NegativeInfinity。</summary>
    public static float Score(CareerDefinition c, Input input)
    {
        if (!MeetsRequirements(c, input)) return float.NegativeInfinity;

        float score = input.grade * c.gradeWeight + input.health * c.healthWeight;
        if (c.hobby != HobbyKind.なし && input.hobby == c.hobby) score += input.hobbyLevel * c.hobbyWeight;
        if (c.traitBonuses != null)
        {
            foreach (var b in c.traitBonuses)
            {
                if (b != null && input.HasTrait(b.trait)) score += b.bonus;
            }
        }
        return score;
    }

    /// <summary>
    /// 候補のうち点数が一番高い職業。同点は候補リストの先の方。
    /// 条件を満たす候補が無ければ fallback（会社員）。fallback 自身は候補にあっても点数を比べない。
    /// </summary>
    public static CareerDefinition Judge(IEnumerable<CareerDefinition> candidates, CareerDefinition fallback, Input input)
    {
        CareerDefinition best = null;
        float bestScore = float.NegativeInfinity;
        if (candidates != null)
        {
            foreach (var c in candidates)
            {
                if (c == null || c == fallback || c.isFallback) continue;
                float s = Score(c, input);
                if (float.IsNegativeInfinity(s)) continue;
                if (best == null || s > bestScore)
                {
                    best = c;
                    bestScore = s;
                }
            }
        }
        return best != null ? best : fallback;
    }

    /// <summary>確認用：全候補の点数を1行ずつ（高い順）。</summary>
    public static string Explain(IEnumerable<CareerDefinition> candidates, Input input)
    {
        var rows = new List<(string name, float score)>();
        if (candidates != null)
        {
            foreach (var c in candidates)
            {
                if (c == null) continue;
                rows.Add((c.DisplayNameOrName + (c.isFallback ? "（既定）" : ""), c.isFallback ? float.NaN : Score(c, input)));
            }
        }
        rows.Sort((a, b) => b.score.CompareTo(a.score));
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"成績{input.grade} 体調{input.health} 趣味{input.hobby} Lv{input.hobbyLevel} 特性{(input.traitIds != null ? input.traitIds.Count : 0)}個");
        foreach (var r in rows)
        {
            string s = float.IsNaN(r.score) ? "—" : float.IsNegativeInfinity(r.score) ? "条件を満たさない" : r.score.ToString("0.#");
            sb.AppendLine($"  {r.name}: {s}");
        }
        return sb.ToString();
    }
}
