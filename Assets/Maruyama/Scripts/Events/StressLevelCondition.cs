using UnityEngine;

namespace Maruyama.Events
{
    /// <summary>ストレスを見る相手。</summary>
    public enum StressSource
    {
        Child,           // 子ども
        Mother,          // 母親
        Father,          // 父親
        ParentsAverage,  // 親2人の平均
    }

    /// <summary>
    /// ストレスの段階（良好・普通・危険・限界）が範囲内のときだけ発生を許す条件。
    /// 例：子のストレスが「危険」以上のときだけ反抗期 → source = Child, minLevel = 危険, maxLevel = 限界。
    ///     ストレスが「良好」のときだけ出る良いイベント → minLevel = 良好, maxLevel = 良好。
    /// </summary>
    [CreateAssetMenu(fileName = "Cond_Stress", menuName = "ChildRearing/Event Condition/Stress Level")]
    public class StressLevelCondition : EventConditionSO
    {
        [Tooltip("ストレスを見る相手")]
        public StressSource source = StressSource.Child;

        [Tooltip("この段階以上で発生する")]
        public StressLevel minLevel = StressLevel.良好;

        [Tooltip("この段階以下で発生する")]
        public StressLevel maxLevel = StressLevel.限界;

        public override bool IsSatisfied()
        {
            // 世帯が無い環境（テストシーンなど）では絞り込まない
            if (Household.I == null) return true;

            if (!TryGetStress(out int stress)) return true;

            int level = (int)LevelOf(stress);
            int lo = Mathf.Min((int)minLevel, (int)maxLevel);
            int hi = Mathf.Max((int)minLevel, (int)maxLevel);
            return level >= lo && level <= hi;
        }

        private bool TryGetStress(out int stress)
        {
            stress = 0;
            var h = Household.I;

            switch (source)
            {
                case StressSource.Child:
                    if (h.Child == null) return false;
                    stress = h.Child.Stress;
                    return true;
                case StressSource.Mother:
                    if (h.Mother == null) return false;
                    stress = h.Mother.Stress;
                    return true;
                case StressSource.Father:
                    if (h.Father == null) return false;
                    stress = h.Father.Stress;
                    return true;
                case StressSource.ParentsAverage:
                    if (h.Mother == null && h.Father == null) return false;
                    stress = Mathf.RoundToInt(h.AverageParentStress);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 整数のストレス値を段階に直す。区切りは PersonStatus.StressLevel と同じ
        /// （0–30 良好 / 31–70 普通 / 71–90 危険 / 91–100 限界。rules-and-stats.md）。
        /// 親の平均値にも使えるよう、ここで同じ区切りを持つ。区切りを変えるときは PersonStatus も直すこと。
        /// </summary>
        private static StressLevel LevelOf(int stress)
        {
            if (stress <= 30) return StressLevel.良好;
            if (stress <= 70) return StressLevel.普通;
            if (stress <= 90) return StressLevel.危険;
            return StressLevel.限界;
        }
    }
}
