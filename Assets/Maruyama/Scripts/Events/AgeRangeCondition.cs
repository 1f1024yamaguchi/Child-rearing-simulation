using UnityEngine;

namespace Maruyama.Events
{
    /// <summary>
    /// 子どもの年齢（満年齢）が範囲内のときだけ発生を許す条件。
    /// 例：0〜5歳 → minAgeYears = 0, maxAgeYears = 5（6歳の誕生日の前日まで）。
    /// </summary>
    [CreateAssetMenu(fileName = "Cond_Age", menuName = "ChildRearing/Event Condition/Age Range")]
    public class AgeRangeCondition : EventConditionSO
    {
        [Tooltip("この歳以上で発生する（満年齢）")]
        [Min(0)] public int minAgeYears = 0;

        [Tooltip("この歳以下で発生する（満年齢。5 なら 5歳11か月まで）")]
        [Min(0)] public int maxAgeYears = 5;

        public override bool IsSatisfied()
        {
            // 年齢が分からない環境（テストシーンなど）では絞り込まない
            if (GameState.I == null) return true;

            GameDate.FromAgeDays(GameState.I.AgeDays, out int years, out _, out _);

            int lo = Mathf.Min(minAgeYears, maxAgeYears);
            int hi = Mathf.Max(minAgeYears, maxAgeYears);
            return years >= lo && years <= hi;
        }
    }
}
