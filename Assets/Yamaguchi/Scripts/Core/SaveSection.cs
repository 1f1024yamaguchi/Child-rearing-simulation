/// <summary>参加者1人ぶんのセーブ内容。key は ISaveParticipant.SaveKey、json は Capture() の結果。</summary>
[System.Serializable]
public class SaveSection
{
    public string key;
    public string json;
}
