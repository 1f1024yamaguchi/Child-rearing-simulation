using UnityEngine;

/// <summary>
/// ストーリー状態（StoryState）のセーブ・ロードを担当する参加者。セーブキー "story"。
/// </summary>
public class StorySaveParticipant : MonoBehaviour, ISaveParticipant
{
    public string SaveKey => "story";

    /// <summary>
    /// 他の人の担当分（100〜）やイベント（110）の直後である 115 で復元する。
    /// </summary>
    public int RestoreOrder => 115;

    private void OnEnable() => SaveSystem.Register(this);
    private void OnDisable() => SaveSystem.Unregister(this);

    public string Capture()
    {
        var state = StoryState.I;
        if (state == null) return string.Empty;
        var data = state.CaptureSaveData();
        return JsonUtility.ToJson(data);
    }

    public void Restore(string json)
    {
        var state = StoryState.I;
        if (state == null) return;

        // 古いセーブ等で "story" セクションが無い、または空文字の場合でも安全にクリアして復元完了とする
        if (string.IsNullOrEmpty(json))
        {
            state.Clear();
            return;
        }

        try
        {
            var data = JsonUtility.FromJson<StoryState.SaveData>(json);
            state.RestoreSaveData(data);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[StorySaveParticipant] ロードデータの復元に失敗しました: {ex.Message}");
            state.Clear();
        }
    }
}

