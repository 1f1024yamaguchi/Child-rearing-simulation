using UnityEngine;

namespace Maruyama.Events
{
    /// <summary>
    /// 子どもの体調（0〜100、高い＝元気）が範囲内のときだけ発生を許す条件。
    /// 例：風邪は体調が低め（元気でない）ときだけ → minHealth = 0, maxHealth = 75。
    ///     体調が高い（体調不良ゲージが低い）のに風邪をひく、をなくすために使う。
    /// </summary>
    [CreateAssetMenu(fileName = "Cond_Health", menuName = "ChildRearing/Event Condition/Child Health")]
    public class ChildHealthCondition : EventConditionSO
    {
        [Tooltip("この体調以上で発生する（0〜100。高い＝元気）")]
        [Range(0, 100)] public int minHealth = 0;

        [Tooltip("この体調以下で発生する（0〜100。高い＝元気）")]
        [Range(0, 100)] public int maxHealth = 75;

        public override bool IsSatisfied()
        {
            // 世帯・子どもが無い環境（テストシーンなど）では絞り込まない
            if (Household.I == null || Household.I.Child == null) return true;

            int health = Household.I.Child.Health;
            int lo = Mathf.Min(minHealth, maxHealth);
            int hi = Mathf.Max(minHealth, maxHealth);
            return health >= lo && health <= hi;
        }
    }
}
