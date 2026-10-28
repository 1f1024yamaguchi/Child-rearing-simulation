using System;
using System.Collections.Generic;
using System.Text;
using Maruyama.Core;
using UnityEngine;

/// <summary>
/// 毎月の支出（家賃・光熱費・保育料・学費など）を管理し、月が変わるたびに引き落とすクラス（expenses.md 1〜3章）。
/// GameManager に付ける。
/// </summary>
public class ExpenseManager : MonoBehaviour, ISaveParticipant
{
    public static ExpenseManager I { get; private set; }

    [Header("支出項目一覧")]
    [SerializeField] private List<ExpenseItem> items = new List<ExpenseItem>();

    public List<ExpenseItem> Items => items;

    public string SaveKey => "expense";
    public int RestoreOrder => 35;

    private readonly Dictionary<string, ExpenseRuntime> runtimes = new Dictionary<string, ExpenseRuntime>();
    private bool subscribed;

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }
        I = this;
    }

    private void OnEnable()
    {
        SaveSystem.Register(this);
    }

    private void OnDisable()
    {
        SaveSystem.Unregister(this);
    }

    private void Start()
    {
        if (subscribed) return;
        if (MonthTicker.I == null)
        {
            Debug.LogError("ExpenseManager: MonthTicker が見つかりません");
            return;
        }
        MonthTicker.I.OnMonthChanged += HandleMonthChanged;
        subscribed = true;
    }

    private void OnDestroy()
    {
        if (subscribed && MonthTicker.I != null)
        {
            MonthTicker.I.OnMonthChanged -= HandleMonthChanged;
        }
        subscribed = false;
    }

    // ------------------------------------------------------------
    // 状態管理（Optional 用）
    // ------------------------------------------------------------

    public ExpenseRuntime GetRuntime(ExpenseItem item)
    {
        if (item == null || string.IsNullOrEmpty(item.id)) return null;
        return GetRuntime(item.id);
    }

    public ExpenseRuntime GetRuntime(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (!runtimes.TryGetValue(id, out var rt))
        {
            rt = new ExpenseRuntime { id = id };
            runtimes[id] = rt;
        }
        return rt;
    }

    /// <summary>申し込み中（受講中・通塾中など）か。</summary>
    public bool IsEnrolled(ExpenseItem item)
    {
        if (item == null || string.IsNullOrEmpty(item.id)) return false;
        return GetRuntime(item).enrolled;
    }

    /// <summary>申し込む（Optional 用）。</summary>
    public void Enroll(ExpenseItem item)
    {
        if (item == null || string.IsNullOrEmpty(item.id)) return;
        var rt = GetRuntime(item);
        rt.enrolled = true;
        rt.enrolledAgeDays = GameState.I != null ? Mathf.FloorToInt(GameState.I.AgeDays) : 0;
        Debug.Log($"[ExpenseManager] {item.title} を申し込みました");
    }

    /// <summary>やめる（Optional 用）。</summary>
    public void Withdraw(ExpenseItem item)
    {
        if (item == null || string.IsNullOrEmpty(item.id)) return;
        var rt = GetRuntime(item);
        rt.enrolled = false;
        Debug.Log($"[ExpenseManager] {item.title} をやめました");
    }

    // ------------------------------------------------------------
    // 月の精算
    // ------------------------------------------------------------

    /// <summary>
    /// 今月の額を計算する。依頼E3 で支援制度による肩代わり（covers）をここに足す。
    /// </summary>
    public int CalcMonthlyCost(ExpenseItem item, CostTier tier, GameContext ctx)
    {
        if (tier == null) return 0;
        // 依頼E3 で支援制度による肩代わり計算をここに追加
        return Mathf.Max(0, tier.monthlyCost);
    }

    private void HandleMonthChanged(int year, int month)
    {
        GameContext ctx = GameContext.Current();
        int totalCost = 0;

        foreach (var item in items)
        {
            if (item == null) continue;

            // Auto または 申し込み中の Optional
            bool active = item.mode == ExpenseMode.Auto || IsEnrolled(item);
            if (!active) continue;

            CostTier tier = item.FindTier(ctx);
            if (tier == null) continue;

            int cost = CalcMonthlyCost(item, tier, ctx);
            var effects = new List<Effect>();

            if (cost > 0)
            {
                effects.Add(new Effect
                {
                    kind = EffectKind.Money,
                    target = EffectTarget.Household,
                    value = -cost
                });
            }

            // 段のお金以外の効果（塾の成績+、子ストレス+など）
            if (tier.monthlyEffects != null)
            {
                foreach (var eff in tier.monthlyEffects)
                {
                    if (eff != null) effects.Add(eff);
                }
            }

            if (effects.Count > 0)
            {
                EffectApplier.Apply(effects, new LedgerSource(item.title, item.category));
                totalCost += cost;
                Debug.Log($"[ExpenseManager] {item.title}（{tier.DisplayLabel()}）：-{cost:N0}円");
            }
        }

        Debug.Log($"[ExpenseManager] {year}歳{month}月 支出引き落とし完了 合計: -{totalCost:N0}円 → 所持金残高: {(GameState.I != null ? GameState.I.Money : 0):N0}円");
    }

    // ------------------------------------------------------------
    // セーブ・ロード
    // ------------------------------------------------------------

    [Serializable]
    private class ExpenseSaveData
    {
        public List<ExpenseRuntime> runtimes = new List<ExpenseRuntime>();
    }

    public string Capture()
    {
        var data = new ExpenseSaveData();
        foreach (var kv in runtimes)
        {
            data.runtimes.Add(kv.Value);
        }
        return JsonUtility.ToJson(data);
    }

    public void Restore(string json)
    {
        runtimes.Clear();
        if (string.IsNullOrEmpty(json)) return;
        var data = JsonUtility.FromJson<ExpenseSaveData>(json);
        if (data != null && data.runtimes != null)
        {
            foreach (var rt in data.runtimes)
            {
                if (rt != null && !string.IsNullOrEmpty(rt.id))
                {
                    runtimes[rt.id] = rt;
                }
            }
        }
    }

    // ------------------------------------------------------------
    // デバッグ確認用
    // ------------------------------------------------------------

#if UNITY_EDITOR
    [ContextMenu("Editor/全支出アセットを自動登録")]
    private void LoadAllExpenseAssets()
    {
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ExpenseItem");
        items.Clear();
        foreach (var guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ExpenseItem>(path);
            if (asset != null && !items.Contains(asset))
            {
                items.Add(asset);
            }
        }
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[ExpenseManager] {items.Count} 個の支出項目アセットを登録しました");
    }
#endif

    [ContextMenu("Debug/今月の支出をログ（引かない）")]
    private void LogThisMonthExpenses()
    {
        GameContext ctx = GameContext.Current();
        int y = 0, m = 1, d = 1;
        if (GameState.I != null) GameDate.FromAgeDays(GameState.I.AgeDays, out y, out m, out d);

        var sb = new StringBuilder();
        sb.AppendLine($"[ExpenseManager] 今月の支出予定（{y}歳{m}月 / 年収 {ctx.AnnualIncome:N0}円）");
        int total = 0;
        foreach (var item in items)
        {
            if (item == null) continue;
            bool active = item.mode == ExpenseMode.Auto || IsEnrolled(item);
            if (!active) continue;

            CostTier tier = item.FindTier(ctx);
            if (tier != null)
            {
                int cost = CalcMonthlyCost(item, tier, ctx);
                total += cost;
                sb.AppendLine($"  [{item.category}] {item.title} ({tier.DisplayLabel()}): {cost:N0}円");
            }
            else
            {
                sb.AppendLine($"  [{item.category}] {item.title}: 対象外 (0円)");
            }
        }
        sb.AppendLine($"  合計: {total:N0}円");
        Debug.Log(sb.ToString());
    }
}
