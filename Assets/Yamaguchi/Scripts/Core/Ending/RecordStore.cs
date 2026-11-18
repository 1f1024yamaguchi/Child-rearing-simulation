using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 育成記録（result-scene.md 5章）。セーブとは別のファイル persistentDataPath/records.json に、終わるたびに1件足す。
/// 一覧・詳細の画面はタイトル画面の作業で作る。
/// ※review-records.md 6.2 の「概要と1件ぶんを分ける」は、月の記録（LifeRecorder）を入れるときに行う。
/// </summary>
public static class RecordStore
{
    [Serializable]
    private class RecordList
    {
        public List<EndingResult> records = new List<EndingResult>();
    }

    public static string FilePath => Path.Combine(Application.persistentDataPath, "records.json");

    public static void Add(EndingResult result)
    {
        if (result == null) return;
        var list = Read();
        list.records.Add(result);
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(list));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RecordStore] 育成記録を書けませんでした: {ex.Message}");
        }
    }

    /// <summary>新しい順。ファイルが無い・壊れているときは空のリスト。</summary>
    public static List<EndingResult> LoadAll()
    {
        var list = Read().records;
        list.Sort((a, b) => string.CompareOrdinal(b.finishedAt, a.finishedAt));
        return list;
    }

    public static int Count => Read().records.Count;

    public static void DeleteAll()
    {
        try { if (File.Exists(FilePath)) File.Delete(FilePath); }
        catch (Exception ex) { Debug.LogWarning($"[RecordStore] 育成記録を消せませんでした: {ex.Message}"); }
    }

    private static RecordList Read()
    {
        try
        {
            if (!File.Exists(FilePath)) return new RecordList();
            var list = JsonUtility.FromJson<RecordList>(File.ReadAllText(FilePath));
            return list ?? new RecordList();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RecordStore] 育成記録が読めませんでした（壊れている？）: {ex.Message}");
            return new RecordList();
        }
    }
}
