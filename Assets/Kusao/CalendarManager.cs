using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// カレンダーの予定（CalendarPlan）の保持・管理・セーブを担当するマネージャー。
/// シーンロード時に自動生成され、DontDestroyOnLoad で保持される。
/// </summary>
public class CalendarManager : MonoBehaviour, ISaveParticipant
{
    private static CalendarManager instance;
    public static CalendarManager I
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<CalendarManager>();
                if (instance == null)
                {
                    var go = new GameObject("CalendarManager");
                    instance = go.AddComponent<CalendarManager>();
                }
            }
            return instance;
        }
    }

    [Serializable]
    private class CalendarSaveData
    {
        public List<CalendarPlan> plans = new List<CalendarPlan>();
    }

    [SerializeField] private List<CalendarPlan> plans = new List<CalendarPlan>();

    /// <summary>予定が追加・変更・削除されたときの通知イベント。</summary>
    public event Action OnPlansChanged;

    public string SaveKey => "calendar_plans";
    public int RestoreOrder => 50;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        if (instance == null)
        {
            var go = new GameObject("CalendarManager");
            instance = go.AddComponent<CalendarManager>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable()
    {
        SaveSystem.Register(this);
    }

    private void OnDisable()
    {
        SaveSystem.Unregister(this);
    }

    // ------------------------------------------------------------
    // 予定の追加・更新・削除・取得
    // ------------------------------------------------------------

    /// <summary>
    /// 制度カードから予定を追加または更新する。
    /// すでに同じ制度の予定が存在する場合は期限日を更新する。
    /// </summary>
    public CalendarPlan AddPlanFromScheme(SupportScheme scheme, int ageDays, PlanKind kind)
    {
        if (scheme == null) return null;

        var existing = plans.Find(p => p.schemeId == scheme.id);
        if (existing != null)
        {
            existing.ageDays = ageDays;
            existing.kind = kind;
            existing.title = scheme.title;
            existing.reminded = false;
            OnPlansChanged?.Invoke();
            AutoSave();
            return existing;
        }

        var plan = new CalendarPlan(scheme.id, scheme.title, ageDays, kind);
        plans.Add(plan);
        OnPlansChanged?.Invoke();
        AutoSave();
        return plan;
    }

    /// <summary>
    /// 任意の予定を追加・更新する。
    /// </summary>
    public void AddOrUpdatePlan(CalendarPlan plan)
    {
        if (plan == null) return;
        int index = plans.FindIndex(p => p.uid == plan.uid);
        if (index >= 0)
        {
            plans[index] = plan;
        }
        else
        {
            plans.Add(plan);
        }
        OnPlansChanged?.Invoke();
        AutoSave();
    }

    /// <summary>
    /// 予定を削除する。
    /// </summary>
    public bool RemovePlan(string uid)
    {
        int count = plans.RemoveAll(p => p.uid == uid);
        if (count > 0)
        {
            OnPlansChanged?.Invoke();
            AutoSave();
            return true;
        }
        return false;
    }

    /// <summary>
    /// 指定した制度がすでにカレンダーに登録されているか。
    /// </summary>
    public bool HasPlanForScheme(string schemeId)
    {
        if (string.IsNullOrEmpty(schemeId)) return false;
        return plans.Exists(p => p.schemeId == schemeId);
    }

    /// <summary>
    /// 指定した日の通算日数 (ageDays) にある予定のリストを取得する。
    /// </summary>
    public List<CalendarPlan> GetPlansForDay(int ageDays)
    {
        return plans.FindAll(p => p.ageDays == ageDays);
    }

    /// <summary>
    /// 全予定のリストを取得する。
    /// </summary>
    public IReadOnlyList<CalendarPlan> GetAllPlans()
    {
        return plans;
    }

    private void AutoSave()
    {
        SaveSystem.Save();
    }

    // ------------------------------------------------------------
    // セーブ・ロード (ISaveParticipant)
    // ------------------------------------------------------------

    public string Capture()
    {
        var data = new CalendarSaveData { plans = new List<CalendarPlan>(plans) };
        return JsonUtility.ToJson(data);
    }

    public void Restore(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            var data = JsonUtility.FromJson<CalendarSaveData>(json);
            if (data != null && data.plans != null)
            {
                plans = data.plans;
                OnPlansChanged?.Invoke();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CalendarManager] Restore 失敗: {ex.Message}");
        }
    }
}
