using System.Collections.Generic;
using Maruyama.Events;
using UnityEditor;

/// <summary>
/// ストーリーの索引。フラグごとに「立てる／消す／見る」場所をまとめる。
/// イベントの分は EventRegistry.FindFlagUsers を使い、足りない分（チュートリアル・支援制度や職業の条件）だけここで足す。
/// アセットが変わったとき（projectChanged）に Invalidate する。毎フレームは作らない。
/// </summary>
public static class StoryIndex
{
    public class FlagUsage
    {
        public List<EventData> setters = new List<EventData>();
        public List<EventData> clearers = new List<EventData>();
        public List<EventData> eventConditions = new List<EventData>();
        public List<TutorialData> tutorialTimings = new List<TutorialData>();   // 出すタイミングが「フラグが立った日」
        public List<TutorialData> tutorialPages = new List<TutorialData>();     // ページの出す条件
        public List<StoryFlagRequirementSO> requirements = new List<StoryFlagRequirementSO>(); // 支援制度・職業の条件の部品
        public List<CareerDefinition> careers = new List<CareerDefinition>();   // 上の条件を使っている職業

        public int SetCount => setters.Count;
        public int ClearCount => clearers.Count;
        public int LookCount => eventConditions.Count + tutorialTimings.Count + tutorialPages.Count + requirements.Count;
        public bool IsUnused => SetCount == 0 && ClearCount == 0 && LookCount == 0;
    }

    /// <summary>Invalidate のたびに増える。流れ図などが作り直しの合図に使う。</summary>
    public static int Version { get; private set; }

    private static readonly Dictionary<StoryFlag, FlagUsage> cache = new Dictionary<StoryFlag, FlagUsage>();
    private static List<TutorialData> tutorials;
    private static List<StoryFlagRequirementSO> flagRequirements;
    private static List<CareerDefinition> careers;

    public static void Invalidate()
    {
        cache.Clear();
        tutorials = null;
        flagRequirements = null;
        careers = null;
        Version++;
        EventRegistry.Invalidate();
    }

    public static FlagUsage UsageOf(StoryFlag flag)
    {
        if (flag == null) return new FlagUsage();
        if (cache.TryGetValue(flag, out var u)) return u;

        u = new FlagUsage();
        var ev = EventRegistry.FindFlagUsers(flag);
        u.setters.AddRange(ev.setters);
        u.clearers.AddRange(ev.clearers);
        u.eventConditions.AddRange(ev.conditionUsers);

        foreach (var t in Tutorials())
        {
            if (t == null) continue;
            var timing = t.Timing;
            if (timing != null && timing.kind == TutorialTimingKind.FlagSet && Same(timing.targetFlag, flag)) u.tutorialTimings.Add(t);
            foreach (var page in t.Pages)
            {
                if (page == null || page.showConditions == null) continue;
                bool hit = false;
                foreach (var c in page.showConditions)
                    if (c is StoryFlagCondition sfc && Same(sfc.flag, flag)) { hit = true; break; }
                if (hit) { u.tutorialPages.Add(t); break; }
            }
        }

        foreach (var r in FlagRequirements())
        {
            if (r == null || !Same(r.flag, flag)) continue;
            u.requirements.Add(r);
            foreach (var c in Careers())
                if (c != null && c.requirements != null && c.requirements.Contains(r) && !u.careers.Contains(c)) u.careers.Add(c);
        }

        cache[flag] = u;
        return u;
    }

    /// <summary>✕（エラー）と ⚠（注意）の文。</summary>
    public static List<string> Check(StoryFlag flag, out int errors)
    {
        var list = new List<string>();
        errors = 0;
        if (flag == null) return list;

        if (string.IsNullOrEmpty(flag.id)) { list.Add("✕ ID が空です"); errors++; }
        else if (!EventRegistry.IsValidIdChars(flag.id)) { list.Add("✕ ID は英数字と _ だけにしてください"); errors++; }
        else if (CountId(flag.id) > 1) { list.Add($"✕ ID「{flag.id}」がほかのフラグと重なっています"); errors++; }

        var u = UsageOf(flag);
        if (u.SetCount == 0 && u.LookCount > 0) list.Add("⚠ 立てる場所が無いのに、見ている場所があります（条件がずっと同じ結果になります）");
        if (u.SetCount > 0 && u.LookCount == 0) list.Add("⚠ 立てる場所はあるのに、見ている場所がありません");
        if (u.IsUnused) list.Add("⚠ どこからも使われていません");
        if (flag.useCustomHobbyName && string.IsNullOrEmpty(flag.customHobbyName)) list.Add("⚠ 趣味の呼び名が空です");
        return list;
    }

    public static int CountId(string id)
    {
        int n = 0;
        foreach (var f in EventRegistry.AllFlags()) if (f != null && f.id == id) n++;
        return n;
    }

    public static bool Same(StoryFlag a, StoryFlag b)
    {
        if (a == null || b == null) return false;
        return a == b || (!string.IsNullOrEmpty(a.id) && a.id == b.id);
    }

    public static List<TutorialData> Tutorials()
    {
        if (tutorials == null) tutorials = LoadAll<TutorialData>("t:TutorialData");
        return tutorials;
    }

    private static List<StoryFlagRequirementSO> FlagRequirements()
    {
        if (flagRequirements == null) flagRequirements = LoadAll<StoryFlagRequirementSO>("t:StoryFlagRequirementSO");
        return flagRequirements;
    }

    public static List<CareerDefinition> Careers()
    {
        if (careers == null) careers = LoadAll<CareerDefinition>("t:CareerDefinition");
        return careers;
    }

    private static List<T> LoadAll<T>(string filter) where T : UnityEngine.Object
    {
        var list = new List<T>();
        foreach (var guid in AssetDatabase.FindAssets(filter))
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (a != null) list.Add(a);
        }
        return list;
    }
}
