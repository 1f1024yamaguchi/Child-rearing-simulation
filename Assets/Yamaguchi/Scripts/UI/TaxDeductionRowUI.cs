using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 申告画面の控除1行の見た目だけ（taxes.md 6章）。
/// ［チェック］控除名・所得から引く額・説明・［母／父］の切り替え。
/// </summary>
public class TaxDeductionRowUI : MonoBehaviour
{
    [SerializeField] private Toggle toggle;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Button whoButton;
    [SerializeField] private TMP_Text whoLabel;

    private DeductionOption option;
    private ParentRole role;
    private Action onChanged;

    public TaxDeduction Deduction => option != null ? option.deduction : null;
    public ParentRole Role => role;
    public bool IsChecked => toggle != null && toggle.isOn && toggle.interactable;

    private void Awake()
    {
        // onClick / onValueChanged はシリアライズされないので実行時に配線する
        if (toggle != null) toggle.onValueChanged.AddListener(_ => onChanged?.Invoke());
        if (whoButton != null) whoButton.onClick.AddListener(SwitchRole);
    }

    private void OnEnable()
    {
        // 複製した直後に TMP のメッシュが出ないことがある（ui-scroll-patterns.md）
        foreach (var t in GetComponentsInChildren<TMP_Text>(true))
        {
            t.SetAllDirty();
            t.ForceMeshUpdate();
        }
    }

    public void Bind(DeductionOption o, Action changed)
    {
        option = o;
        onChanged = null;   // 表示を作っている間は通知しない
        role = o.recommended;
        if (titleText != null) titleText.text = o.deduction.title;
        if (descriptionText != null) descriptionText.text = o.deduction.description;
        if (toggle != null) toggle.SetIsOnWithoutNotify(false);
        Refresh();
        onChanged = changed;
    }

    private void SwitchRole()
    {
        if (option == null) return;
        var other = role == ParentRole.Mother ? ParentRole.Father : ParentRole.Mother;
        if (!option.IsEligible(other)) return;
        role = other;
        Refresh();
        onChanged?.Invoke();
    }

    private void Refresh()
    {
        if (option == null) return;
        bool ok = option.IsEligible(role);
        if (toggle != null) toggle.interactable = ok;
        if (amountText != null) amountText.text = ok ? $"所得から −{Man(option.AmountFor(role))}" : "（この人は対象外）";
        if (whoLabel != null) whoLabel.text = TaxManager.RoleLabel(role);
        if (whoButton != null) whoButton.interactable = option.motherEligible && option.fatherEligible;
    }

    // 380000 → "38万円"、123456 → "12.3万円"
    private static string Man(int yen)
    {
        float man = yen / 10000f;
        return Mathf.Approximately(man, Mathf.Round(man)) ? $"{Mathf.RoundToInt(man)}万円" : $"{man:0.#}万円";
    }
}
