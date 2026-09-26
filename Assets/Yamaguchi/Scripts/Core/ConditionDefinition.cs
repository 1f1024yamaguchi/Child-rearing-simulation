using System.Collections.Generic;
using UnityEngine;

/// 状態（一時的な効果）の定義アセット。
/// TraitDefinition に加えて「既定の持続日数・バッジ色・優先度・毎日の効果」を持つ。
/// traits.md 5章。
[CreateAssetMenu(menuName = "ChildRearing/ConditionDefinition", fileName = "Condition_New")]
public class ConditionDefinition : ScriptableObject
{
    [Tooltip("プログラム用の識別子。英数字推奨。他の状態と重複しないこと")]
    public string id;

    [Tooltip("UI に表示する名前（例：風邪気味）")]
    public string displayName;

    [Tooltip("説明文")]
    [TextArea]
    public string description;

    [Tooltip("この状態が持つ補正のリスト（伸び方を変える。プラスのときだけ効く）")]
    public List<StatModifier> modifiers = new List<StatModifier>();

    [Tooltip("この状態が毎日1回だけ直接加算する効果のリスト（値を直接動かす）")]
    public List<DailyEffect> dailyEffects = new List<DailyEffect>();

    [Tooltip("既定の持続日数。AddCondition で days を省略したときに使う")]
    public int defaultDays = 7;

    [Tooltip("将来カードに出すときのバッジ色")]
    public Color badgeColor = Color.yellow;

    [Tooltip("複数の状態が付いているとき、表に出す優先度（大きいほど上）")]
    public int priority = 0;
}
