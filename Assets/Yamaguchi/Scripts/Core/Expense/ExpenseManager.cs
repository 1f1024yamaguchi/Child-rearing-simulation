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
    /// 今月支払う額（肩代わりを引いた後）を計算する。肩代わりされた額も知りたいときは out 付きの方を使う。
    /// </summary>
    public int CalcMonthlyCost(ExpenseItem item, CostTier tier, GameContext ctx)
    {
        return CalcMonthlyCost(item, tier, ctx, out _);
    }

    /// <summary>
    /// 今月支払う額を計算する（expenses.md 4章）。
    /// 支払う額 = 支出の額 − 今月肩代わりが効いている制度の covers の合計（期間内のものだけ。0 未満にしない）。
    /// covered には実際に肩代わりされた額（0〜支出の額）を返す。
    /// </summary>
    public int CalcMonthlyCost(ExpenseItem item, CostTier tier, GameContext ctx, out int covered)
    {
        covered = 0;
        if (tier == null) return 0;
        int baseCost = Mathf.Max(0, tier.monthlyCost);
        if (baseCost == 0 || item == null) return baseCost;

        covered = Mathf.Min(baseCost, CalcCovered(item, baseCost, ctx));
        return baseCost - covered;
    }

    /// <summary>受給中の制度の covers のうち、この支出・今日に当てはまるものの肩代わり額の合計。</summary>
    private static int CalcCovered(ExpenseItem item, int baseCost, GameContext ctx)
    {
        if (SupportManager.I == null) return 0;
        int today = ctx != null ? ctx.ChildAgeDays : 0;
        int sum = 0;
        foreach (var scheme in SupportManager.I.CoveringSchemes())
        {
            foreach (var cover in scheme.covers)
            {
                if (cover == null || !cover.Targets(item) || !cover.Contains(today)) continue;
                sum += cover.CoveredAmount(baseCost);
            }
        }
        return sum;
    }

    private void HandleMonthChanged(int year, int month)
    {
        GameContext ctx = GameContext.Current();
        int totalCost = 0;
        int totalCovered = 0;

        foreach (var item in items)
        {
            if (item == null) continue;

            // Auto または 申し込み中の Optional
            bool active = item.mode == ExpenseMode.Auto || IsEnrolled(item);
            if (!active) continue;

            CostTier tier = item.FindTier(ctx);
            if (tier == null) continue;

            int cost = CalcMonthlyCost(item, tier, ctx, out int covered);
            var effects = new List<Effect>();
            var source = new LedgerSource(item.title, item.category);

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
                // 支払った額は EffectApplier が家計簿に amount として載せる
                EffectApplier.Apply(effects, source);
                totalCost += cost;
            }

            // 肩代わりされた額は同じ行（同じ月・名前・種類）に covered として足す。
            // 全額肩代わり（支払い 0 円）でも「保育料 0円（支援 ○○円）」の行が残る。
            if (covered > 0 && GameState.I != null)
            {
                Ledger.I.Record(source, 0, covered);
                totalCovered += covered;
            }

            if (effects.Count > 0 || covered > 0)
            {
                string coverText = covered > 0 ? $"（支援で {covered:N0}円 肩代わり）" : "";
                Debug.Log($"[ExpenseManager] {item.title}（{tier.DisplayLabel()}）：-{cost:N0}円{coverText}");
            }
        }

        Debug.Log($"[ExpenseManager] {year}歳{month}月 支出引き落とし完了 合計: -{totalCost:N0}円（肩代わり {totalCovered:N0}円） → 所持金残高: {(GameState.I != null ? GameState.I.Money : 0):N0}円");
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
        sb.AppendLine("  [種類] 名前 (段): 支払う額 / 肩代わり額（元の額）");
        int total = 0;
        int totalCovered = 0;
        foreach (var item in items)
        {
            if (item == null) continue;
            bool active = item.mode == ExpenseMode.Auto || IsEnrolled(item);
            if (!active) continue;

            CostTier tier = item.FindTier(ctx);
            if (tier != null)
            {
                int cost = CalcMonthlyCost(item, tier, ctx, out int covered);
                total += cost;
                totalCovered += covered;
                string coverText = covered > 0 ? $"{covered:N0}円" : "-";
                sb.AppendLine($"  [{item.category}] {item.title} ({tier.DisplayLabel()}): {cost:N0}円 / 肩代わり {coverText}（元 {cost + covered:N0}円）");
            }
            else
            {
                sb.AppendLine($"  [{item.category}] {item.title}: 対象外 (0円)");
            }
        }
        sb.AppendLine($"  合計: {total:N0}円 / 肩代わり {totalCovered:N0}円");
        Debug.Log(sb.ToString());
    }
}
