using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 家族全員（母・父・子ども）のステータス、役職、バランス、積立給与、特性、状態のセーブ・ロードを担当する。
/// GameManager に付ける。
/// </summary>
public class FamilySaveParticipant : MonoBehaviour, ISaveParticipant
{
    public string SaveKey => "family";
    public int RestoreOrder => 10;

    private void OnEnable() => SaveSystem.Register(this);
    private void OnDisable() => SaveSystem.Unregister(this);

    [Serializable]
    private class SavedCondition
    {
        public string id;
        public int remainingDays;
    }

    [Serializable]
    private class SavedParent
    {
        public float stress;
        public int rank;
        public float workLifeBalance;
        public int workMode;
        public float savedWorkLifeBalance;
        public int salaryBeforeLeave;
        public float pendingWage;
        public List<string> traits = new List<string>();
        public List<SavedCondition> conditions = new List<SavedCondition>();
    }

    [Serializable]
    private class SavedChild
    {
        public float stress;
        public float grade;
        public float health;
        public float studyPlayBalance;
        public int hobby;
        public float hobbyExp;
        public List<string> traits = new List<string>();
        public List<SavedCondition> conditions = new List<SavedCondition>();
    }

    [Serializable]
    private class FamilySaveData
    {
        public SavedParent mother = new SavedParent();
        public SavedParent father = new SavedParent();
        public SavedChild child = new SavedChild();
    }

    public string Capture()
    {
        var data = new FamilySaveData();
        var h = Household.I;
        if (h != null)
        {
            if (h.Mother != null) data.mother = CaptureParent(h.Mother);
            if (h.Father != null) data.father = CaptureParent(h.Father);
            if (h.Child != null) data.child = CaptureChild(h.Child);
        }
        return JsonUtility.ToJson(data);
    }

    private SavedParent CaptureParent(ParentStatus parent)
    {
        var p = new SavedParent
        {
            stress = parent.Stress,
            rank = (int)parent.Rank,
            workLifeBalance = parent.WorkLifeBalance,
            workMode = (int)parent.WorkMode,
            savedWorkLifeBalance = parent.SavedWorkLifeBalance,
            salaryBeforeLeave = parent.SalaryBeforeLeave,
            pendingWage = parent.PendingWage
        };
        if (parent.Traits != null)
        {
            foreach (var t in parent.Traits.All)
            {
                if (t != null && !string.IsNullOrEmpty(t.id))
                    p.traits.Add(t.id);
            }
        }
        if (parent.Conditions != null)
        {
            foreach (var c in parent.Conditions.All)
            {
                if (c.definition != null && !string.IsNullOrEmpty(c.definition.id))
                {
                    p.conditions.Add(new SavedCondition { id = c.definition.id, remainingDays = c.remainingDays });
                }
            }
        }
        return p;
    }

    private SavedChild CaptureChild(ChildStatus child)
    {
        var c = new SavedChild
        {
            stress = child.Stress,
            grade = child.Grade,
            health = child.Health,
            studyPlayBalance = child.StudyPlayBalance,
            hobby = (int)child.Hobby,
            hobbyExp = child.HobbyExp
        };
        if (child.Traits != null)
        {
            foreach (var t in child.Traits.All)
            {
                if (t != null && !string.IsNullOrEmpty(t.id))
                    c.traits.Add(t.id);
            }
        }
        if (child.Conditions != null)
        {
            foreach (var cond in child.Conditions.All)
            {
                if (cond.definition != null && !string.IsNullOrEmpty(cond.definition.id))
                {
                    c.conditions.Add(new SavedCondition { id = cond.definition.id, remainingDays = cond.remainingDays });
                }
            }
        }
        return c;
    }

    public void Restore(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        var data = JsonUtility.FromJson<FamilySaveData>(json);
        if (data == null) return;

        var h = Household.I;
        if (h == null)
        {
            Debug.LogWarning("[FamilySaveParticipant] Household.I が見つからないため復元できません。");
            return;
        }

        var catalog = DefinitionCatalog.Load();
        if (catalog == null)
        {
            Debug.LogWarning("[FamilySaveParticipant] DefinitionCatalog が読み込めないため、特性・状態の復元をスキップします。");
        }

        if (h.Mother != null && data.mother != null)
            RestoreParent(h.Mother, data.mother, catalog, "母");

        if (h.Father != null && data.father != null)
            RestoreParent(h.Father, data.father, catalog, "父");

        if (h.Child != null && data.child != null)
            RestoreChild(h.Child, data.child, catalog);
    }

    private void RestoreParent(ParentStatus parent, SavedParent data, DefinitionCatalog catalog, string roleName)
    {
        parent.SetStress(data.stress);
        parent.SetRank((JobRank)data.rank);
        parent.RestoreWorkMode((WorkMode)data.workMode, data.savedWorkLifeBalance, data.salaryBeforeLeave);
        parent.SetBalance(data.workLifeBalance);
        parent.RestorePendingWage(data.pendingWage);

        var traitList = new List<TraitDefinition>();
        if (catalog != null && data.traits != null)
        {
            foreach (var id in data.traits)
            {
                var def = catalog.FindTrait(id);
                if (def != null) traitList.Add(def);
                else Debug.LogWarning($"[FamilySaveParticipant] {roleName}の特性 id \"{id}\" がカタログに見つかりません。スキップします。");
            }
        }
        parent.RestoreTraits(traitList);

        var conditionList = new List<(ConditionDefinition def, int remainingDays)>();
        if (catalog != null && data.conditions != null)
        {
            foreach (var c in data.conditions)
            {
                var def = catalog.FindCondition(c.id);
                if (def != null) conditionList.Add((def, c.remainingDays));
                else Debug.LogWarning($"[FamilySaveParticipant] {roleName}の状態 id \"{c.id}\" がカタログに見つかりません。スキップします。");
            }
        }
        parent.RestoreConditions(conditionList);
    }

    private void RestoreChild(ChildStatus child, SavedChild data, DefinitionCatalog catalog)
    {
        child.SetStress(data.stress);
        child.RestoreValues(data.grade, data.health, data.studyPlayBalance, (HobbyKind)data.hobby, data.hobbyExp);

        var traitList = new List<TraitDefinition>();
        if (catalog != null && data.traits != null)
        {
            foreach (var id in data.traits)
            {
                var def = catalog.FindTrait(id);
                if (def != null) traitList.Add(def);
                else Debug.LogWarning($"[FamilySaveParticipant] 子どもの特性 id \"{id}\" がカタログに見つかりません。スキップします。");
            }
        }
        child.RestoreTraits(traitList);

        var conditionList = new List<(ConditionDefinition def, int remainingDays)>();
        if (catalog != null && data.conditions != null)
        {
            foreach (var c in data.conditions)
            {
                var def = catalog.FindCondition(c.id);
                if (def != null) conditionList.Add((def, c.remainingDays));
                else Debug.LogWarning($"[FamilySaveParticipant] 子どもの状態 id \"{c.id}\" がカタログに見つかりません。スキップします。");
            }
        }
        child.RestoreConditions(conditionList);
    }
}
