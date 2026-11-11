using System.Collections.Generic;
using Maruyama.Events;
using UnityEngine;

/// <summary>特性・状態の判定対象者。</summary>
public enum TraitTargetPerson
{
    子,
    母,
    父,
    家族のだれか
}

/// <summary>
/// 指定した対象が特性（永続）または状態（一時）を持っている／いないことを判定する条件。
/// </summary>
public class TraitCondition : EventConditionSO, IDescribable
{
    [Tooltip("判定する相手")]
    public TraitTargetPerson target = TraitTargetPerson.子;

    [Tooltip("判定する特性（永続）。状態を判定する場合は空にする")]
    public TraitDefinition trait;

    [Tooltip("判定する状態（一時）。特性を判定する場合は空にする")]
    public ConditionDefinition condition;

    [Tooltip("持っていることを期待するか（true = 持っている / false = 持っていない）")]
    public bool mustHave = true;

    public override bool IsSatisfied()
    {
        if (Household.I == null) return true;

        var targets = GetTargets();
        if (targets.Count == 0) return true;

        if (target == TraitTargetPerson.家族のだれか)
        {
            bool anyHas = targets.Exists(HasItem);
            return mustHave ? anyHas : !anyHas;
        }
        else
        {
            var p = targets[0];
            return HasItem(p) == mustHave;
        }
    }

    private bool HasItem(PersonStatus p)
    {
        if (p == null) return false;
        if (trait != null && p.Traits != null && p.Traits.Has(trait.id)) return true;
        if (condition != null && p.Conditions != null && p.Conditions.Has(condition.id)) return true;
        return false;
    }

    private List<PersonStatus> GetTargets()
    {
        var list = new List<PersonStatus>();
        var h = Household.I;
        if (h == null) return list;

        switch (target)
        {
            case TraitTargetPerson.子:
                if (h.Child != null) list.Add(h.Child);
                break;
            case TraitTargetPerson.母:
                if (h.Mother != null) list.Add(h.Mother);
                break;
            case TraitTargetPerson.父:
                if (h.Father != null) list.Add(h.Father);
                break;
            case TraitTargetPerson.家族のだれか:
                foreach (var m in h.Members)
                {
                    if (m != null) list.Add(m);
                }
                break;
        }
        return list;
    }

    public string Describe()
    {
        string name = trait != null ? trait.displayName : (condition != null ? condition.displayName : "（未指定）");
        string verb = mustHave ? "持っている" : "持っていない";
        return $"{target}が「{name}」を{verb}";
    }
}

