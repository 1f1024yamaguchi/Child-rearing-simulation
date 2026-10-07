using UnityEngine;

/// <summary>
/// 申請条件の部品（support-apply.md 4.1）。新しい条件はこれを継承したクラスを1つ足すだけ。
/// ※「Condition」は状態（風邪気味など）の意味で使われているため Requirement と呼ぶ。
/// </summary>
public abstract class RequirementSO : ScriptableObject
{
    /// <summary>条件を満たしているか。</summary>
    public abstract bool IsSatisfied(GameContext ctx);

    /// <summary>カードに出す説明。例 "年収 960万円以下"</summary>
    public abstract string Describe();
}
