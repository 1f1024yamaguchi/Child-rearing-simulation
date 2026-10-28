using UnityEngine;

/// <summary>
/// 家計簿（Ledger）のセーブ・ロードを担当する参加者。
/// GameManager に付ける。
/// </summary>
public class LedgerSaveParticipant : MonoBehaviour, ISaveParticipant
{
    public string SaveKey => "ledger";
    public int RestoreOrder => 25;

    private void OnEnable() => SaveSystem.Register(this);
    private void OnDisable() => SaveSystem.Unregister(this);

    public string Capture()
    {
        return JsonUtility.ToJson(Ledger.I.ToSave());
    }

    public void Restore(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        var data = JsonUtility.FromJson<LedgerSaveData>(json);
        if (data != null)
        {
            Ledger.I.Restore(data);
        }
    }
}
