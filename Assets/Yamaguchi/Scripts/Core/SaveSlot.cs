using System;

/// <summary>セーブの枠。Manual は従来の save.json、Auto は save_auto.json。</summary>
public enum SaveSlot
{
    Auto,
    Manual
}

/// <summary>タイトル画面のカードに出す、1枠ぶんの見出し。SaveData の savedAt / ageDays / money だけを使う。</summary>
[Serializable]
public class SaveSlotInfo
{
    public SaveSlot slot;
    public bool exists;
    public string savedAt;   // "yyyy-MM-dd HH:mm:ss"（SaveData.savedAt そのまま）
    public float ageDays;
    public int money;
}
