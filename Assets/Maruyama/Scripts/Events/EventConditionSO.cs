using UnityEngine;

namespace Maruyama.Events
{
    /// <summary>
    /// イベントの発生条件を表す ScriptableObject 抽象基底クラス。
    /// 年齢、季節、お金、確率などの条件判定を拡張可能にする。
    /// </summary>
    public abstract class EventConditionSO : ScriptableObject
    {
        public abstract bool IsSatisfied();
    }
}
