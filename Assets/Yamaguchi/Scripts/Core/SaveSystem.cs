using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 登録された参加者（ISaveParticipant）を集めて、枠ごとのファイルに保存・読み込みする。
/// 枠は2つ：Manual = save.json（従来のファイル）／Auto = save_auto.json。
/// 他のメンバーの型は知らない。参加者は OnEnable で Register するだけでよい。
/// 呼び出し例: SaveSystem.Save(SaveSlot.Manual);  /  SaveSystem.Load(SaveSlot.Auto);
/// 引数なしの Save() は Auto 枠、Load() は保存日時が新しいほうの枠（他のメンバーのコード互換）。
/// </summary>
public static class SaveSystem
{
    private const string ManualFileName = "save.json";
    private const string AutoFileName = "save_auto.json";
    private const string LegacyPlayerPrefsKey = "childgame_save";   // 旧セーブ。使わない。Delete() で消すだけ

    private static readonly List<ISaveParticipant> participants = new List<ISaveParticipant>();

    public static event Action OnSaved;
    public static event Action OnLoaded;

    /// <summary>手動枠のファイルの場所（従来の FilePath）。</summary>
    public static string FilePath => FilePathOf(SaveSlot.Manual);

    public static string FilePathOf(SaveSlot slot) =>
        Path.Combine(Application.persistentDataPath, slot == SaveSlot.Auto ? AutoFileName : ManualFileName);

    // ---- 参加者の登録 ----

    public static void Register(ISaveParticipant participant)
    {
        if (participant == null || participants.Contains(participant)) return;
        participants.Add(participant);
    }

    public static void Unregister(ISaveParticipant participant)
    {
        if (participant == null) return;
        participants.Remove(participant);
    }

    // ---- セーブ ----

    /// <summary>オート枠に保存する（CalendarManager など従来の呼び出し用）。</summary>
    public static bool Save() => Save(SaveSlot.Auto);

    public static bool Save(SaveSlot slot)
    {
        if (GameState.I == null)
        {
            Debug.LogWarning("[SaveSystem] GameState が無いのでセーブできません。");
            return false;
        }

        var data = new SaveData
        {
            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ageDays = GameState.I.AgeDays,
            money = GameState.I.Money,
            isGameOver = GameState.I.IsGameOver,
            stress = GameState.I.Stress,
            workLifeBalance = GameState.I.WorkLifeBalance
        };

        foreach (var p in participants.ToArray())
        {
            try
            {
                data.sections.Add(new SaveSection { key = p.SaveKey, json = p.Capture() });
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] \"{p.SaveKey}\" の Capture に失敗したので、この部分は保存しません: {e}");
            }
        }

        string path = FilePathOf(slot);
        try
        {
            WriteRaw(slot, JsonUtility.ToJson(data, !UseWebStorage));
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] セーブに失敗しました: {e}");
            return false;
        }

        Debug.Log($"[SaveSystem] セーブしました。({slot}: {(UseWebStorage ? PrefsKeyOf(slot) : path)})");
        OnSaved?.Invoke();
        return true;
    }

    /// <summary>一時ファイルに書いてから置き換える（途中で落ちても本体のセーブが壊れない）。</summary>
    private static void WriteAtomic(string path, string text)
    {
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, text);
        if (File.Exists(path)) File.Replace(tmp, path, null);
        else File.Move(tmp, path);
    }

    // ---- 保存先の切り替え ----
    // WebGL（unityroom）ではファイル保存がブラウザを閉じると消えやすいので、PlayerPrefs（ブラウザのIndexedDB）に保存する。
    // それ以外（エディタ・PC）は従来どおり persistentDataPath のファイル。
#if UNITY_WEBGL && !UNITY_EDITOR
    private static bool UseWebStorage => true;
#else
    private static bool UseWebStorage => false;
#endif

    private static string PrefsKeyOf(SaveSlot slot) => slot == SaveSlot.Auto ? "childgame_save_auto" : "childgame_save_manual";

    private static bool ExistsRaw(SaveSlot slot)
    {
        if (UseWebStorage) return PlayerPrefs.HasKey(PrefsKeyOf(slot));
        return File.Exists(FilePathOf(slot));
    }

    private static string ReadRaw(SaveSlot slot)
    {
        if (UseWebStorage) return PlayerPrefs.GetString(PrefsKeyOf(slot), "");
        return File.ReadAllText(FilePathOf(slot));
    }

    private static void WriteRaw(SaveSlot slot, string text)
    {
        if (UseWebStorage)
        {
            PlayerPrefs.SetString(PrefsKeyOf(slot), text);
            PlayerPrefs.Save();   // WebGL ではここでIndexedDBへ書き出される
            return;
        }
        WriteAtomic(FilePathOf(slot), text);
    }

    private static void DeleteRaw(SaveSlot slot)
    {
        if (UseWebStorage)
        {
            PlayerPrefs.DeleteKey(PrefsKeyOf(slot));
            PlayerPrefs.Save();
            return;
        }
        string path = FilePathOf(slot);
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
    }

    // ---- 有無・削除 ----

    public static bool HasSave(SaveSlot slot) => ExistsRaw(slot);

    public static bool HasAnySave() => HasSave(SaveSlot.Auto) || HasSave(SaveSlot.Manual);

    /// <summary>どちらかの枠にセーブがあるか（従来の呼び出し用）。</summary>
    public static bool HasSave() => HasAnySave();

    /// <summary>指定した枠のセーブを消す。</summary>
    public static void Delete(SaveSlot slot)
    {
        string path = FilePathOf(slot);
        try
        {
            DeleteRaw(slot);
            
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] セーブの削除に失敗しました（{slot}）: {e}");
        }
    }

    /// <summary>2枠とも消す（ゲームが終わったとき AutoSaver が呼ぶ）。旧 PlayerPrefs のセーブも消す。</summary>
    public static void Delete()
    {
        Delete(SaveSlot.Auto);
        Delete(SaveSlot.Manual);

        if (PlayerPrefs.HasKey(LegacyPlayerPrefsKey))
        {
            PlayerPrefs.DeleteKey(LegacyPlayerPrefsKey);
            PlayerPrefs.Save();
        }
    }

    // ---- 見出し（タイトル画面用。GameState が無くても呼べる） ----

    /// <summary>枠の見出しを返す。ファイルが無い・壊れているときは例外を出さず exists=false。</summary>
    public static SaveSlotInfo GetSlotInfo(SaveSlot slot)
    {
        var info = new SaveSlotInfo { slot = slot, exists = false };
        try
        {
            string path = FilePathOf(slot);
            if (!ExistsRaw(slot)) return info;

            var data = JsonUtility.FromJson<SaveData>(ReadRaw(slot));
            if (data == null) return info;

            info.exists = true;
            info.savedAt = data.savedAt;
            info.ageDays = data.ageDays;
            info.money = data.money;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveSystem] セーブの見出しを読めませんでした（{slot}）: {e.Message}");
            info.exists = false;
        }
        return info;
    }

    /// <summary>保存日時を DateTime にする。読めなければ DateTime.MinValue。</summary>
    public static DateTime ParseSavedAt(string savedAt)
    {
        return DateTime.TryParse(savedAt, out var t) ? t : DateTime.MinValue;
    }

    /// <summary>保存日時が新しいほうの枠。同じ・どちらも無いときは Auto。</summary>
    public static SaveSlot NewestSlot()
    {
        var a = GetSlotInfo(SaveSlot.Auto);
        var m = GetSlotInfo(SaveSlot.Manual);
        if (a.exists && !m.exists) return SaveSlot.Auto;
        if (!a.exists && m.exists) return SaveSlot.Manual;
        return ParseSavedAt(m.savedAt) > ParseSavedAt(a.savedAt) ? SaveSlot.Manual : SaveSlot.Auto;
    }

    // ---- ロード ----

    /// <summary>保存日時が新しいほうの枠を読む（従来の呼び出し用）。セーブが無ければ false。</summary>
    public static bool Load()
    {
        if (!HasAnySave()) return false;
        return Load(NewestSlot());
    }

    /// <summary>
    /// 指定した枠のセーブがあれば復元して true を返す。順番は必ず：
    /// 時間を止める → Ticker に今日を教える（通知なし）→ 年齢・お金・ストレスを戻す → 参加者を RestoreOrder 順に戻す → 時間を戻す。
    /// </summary>
    public static bool Load(SaveSlot slot)
    {
        if (GameState.I == null || GameClock.I == null)
        {
            Debug.LogWarning("[SaveSystem] GameState / GameClock が無いのでロードできません。");
            return false;
        }
        if (!HasSave(slot)) return false;

        SaveData data;
        try
        {
            data = JsonUtility.FromJson<SaveData>(ReadRaw(slot));
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] セーブファイルを読めませんでした: {e}");
            return false;
        }
        if (data == null)
        {
            Debug.LogError("[SaveSystem] セーブファイルが空か壊れています。");
            return false;
        }

        bool ok = false;
        GameClock.I.PushPause();                                   // ① 時間を止める
        try
        {
            DayTicker.I?.Resync(Mathf.FloorToInt(data.ageDays));   // ② 通知なしで「今日」を合わせる
            MonthTicker.I?.Resync(data.ageDays);

            // ③ 年齢・お金・ストレスゲージ・バランス（古いセーブで値が無いものは今の値のまま）
            int   stress  = data.stress >= 0 ? data.stress : GameState.I.Stress;
            float balance = data.workLifeBalance >= 0f ? data.workLifeBalance : GameState.I.WorkLifeBalance;
            GameState.I.LoadValues(data.money, stress, data.ageDays, balance, data.isGameOver);

            // ④ 参加者を RestoreOrder の小さい順に
            var ordered = new List<ISaveParticipant>(participants);
            ordered.Sort((a, b) => a.RestoreOrder.CompareTo(b.RestoreOrder));
            foreach (var p in ordered)
            {
                var section = data.sections?.Find(s => s.key == p.SaveKey);
                if (section == null) continue;                    // セーブにキーが無ければ飛ばす
                try
                {
                    p.Restore(section.json);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SaveSystem] \"{p.SaveKey}\" の Restore に失敗しました: {e}");
                }
            }
            ok = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] ロード中にエラーが出ました: {e}");
        }
        finally
        {
            GameClock.I.PopPause();                                // ⑤ 必ず時間を戻す
        }

        if (ok)
        {
            Debug.Log($"[SaveSystem] ロードしました。({slot})");
            OnLoaded?.Invoke();
        }
        return ok;
    }
}
