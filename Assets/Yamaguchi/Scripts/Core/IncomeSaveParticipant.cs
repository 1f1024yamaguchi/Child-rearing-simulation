using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 年収の記録（IncomeHistory）のセーブ・ロードを担当する参加者。
/// GameManager に付ける。
/// </summary>
public class IncomeSaveParticipant : MonoBehaviour, ISaveParticipant
{
    public string SaveKey => "income";
    public int RestoreOrder => 20;

    private void OnEnable() => SaveSystem.Register(this);
    private void OnDisable() => SaveSystem.Unregister(this);

    [Serializable]
    private class IncomeSaveData
    {
        public List<int> months = new List<int>();
    }

    public string Capture()
    {
        var data = new IncomeSaveData
        {
            months = IncomeHistory.I.ToSave()
        };
        return JsonUtility.ToJson(data);
    }

    public void Restore(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        var data = JsonUtility.FromJson<IncomeSaveData>(json);
        if (data != null)
        {
            IncomeHistory.I.Restore(data.months);
        }
    }
}
