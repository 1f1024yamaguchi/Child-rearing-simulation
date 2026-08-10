using UnityEngine;

/// <summary>
/// セーブデータの中身。JsonUtilityでシリアライズするため public フィールドで持つ。
/// </summary>
[System.Serializable]
public class SaveData
{
    public int money;
    public int stress;
    public float ageMonths;
    public float workLifeBalance;
}

/// <summary>
/// GameStateをJSON化してPlayerPrefsに保存/復元する。
/// 呼び出し例: SaveSystem.Save();  /  SaveSystem.Load();
/// </summary>
public static class SaveSystem
{
    private const string Key = "childgame_save";

    public static void Save()
    {
        if (GameState.I == null) return;
        var s = GameState.I;

        var data = new SaveData
        {
            money = s.Money,
            stress = s.Stress,
            ageMonths = s.AgeMonths,
            workLifeBalance = s.WorkLifeBalance
        };

        PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        Debug.Log("[SaveSystem] セーブしました。");
    }

    public static bool HasSave() => PlayerPrefs.HasKey(Key);

    /// <summary>
    /// セーブがあれば復元してtrueを返す。復元後はGameState側が通知を飛ばすのでUIも揃う。
    /// </summary>
    public static bool Load()
    {
        if (GameState.I == null || !HasSave()) return false;

        var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(Key));
        GameState.I.LoadValues(data.money, data.stress, data.ageMonths, data.workLifeBalance);
        Debug.Log("[SaveSystem] ロードしました。");
        return true;
    }

    public static void Delete()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }
}
