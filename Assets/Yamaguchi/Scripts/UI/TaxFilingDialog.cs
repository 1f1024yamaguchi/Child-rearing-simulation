using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 年末調整の書類と確定申告の画面（taxes.md 6章）。TaxManager の依頼を受けて開く。
/// 構成：このオブジェクト（常に有効・描画なし）＋ 子の window（開いている間だけ有効）。
/// 開いている間は時間を止める（0004）。どの閉じ方でも Close を通るので PopPause を呼び忘れない。
/// 書類を出す・申告するのは TaxManager に頼むだけ（お金は触らない）。
/// </summary>
public class TaxFilingDialog : MonoBehaviour
{
    private enum Mode { None, YearEnd, TaxReturn }

    [Header("ウィンドウ（開いている間だけ有効にする）")]
    [SerializeField] private GameObject window;

    [Header("文字")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text previewText;

    [Header("控除の行")]
    [SerializeField] private RectTransform rowsParent;
    [SerializeField] private TaxDeductionRowUI rowTemplate;   // rowsParent の子に置いた非表示の行

    [Header("ボタン")]
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_Text cancelLabel;
    [SerializeField] private Button okButton;
    [SerializeField] private TMP_Text okLabel;

    [Header("文言")]
    [TextArea] [SerializeField] private string yearEndBody =
        "会社に出すと、12月の給料で1年分の所得税が計算し直されます。\n使える控除にチェックを入れてください（出し忘れても確定申告で取り戻せます）。";
    [TextArea] [SerializeField] private string taxReturnBody =
        "年末調整で出せなかった控除は、3月15日までに確定申告をすると所得税が戻ります。";

    private readonly List<TaxDeductionRowUI> rows = new List<TaxDeductionRowUI>();
    private Mode mode = Mode.None;
    private int year;
    private bool paused;
    private bool subscribed;

    public bool IsOpen => window != null && window.activeSelf;

    // ------------------------------------------------------------
    // ライフサイクル
    // ------------------------------------------------------------

    private void Awake()
    {
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancel);
        if (okButton != null) okButton.onClick.AddListener(OnOk);
        if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        if (window != null) window.SetActive(false);
    }

    private void Start()
    {
        transform.SetAsLastSibling();   // 他の画面より手前に出す
        Subscribe();
    }

    private void OnEnable() => Subscribe();

    private void OnDisable()
    {
        Close();
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (subscribed || TaxManager.I == null) return;
        TaxManager.I.OnYearEndFormRequested += OpenYearEnd;
        TaxManager.I.OnTaxReturnRequested += HandleTaxReturnRequested;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (TaxManager.I != null)
        {
            TaxManager.I.OnYearEndFormRequested -= OpenYearEnd;
            TaxManager.I.OnTaxReturnRequested -= HandleTaxReturnRequested;
        }
        subscribed = false;
    }

    // ------------------------------------------------------------
    // 公開窓口
    // ------------------------------------------------------------

    /// <summary>年末調整の書類（taxYear 歳分）を開く。</summary>
    public void OpenYearEnd(int taxYear)
    {
        if (IsOpen || TaxManager.I == null) return;
        Open(Mode.YearEnd, taxYear, TaxManager.I.GetOptions(taxYear, false));
    }

    /// <summary>確定申告（taxYear 歳分）を開く。ボタンなどから呼んでもよい。</summary>
    public void OpenTaxReturn(int taxYear)
    {
        if (IsOpen || TaxManager.I == null) return;
        Open(Mode.TaxReturn, taxYear, TaxManager.I.GetOptions(taxYear, true));
    }

    private void HandleTaxReturnRequested(int taxYear, bool reminder) => OpenTaxReturn(taxYear);

    // ------------------------------------------------------------
    // 開く・閉じる
    // ------------------------------------------------------------

    private void Open(Mode m, int taxYear, List<DeductionOption> options)
    {
        mode = m;
        year = taxYear;

        bool yearEnd = m == Mode.YearEnd;
        if (titleText != null) titleText.text = yearEnd ? $"年末調整の書類（{taxYear}歳分）" : $"確定申告（{taxYear}歳分・3月15日まで）";
        if (bodyText != null) bodyText.text = yearEnd ? yearEndBody : taxReturnBody;
        if (cancelLabel != null) cancelLabel.text = yearEnd ? "出さない" : "あとで";
        if (okLabel != null) okLabel.text = yearEnd ? "提出する" : "申告する";

        BuildRows(options);
        if (window != null) window.SetActive(true);
        if (!paused && GameClock.I != null)
        {
            GameClock.I.PushPause();
            paused = true;
        }
        OnRowsChanged();
        if (window != null && window.transform is RectTransform rt) LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    public void Close()
    {
        if (window != null && window.activeSelf) window.SetActive(false);
        mode = Mode.None;
        if (paused)
        {
            paused = false;
            if (GameClock.I != null) GameClock.I.PopPause();
        }
    }

    private void BuildRows(List<DeductionOption> options)
    {
        foreach (var r in rows)
        {
            if (r == null) continue;
            r.gameObject.SetActive(false);
            Destroy(r.gameObject);
        }
        rows.Clear();
        if (rowTemplate == null || rowsParent == null) return;

        foreach (var o in options)
        {
            var row = Instantiate(rowTemplate, rowsParent);
            row.gameObject.SetActive(true);
            row.Bind(o, OnRowsChanged);
            rows.Add(row);
        }
    }

    // ------------------------------------------------------------
    // ボタン
    // ------------------------------------------------------------

    private void OnRowsChanged()
    {
        var picks = Picks();
        if (okButton != null) okButton.interactable = mode == Mode.YearEnd || picks.Count > 0;

        if (previewText == null) return;
        if (mode == Mode.TaxReturn && TaxManager.I != null)
        {
            int refund = TaxManager.I.PreviewTaxReturn(year, picks);
            int resident = TaxManager.I.PreviewResidentReduction(year, picks);
            previewText.text = picks.Count == 0
                ? "申告する控除にチェックを入れてください"
                : $"戻ってくる所得税：{refund:+#,0;-#,0;0}円　6月からの住民税：1年で {resident:N0}円 安くなる見込み";
        }
        else
        {
            previewText.text = "12月の給料日に、引きすぎた所得税が戻ります";
        }
    }

    private List<(TaxDeduction deduction, ParentRole role)> Picks()
    {
        var list = new List<(TaxDeduction, ParentRole)>();
        foreach (var r in rows)
        {
            if (r != null && r.IsChecked && r.Deduction != null) list.Add((r.Deduction, r.Role));
        }
        return list;
    }

    private void OnOk()
    {
        var m = mode;
        int y = year;
        var picks = Picks();
        Close();
        if (TaxManager.I == null) return;
        if (m == Mode.YearEnd) TaxManager.I.SubmitYearEndForm(y, picks);
        else if (m == Mode.TaxReturn) TaxManager.I.FileTaxReturn(y, picks);
    }

    private void OnCancel()
    {
        var m = mode;
        int y = year;
        Close();
        // 年末調整は「出さない」と決めたことを記録する。確定申告は「あとで」なので何もしない
        if (m == Mode.YearEnd && TaxManager.I != null) TaxManager.I.SubmitYearEndForm(y, null);
    }
}
