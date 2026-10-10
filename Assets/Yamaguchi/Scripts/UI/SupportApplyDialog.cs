using System.Collections.Generic;
using Maruyama.Core;
using Maruyama.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 制度の申請・更新・手続きの確認ダイアログ（support-apply.md 7.3、ステップ3後半）。
/// SupportPanelController が各カードの OnPrimaryClicked を購読して Open を呼ぶ。
/// OK を押したら SupportManager.Apply / Renew / Resume を呼ぶだけ（GameState は触らない）。
/// 時間は、親のサポート画面が止めているのでここでは PushPause しない。
/// 構成：このオブジェクト（常に有効・描画なし）＋ 子の window（開いている間だけ有効）。
/// 親の画面が閉じたら window も閉じる（OnDisable）。
/// </summary>
public class SupportApplyDialog : MonoBehaviour
{
    [Header("ウィンドウ（開いている間だけ有効にする）")]
    [SerializeField] private GameObject window;
    [SerializeField] private Button dimButton;          // 背景の暗い部分。押すと［やめる］と同じ

    [Header("見出し")]
    [SerializeField] private TMP_Text titleText;        // 「児童手当を申請しますか？」
    [SerializeField] private TMP_Text timingText;       // 「（来月から）」「（今すぐ）」

    [Header("もらえるもの")]
    [SerializeField] private GameObject meritSection;
    [SerializeField] private RectTransform meritRow;

    [Header("かかるもの")]
    [SerializeField] private GameObject costSection;
    [SerializeField] private RectTransform costRow;

    [Header("支給表・注意")]
    [SerializeField] private TMP_Text tierText;         // 支給表がある制度だけ
    [SerializeField] private GameObject noteSection;
    [SerializeField] private TMP_Text noteText;         // demerits（注意）

    [Header("ボタン")]
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button okButton;
    [SerializeField] private TMP_Text okLabel;

    [Header("効果チップ")]
    [SerializeField] private EffectChipUI chipPrefab;   // Assets/Maruyama/Prefabs/Events/EffectChip.prefab（変更せず参照だけ）
    [SerializeField] private string monthlyPrefix = "毎月 ";

    private SupportScheme scheme;
    private SupportSchemeCard.PrimaryAction action = SupportSchemeCard.PrimaryAction.None;

    // ------------------------------------------------------------
    // ライフサイクル
    // ------------------------------------------------------------

    private void Awake()
    {
        // onClick はシリアライズされないので実行時に配線する
        if (cancelButton != null) cancelButton.onClick.AddListener(Close);
        if (dimButton != null) dimButton.onClick.AddListener(Close);
        if (okButton != null) okButton.onClick.AddListener(Confirm);
        if (window != null) window.SetActive(false);
    }

    // 親のサポート画面が閉じたら、開きっぱなしにならないよう閉じる
    private void OnDisable() => Close();

    private void OnDestroy()
    {
        if (cancelButton != null) cancelButton.onClick.RemoveListener(Close);
        if (dimButton != null) dimButton.onClick.RemoveListener(Close);
        if (okButton != null) okButton.onClick.RemoveListener(Confirm);
    }

    // ------------------------------------------------------------
    // 公開窓口
    // ------------------------------------------------------------

    public bool IsOpen => window != null && window.activeSelf;

    public void Open(SupportScheme s, SupportSchemeCard.PrimaryAction a)
    {
        if (s == null || a == SupportSchemeCard.PrimaryAction.None) return;
        scheme = s;
        action = a;

        string verb, ok;
        switch (a)
        {
            case SupportSchemeCard.PrimaryAction.Renew:  verb = "を更新しますか？";       ok = "更新する";   break;
            case SupportSchemeCard.PrimaryAction.Resume: verb = "の手続きをしますか？";   ok = "手続きする"; break;
            default:                                     verb = "を申請しますか？";       ok = "申請する";   break;
        }
        if (titleText != null) titleText.text = s.title + verb;
        if (okLabel != null) okLabel.text = ok;

        bool isApply = a == SupportSchemeCard.PrimaryAction.Apply;

        // 「（来月から）」「（今すぐ）」は申請のときだけ
        if (timingText != null)
        {
            timingText.gameObject.SetActive(isApply);
            timingText.text = s.effectTiming == EffectTiming.Monthly ? "（来月から）" : "（今すぐ）";
        }

        // 効果は申請のときだけ見せる（更新・再開は新しく起きることが無い）
        var merits = new List<(Effect effect, bool monthly)>();
        var costs = new List<(Effect effect, bool monthly)>();
        if (isApply) CollectEffects(s, merits, costs);
        FillChips(meritRow, merits);
        FillChips(costRow, costs);
        if (meritSection != null) meritSection.SetActive(merits.Count > 0);
        if (costSection != null) costSection.SetActive(costs.Count > 0);

        // 支給表の行（カードと同じ文）
        string tier = SupportScheduleText.TierLine(s);
        if (tierText != null)
        {
            tierText.text = tier;
            tierText.gameObject.SetActive(!string.IsNullOrEmpty(tier));
        }

        // 注意（demerits）
        string note = BuildNote(s);
        if (noteText != null) noteText.text = note;
        if (noteSection != null) noteSection.SetActive(!string.IsNullOrEmpty(note));

        if (window != null) window.SetActive(true);
        if (window != null && window.transform is RectTransform rt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    public void Close()
    {
        if (window != null && window.activeSelf) window.SetActive(false);
        scheme = null;
        action = SupportSchemeCard.PrimaryAction.None;
    }

    // ------------------------------------------------------------
    // OK
    // ------------------------------------------------------------

    private void Confirm()
    {
        var s = scheme;
        var a = action;
        Close();   // 先に閉じる（呼んだ結果の OnStateChanged でカードが更新される）

        var mgr = SupportManager.I;
        if (s == null || mgr == null) return;
        switch (a)
        {
            case SupportSchemeCard.PrimaryAction.Apply:  mgr.Apply(s);  break;
            case SupportSchemeCard.PrimaryAction.Renew:  mgr.Renew(s);  break;
            case SupportSchemeCard.PrimaryAction.Resume: mgr.Resume(s); break;
        }
    }

    // ------------------------------------------------------------
    // 内容を作る
    // ------------------------------------------------------------

    /// <summary>申請したときの効果（effects と、Monthly なら支給表の最初の段）をプラス／マイナスに分ける。</summary>
    private static void CollectEffects(SupportScheme s,
        List<(Effect, bool)> merits, List<(Effect, bool)> costs)
    {
        AddSorted(s.effects, false, merits, costs);

        // カードと同じ：「毎月」は支給表の最初の段の効果にだけ付ける
        if (s.effectTiming == EffectTiming.Monthly && s.monthlyTiers != null)
        {
            foreach (var t in s.monthlyTiers)
            {
                if (t == null || t.monthlyEffects == null || t.monthlyEffects.Count == 0) continue;
                AddSorted(t.monthlyEffects, true, merits, costs);
                break;
            }
        }
    }

    private static void AddSorted(List<Effect> effects, bool monthly,
        List<(Effect, bool)> merits, List<(Effect, bool)> costs)
    {
        if (effects == null) return;
        foreach (var e in effects)
        {
            if (e == null) continue;
            (IsDemerit(e) ? costs : merits).Add((e, monthly));
        }
    }

    /// <summary>EffectChipUI と同じ判定：ストレスは増えるとマイナス、それ以外は減るとマイナス。</summary>
    private static bool IsDemerit(Effect e)
    {
        return (e.kind == EffectKind.Stress && e.value > 0) ||
               (e.kind != EffectKind.Stress && e.value < 0);
    }

    private static string BuildNote(SupportScheme s)
    {
        if (s.demerits == null) return "";
        var lines = new List<string>();
        foreach (var d in s.demerits)
        {
            if (!string.IsNullOrEmpty(d)) lines.Add(d);
        }
        return string.Join("\n", lines);
    }

    private void FillChips(RectTransform row, List<(Effect effect, bool monthly)> items)
    {
        if (row == null) return;

        // 前のチップを消す（Destroy はフレームの最後なので、先にレイアウトから外す）
        for (int i = row.childCount - 1; i >= 0; i--)
        {
            var child = row.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        if (chipPrefab == null) return;

        foreach (var (effect, monthly) in items)
        {
            var chip = Instantiate(chipPrefab, row);
            chip.SetEffect(effect);
            if (!monthly) continue;
            var label = chip.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = monthlyPrefix + label.text;
        }
    }
}
