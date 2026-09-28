using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// PersonStatus の特性・状態を StatusListUI の行として表示する。
/// 母・父・子のどのカードからも使える。
/// traits.md 6章の書式に従う：
///   特性 → 「勉強が得意 ｜ 成績+20%」
///   状態 → 「風邪気味 ｜ あと5日」
/// 行のキーは trait_&lt;id&gt; / condition_&lt;id&gt;。
/// child-status.md 6章・traits.md 6章。
public class PersonTraitRowsUI : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("行を追加・削除する StatusListUI")]
    [SerializeField] private StatusListUI statusList;

    private PersonStatus target;

    /// このコンポーネントが管理している行キーの追跡（削除判定に使う）。
    private readonly HashSet<string> _managedKeys = new HashSet<string>();

    // ─── バインド ─────────────────────────────────────────
    /// 対象の PersonStatus を設定し、変更通知を購読して表示を初期化する。
    public void Bind(PersonStatus person)
    {
        Unsubscribe();
        target = person;
        Subscribe();
        RefreshAll();
    }

    private void OnEnable()  { Subscribe();   RefreshAll(); }
    private void OnDisable() { Unsubscribe(); }

    private void Subscribe()
    {
        if (target == null) return;
        target.Traits.OnChanged     += RefreshAll;
        target.Conditions.OnChanged += RefreshAll;
    }

    private void Unsubscribe()
    {
        if (target == null) return;
        target.Traits.OnChanged     -= RefreshAll;
        target.Conditions.OnChanged -= RefreshAll;
    }

    // ─── 更新 ─────────────────────────────────────────────
    private void RefreshAll()
    {
        if (statusList == null || target == null) return;

        // 現在付いているキーを収集（削除判定に使う）
        var currentKeys = new HashSet<string>();

        // 特性行を追加・更新
        if (target.Traits != null)
        {
            foreach (var trait in target.Traits.All)
            {
                if (trait == null) continue;
                string key = "trait_" + trait.id;
                currentKeys.Add(key);
                statusList.SetStatus(key, trait.displayName, BuildModifierText(trait.modifiers));
                _managedKeys.Add(key);
            }
        }

        // 状態行を追加・更新
        if (target.Conditions != null)
        {
            foreach (var active in target.Conditions.All)
            {
                if (active.definition == null) continue;
                string key = "condition_" + active.definition.id;
                currentKeys.Add(key);
                statusList.SetStatus(key, active.definition.displayName, $"あと{active.remainingDays}日");
                _managedKeys.Add(key);
            }
        }

        // 今は付いていない（外れた）行を削除する
        var toRemove = new List<string>();
        foreach (var key in _managedKeys)
        {
            if (!currentKeys.Contains(key))
                toRemove.Add(key);
        }
        foreach (var key in toRemove)
        {
            statusList.RemoveStatus(key);
            _managedKeys.Remove(key);
        }
    }

    // ─── 効果文字列の組み立て ─────────────────────────────
    /// modifiers のリストから「成績+20%」のような文字列を作る。
    /// 補正が複数ある場合は代表の1つを出し、残りは「他+N」と添える。
    private static string BuildModifierText(List<StatModifier> modifiers)
    {
        if (modifiers == null || modifiers.Count == 0) return "";

        var sb = new StringBuilder();
        sb.Append(FormatModifier(modifiers[0]));
        if (modifiers.Count > 1)
            sb.Append($" 他+{modifiers.Count - 1}");
        return sb.ToString();
    }

    private static string FormatModifier(StatModifier mod)
    {
        if (mod.op == ModifierOp.Multiply)
        {
            // 倍率は % 表示（+0.2 → "+20%"）
            int pct = Mathf.RoundToInt(mod.value * 100f);
            string sign = pct >= 0 ? "+" : "";
            return $"{KindLabel(mod.kind)}{sign}{pct}%";
        }
        else
        {
            // 加算はそのまま整数
            string sign = mod.value >= 0 ? "+" : "";
            return $"{KindLabel(mod.kind)}{sign}{Mathf.RoundToInt(mod.value)}";
        }
    }

    private static string KindLabel(EffectKind kind)
    {
        switch (kind)
        {
            case EffectKind.Grades:        return "成績";
            case EffectKind.Stress:        return "ストレス";
            case EffectKind.Health:        return "体調";
            case EffectKind.Money:         return "給料";
            case EffectKind.Hobby:         return "趣味";
            case EffectKind.GoodEventRate: return "好イベ率";
            case EffectKind.BadEventRate:  return "悪イベ率";
            default:                       return kind.ToString();
        }
    }
}
