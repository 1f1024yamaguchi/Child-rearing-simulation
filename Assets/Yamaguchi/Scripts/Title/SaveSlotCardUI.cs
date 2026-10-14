using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// セーブ選択カード1枚の表示だけを受け持つ。
/// 固定の文字（「オートセーブ」など）とアイコンはシーン側。ここで触るのはセーブごとに変わる値（年齢・お金・保存日時）だけ。
/// </summary>
public class SaveSlotCardUI : MonoBehaviour
{
    [Tooltip("カード全体の Button。OnClick に SaveSelectPanelUI の OnClickAutoSlot / OnClickManualSlot を登録する。")]
    [SerializeField] private Button button;

    [Tooltip("年齢と日付を入れる TMP（例：8歳 3月1日）。")]
    [SerializeField] private TMP_Text ageText;

    [Tooltip("お金を入れる TMP（例：1,234,000円）。")]
    [SerializeField] private TMP_Text moneyText;

    [Tooltip("保存した日時を入れる TMP（例：2026/10/14 14:55 に保存）。")]
    [SerializeField] private TMP_Text savedAtText;

    [Tooltip("値の TMP をまとめた GameObject。セーブが無いときは隠す。")]
    [SerializeField] private GameObject content;

    [Tooltip("「データがありません」と書いた GameObject（文字はシーンの TMP に直接書く）。セーブが無いときだけ表示。")]
    [SerializeField] private GameObject emptyLabel;

    public Button Button => button;

    public void Show(SaveSlotInfo info)
    {
        bool exists = info != null && info.exists;

        if (content != null) content.SetActive(exists);
        if (emptyLabel != null) emptyLabel.SetActive(!exists);
        if (button != null) button.interactable = exists;
        if (!exists) return;

        if (ageText != null) ageText.text = GameDate.Format(info.ageDays);
        if (moneyText != null) moneyText.text = info.money.ToString("N0") + "円";
        if (savedAtText != null)
        {
            var t = SaveSystem.ParseSavedAt(info.savedAt);
            savedAtText.text = t == System.DateTime.MinValue ? "" : t.ToString("yyyy/MM/dd HH:mm") + " に保存";
        }
    }
}
