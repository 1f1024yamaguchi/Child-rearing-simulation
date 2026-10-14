using System.Collections.Generic;

/// <summary>
/// セーブファイル（save.json）の中身。JsonUtility でシリアライズするため public フィールドで持つ。
/// </summary>
[System.Serializable]
public class SaveData
{
    public int    version = 1;     // データの形を変えたら上げる（古いセーブを読むとき用）
    public string savedAt;         // 保存した現実の日時（表示用）
    public float  ageDays;         // 先に戻す必要がある（ロードの順番）
    public int    money;
    public bool   isGameOver;
    public int    stress = -1;            // 画面のストレスゲージ（GameState.Stress）。-1 = 古いセーブで値なし
    public float  workLifeBalance = -1f;  // GameState.WorkLifeBalance。-1 = 古いセーブで値なし
    public List<SaveSection> sections = new List<SaveSection>();
}
