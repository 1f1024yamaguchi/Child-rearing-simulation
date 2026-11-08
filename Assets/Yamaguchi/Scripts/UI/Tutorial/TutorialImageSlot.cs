using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// チュートリアルの画像1枚ぶんの見た目だけを持つ部品。
/// 画像があれば表示し、無ければ灰色の差し替え枠と「絵の指示メモ」を出す。ページが無ければ自分を隠す。
/// </summary>
public class TutorialImageSlot : MonoBehaviour
{
    [Tooltip("ドット絵を出す Image（この枠いっぱいに、縦横比を保って表示）")]
    [SerializeField] private Image image;

    [Tooltip("画像が未設定のときに出す差し替え枠（灰色の板）")]
    [SerializeField] private GameObject placeholder;

    [Tooltip("差し替え枠の中に出す「絵の指示メモ」")]
    [SerializeField] private TMP_Text placeholderNote;

    public RectTransform Rect => (RectTransform)transform;

    /// ページの中身を出す。null ならこの枠ごと隠す（最初・最後のページの外側）。
    public void Set(TutorialPage page)
    {
        if (page == null)
        {
            gameObject.SetActive(false);
            return;
        }
        gameObject.SetActive(true);

        bool hasImage = page.image != null;
        if (image != null)
        {
            image.sprite = page.image;
            image.enabled = hasImage;
            image.preserveAspect = true;
        }
        if (placeholder != null) placeholder.SetActive(!hasImage);
        if (placeholderNote != null) placeholderNote.text = page.imageNote ?? "";
    }
}
