using System.Collections.Generic;
using System.Text;
using Maruyama.Events;
using UnityEditor;
using UnityEngine;

/// <summary>
/// プロジェクト内の EventData を探して覚えておく（アセットが変わったときだけ作り直す。毎フレームは探さない）。
/// ID の自動生成と重複確認、Resources/Events の判定と移動、条件アセットの逆引きも持つ。
/// </summary>
public static class EventRegistry
{
    /// <summary>EventManager が読み込むフォルダ（Resources 以下の Events）。</summary>
    public const string ResourcesFolderName = "/Resources/Events/";

    /// <summary>新しく作ったイベントの保存先。</summary>
    public const string DefaultDir = "Assets/Maruyama/Resources/Events";

    private static List<EventData> events;
    private static List<EventConditionSO> conditionAssets;
    private static Dictionary<EventConditionSO, List<EventData>> conditionUsers;
    private static List<StoryFlag> flags;

    /// <summary>アセットが増減・変更されたときに呼ぶ。次に使うときに探し直す。</summary>
    public static void Invalidate()
    {
        events = null;
        conditionAssets = null;
        conditionUsers = null;
        flags = null;
    }

    // ─── イベントの一覧 ──────────────────────────────────────────
    /// <summary>プロジェクト内のすべての EventData（ファイル名順）。</summary>
    public static List<EventData> All()
    {
        if (events != null)
        {
            // 消されたアセットが残っていたら作り直す
            foreach (var e in events)
            {
                if (e == null) { events = null; break; }
            }
        }

        if (events == null)
        {
            events = new List<EventData>();
            foreach (var guid in AssetDatabase.FindAssets("t:EventData"))
            {
                var e = AssetDatabase.LoadAssetAtPath<EventData>(AssetDatabase.GUIDToAssetPath(guid));
                if (e != null) events.Add(e);
            }
            events.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            conditionUsers = null;
        }
        return events;
    }

    // ─── ID ──────────────────────────────────────────────────────
    /// <summary>ID に使える文字（英数字と _）だけか。</summary>
    public static bool IsValidIdChars(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        foreach (char c in id)
        {
            bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
            if (!ok) return false;
        }
        return true;
    }

    /// <summary>
    /// ファイル名から ID を作る。先頭の Event_ を外して PascalCase を snake_case にする
    /// （Event_Merit_Baseball → merit_baseball）。ほかのイベントと重なったら _2 _3 を付ける。
    /// </summary>
    public static string MakeId(string fileNameWithoutExt, EventData self)
    {
        string name = fileNameWithoutExt ?? "";
        if (name.StartsWith("Event_")) name = name.Substring("Event_".Length);

        var sb = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (char.IsUpper(c))
            {
                bool prevLowerOrDigit = i > 0 && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]));
                if (prevLowerOrDigit) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
            else sb.Append('_');
        }

        string baseId = sb.ToString().Trim('_');
        if (baseId.Length == 0) baseId = "event";

        string id = baseId;
        int n = 2;
        while (IdCount(id, self) > 0) id = baseId + "_" + n++;
        return id;
    }

    /// <summary>この ID を持つイベントの数（self は数えない）。</summary>
    public static int IdCount(string id, EventData self)
    {
        int count = 0;
        foreach (var e in All())
        {
            if (e == null || e == self) continue;
            if (e.id == id) count++;
        }
        return count;
    }

    // ─── Resources/Events ────────────────────────────────────────
    /// <summary>Resources/Events の中にあるか（EventManager が自動で読み込む場所）。</summary>
    public static bool IsInResources(EventData e)
    {
        string path = AssetDatabase.GetAssetPath(e);
        return !string.IsNullOrEmpty(path) && path.Contains(ResourcesFolderName);
    }

    /// <summary>Assets/Maruyama/Resources/Events/ へ移す。失敗したときだけエラーの文を返す（成功は空文字）。</summary>
    public static string MoveToResources(EventData e)
    {
        SupportEditorGUI.EnsureFolder(DefaultDir);
        string src = AssetDatabase.GetAssetPath(e);
        string dst = AssetDatabase.GenerateUniqueAssetPath(DefaultDir + "/" + System.IO.Path.GetFileName(src));
        string error = AssetDatabase.MoveAsset(src, dst);
        Invalidate();
        return error;
    }

    // ─── 条件アセット ────────────────────────────────────────────
    /// <summary>プロジェクト内の EventConditionSO アセット（別アセットのものだけ。サブアセットは含めない）。</summary>
    public static List<EventConditionSO> AllConditionAssets()
    {
        if (conditionAssets != null)
        {
            foreach (var c in conditionAssets)
            {
                if (c == null) { conditionAssets = null; break; }
            }
        }

        if (conditionAssets == null)
        {
            conditionAssets = new List<EventConditionSO>();
            foreach (var guid in AssetDatabase.FindAssets("t:EventConditionSO"))
            {
                var c = AssetDatabase.LoadAssetAtPath<EventConditionSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (c != null && AssetDatabase.IsMainAsset(c)) conditionAssets.Add(c);
            }
            conditionAssets.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }
        return conditionAssets;
    }

    /// <summary>この条件を使っているイベントの名前（owner は除く）。</summary>
    public static List<string> ConditionUsers(EventConditionSO cond, EventData owner)
    {
        var names = new List<string>();
        if (cond == null) return names;

        // 作っている途中で索引が空に戻されても落ちないよう、手元の辞書に作ってから差し替える
        var map = conditionUsers;
        if (map == null)
        {
            map = new Dictionary<EventConditionSO, List<EventData>>();
            foreach (var e in All())
            {
                if (e == null || e.conditions == null) continue;
                foreach (var c in e.conditions)
                {
                    if (c == null) continue;
                    if (!map.TryGetValue(c, out var list))
                    {
                        list = new List<EventData>();
                        map[c] = list;
                    }
                    if (!list.Contains(e)) list.Add(e);
                }
            }
            conditionUsers = map;
        }

        if (map.TryGetValue(cond, out var users))
        {
            foreach (var e in users)
            {
                if (e != null && e != owner) names.Add(e.name);
            }
        }
        return names;
    }

    // ─── ストーリー・逆引き ──────────────────────────────────────

    public class ScheduledCaller
    {
        public EventData sourceEvent;
        public EventChoice choice;
        public int choiceIndex;
        public int days;
    }

    public class ScheduledDestination
    {
        public EventChoice choice;
        public int choiceIndex;
        public EventData targetEvent;
        public int days;
    }

    public class EventConditionUser
    {
        public EventData userEvent;
        public ChoiceHistoryCondition condition;
    }

    public class FlagUsage
    {
        public List<EventData> setters = new List<EventData>();
        public List<EventData> clearers = new List<EventData>();
        public List<EventData> conditionUsers = new List<EventData>();
    }

    /// <summary>指定イベントを ScheduleEvent で予約している他のイベントと選択肢を探す。</summary>
    public static List<ScheduledCaller> FindCallersOf(EventData target)
    {
        var list = new List<ScheduledCaller>();
        if (target == null) return list;

        foreach (var e in All())
        {
            if (e == null || e.choices == null) continue;
            for (int ci = 0; ci < e.choices.Count; ci++)
            {
                var c = e.choices[ci];
                if (c == null || c.effects == null) continue;
                foreach (var ef in c.effects)
                {
                    if (ef != null && ef.kind == EffectKind.ScheduleEvent)
                    {
                        if (ef.asset == target || (ef.asset is EventData ed && ed.id == target.id))
                        {
                            list.Add(new ScheduledCaller
                            {
                                sourceEvent = e,
                                choice = c,
                                choiceIndex = ci,
                                days = ef.days
                            });
                        }
                    }
                }
            }
        }
        return list;
    }

    /// <summary>このイベントの選択肢が ScheduleEvent で予約している続きイベントを探す。</summary>
    public static List<ScheduledDestination> FindFollowUpsOf(EventData source)
    {
        var list = new List<ScheduledDestination>();
        if (source == null || source.choices == null) return list;

        for (int ci = 0; ci < source.choices.Count; ci++)
        {
            var c = source.choices[ci];
            if (c == null || c.effects == null) continue;
            foreach (var ef in c.effects)
            {
                if (ef != null && ef.kind == EffectKind.ScheduleEvent)
                {
                    var targetEvent = ef.asset as EventData;
                    if (targetEvent != null)
                    {
                        list.Add(new ScheduledDestination
                        {
                            choice = c,
                            choiceIndex = ci,
                            targetEvent = targetEvent,
                            days = ef.days
                        });
                    }
                }
            }
        }
        return list;
    }

    /// <summary>このイベントを条件（ChoiceHistoryCondition など）にしている他のイベントを探す。</summary>
    public static List<EventConditionUser> FindConditionUsersOf(EventData target)
    {
        var list = new List<EventConditionUser>();
        if (target == null) return list;

        foreach (var e in All())
        {
            if (e == null || e == target || e.conditions == null) continue;
            foreach (var c in e.conditions)
            {
                if (c is ChoiceHistoryCondition hist && hist.targetEvent == target)
                {
                    list.Add(new EventConditionUser
                    {
                        userEvent = e,
                        condition = hist
                    });
                }
            }
        }
        return list;
    }

    /// <summary>プロジェクト内のすべての StoryFlag アセット。</summary>
    public static List<StoryFlag> AllFlags()
    {
        if (flags != null)
        {
            foreach (var f in flags)
            {
                if (f == null) { flags = null; break; }
            }
        }

        if (flags == null)
        {
            flags = new List<StoryFlag>();
            foreach (var guid in AssetDatabase.FindAssets("t:StoryFlag"))
            {
                var f = AssetDatabase.LoadAssetAtPath<StoryFlag>(AssetDatabase.GUIDToAssetPath(guid));
                if (f != null) flags.Add(f);
            }
            flags.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }
        return flags;
    }

    /// <summary>指定フラグを立てる／消す／条件にしているイベントを探す。</summary>
    public static FlagUsage FindFlagUsers(StoryFlag flag)
    {
        var usage = new FlagUsage();
        if (flag == null) return usage;

        foreach (var e in All())
        {
            if (e == null) continue;

            // 選択肢の効果（立てる／消す）
            if (e.choices != null)
            {
                foreach (var c in e.choices)
                {
                    if (c == null || c.effects == null) continue;
                    foreach (var ef in c.effects)
                    {
                        if (ef == null) continue;
                        if (ef.kind == EffectKind.SetFlag && (ef.asset == flag || (ef.asset is StoryFlag sf && sf.id == flag.id)))
                        {
                            if (!usage.setters.Contains(e)) usage.setters.Add(e);
                        }
                        else if (ef.kind == EffectKind.ClearFlag && (ef.asset == flag || (ef.asset is StoryFlag cf && cf.id == flag.id)))
                        {
                            if (!usage.clearers.Contains(e)) usage.clearers.Add(e);
                        }
                    }
                }
            }

            // 条件
            if (e.conditions != null)
            {
                foreach (var cond in e.conditions)
                {
                    if (cond is StoryFlagCondition sfc && (sfc.flag == flag || (sfc.flag != null && sfc.flag.id == flag.id)))
                    {
                        if (!usage.conditionUsers.Contains(e)) usage.conditionUsers.Add(e);
                    }
                }
            }
        }

        return usage;
    }
}

/// <summary>アセットが取り込まれたり消えたりしたら、イベントの一覧を作り直す合図を出す。</summary>
public class EventRegistryWatcher : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        EventRegistry.Invalidate();
    }
}

