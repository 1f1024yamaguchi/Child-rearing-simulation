using System.Collections.Generic;
using UnityEngine;

/// 特性（永続の特殊能力）の定義アセット。
/// ScriptableObject にして、1アセット = 1特性として増やせるようにする。
/// traits.md 5章・effects.md 5.5章。
[CreateAssetMenu(menuName = "ChildRearing/TraitDefinition", fileName = "Trait_New")]
public class TraitDefinition : ScriptableObject
{
    [Tooltip("プログラム用の識別子。英数字推奨。他の特性と重複しないこと")]
    public string id;

    [Tooltip("UI に表示する名前（例：勉強が得意）")]
    public string displayName;

    [Tooltip("説明文")]
    [TextArea]
    public string description;

    [Tooltip("この特性が持つ補正のリスト")]
    public List<StatModifier> modifiers = new List<StatModifier>();
}
