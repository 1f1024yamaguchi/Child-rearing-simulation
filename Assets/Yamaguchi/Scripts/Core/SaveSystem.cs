using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 登録された参加者（ISaveParticipant）を集めて、1つのファイル（save.json）に保存・読み込みする。
/// 他のメンバーの型は知らない。参加者は OnEnable で Register するだけでよい。
/// 呼び出し例: SaveSystem.Save();  /  SaveSystem.Load();
/// </summary>
public static class SaveSystem
{
    private const string FileName = "save.json";
    private const string LegacyPlayerPrefsKey = "childgame_save";   // 旧セーブ。使わない。Delete() で消すだけ

    private static readonly List<ISaveParticipant> participants = new List<ISaveParticipant>();

    public static event Action OnSaved;
    public static event Action OnLoaded;

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

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

    public static bool Save()
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

        try
        {
            WriteAtomic(FilePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] セーブに失敗しました: {e}");
            return false;
        }

        Debug.Log($"[SaveSystem] セーブしました。({FilePath})");
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

    // ---- 有無・削除 ----

    public static bool HasSave() => File.Exists(FilePath);

    public static void Delete()
    {
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
            if (File.Exists(FilePath + ".tmp")) File.Delete(FilePath + ".tmp");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] セーブの削除に失敗しました: {e}");
        }

        if (PlayerPrefs.HasKey(LegacyPlayerPrefsKey))
        {
            PlayerPrefs.DeleteKey(LegacyPlayerPrefsKey);
            PlayerPrefs.Save();
        }
    }

    // ---- ロード ----

    /// <summary>
    /// セーブがあれば復元して true を返す。順番は必ず：
    /// 時間を止める → Ticker に今日を教える（通知なし）→ 年齢・お金・ストレスを戻す → 参加者を RestoreOrder 順に戻す → 時間を戻す。
    /// </summary>
    public static bool Load()
    {
        if (GameState.I == null || GameClock.I == null)
        {
            Debug.LogWarning("[SaveSystem] GameState / GameClock が無いのでロードできません。");
            return false;
        }
        if (!HasSave()) return false;

        SaveData data;
        try
        {
            data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
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
            Debug.Log("[SaveSystem] ロードしました。");
            OnLoaded?.Invoke();
        }
        return ok;
    }
}
