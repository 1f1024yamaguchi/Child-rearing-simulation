using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 家計簿の1行（通常行・見出し行・折りたたみ行を兼ねる）。
/// </summary>
public class LedgerRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private TMP_Text noteText;
    [SerializeField] private Button button;       // アコーディオン等のクリック用（任意）
    [SerializeField] private Image bgImage;

    public Button ClickButton => button;

    /// <summary>汎用セットアップ。</summary>
    public void Setup(string name, string amountStr, string note, Color amountColor, bool isHeader = false)
    {
        if (nameText != null)
        {
            nameText.text = name;
            nameText.fontStyle = isHeader ? FontStyles.Bold : FontStyles.Normal;
        }

        if (amountText != null)
        {
            amountText.text = amountStr;
            amountText.color = amountColor;
            amountText.fontStyle = isHeader ? FontStyles.Bold : FontStyles.Normal;
        }

        if (noteText != null)
        {
            bool hasNote = !string.IsNullOrEmpty(note);
            noteText.gameObject.SetActive(hasNote);
            if (hasNote) noteText.text = note;
        }

        if (bgImage != null)
        {
            bgImage.enabled = isHeader;
        }
    }
}
