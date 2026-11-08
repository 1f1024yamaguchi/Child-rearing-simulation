using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 今どのページかを示す点（● ○ ○）。点はすべて Canvas 上の Image。
/// 子に置いた「見本の点（DotTemplate）」を複製して、ページ数ぶん横に並べる（並べ方は同じオブジェクトの HorizontalLayoutGroup）。
/// 点の見た目を変えたいときは、見本の点の大きさ・画像を変えるか、下の画像・色の欄を変える。
/// </summary>
public class TutorialPageDots : MonoBehaviour
{
    [Tooltip("見本の点（子に置いた Image）。非表示にしておき、これを複製して並べる")]
    [SerializeField] private Image dotTemplate;

    [Tooltip("今のページの点の画像（未設定なら見本の点の画像のまま、色だけ変える）")]
    [SerializeField] private Sprite activeSprite;
    [Tooltip("ほかのページの点の画像（未設定なら見本の点の画像のまま、色だけ変える）")]
    [SerializeField] private Sprite inactiveSprite;

    [Tooltip("今のページの点の色")]
    [SerializeField] private Color activeColor = new Color32(92, 158, 76, 255);
    [Tooltip("ほかのページの点の色")]
    [SerializeField] private Color inactiveColor = new Color32(200, 200, 200, 255);

    [Tooltip("ページが1枚だけのときは点を出さない")]
    [SerializeField] private bool hideWhenSinglePage = true;

    private readonly List<Image> dots = new List<Image>();

    /// ページ数ぶんの点を用意する。足りなければ見本から複製し、余った点は隠す。
    public void Build(int count)
    {
        if (dotTemplate == null)
        {
            Debug.LogWarning("[TutorialPageDots] 見本の点（Dot Template）が未設定です", this);
            return;
        }

        CollectDots();
        dotTemplate.gameObject.SetActive(false);

        while (dots.Count < count)
        {
            var dot = Instantiate(dotTemplate, transform);
            dot.name = "Dot_" + (dots.Count + 1);
            dots.Add(dot);
        }

        bool show = !(hideWhenSinglePage && count <= 1);
        for (int i = 0; i < dots.Count; i++)
            dots[i].gameObject.SetActive(show && i < count);
    }

    /// 今のページの点だけ色（画像）を変える。
    public void SetCurrent(int index)
    {
        for (int i = 0; i < dots.Count; i++)
        {
            bool on = i == index;
            var sprite = on ? activeSprite : inactiveSprite;
            if (sprite != null) dots[i].sprite = sprite;
            dots[i].color = on ? activeColor : inactiveColor;
        }
    }

    // すでに並んでいる点（見本以外の子の Image）を拾い直す。エディタのプレビューで作った点も使い回す
    private void CollectDots()
    {
        dots.Clear();
        foreach (Transform child in transform)
        {
            if (child == dotTemplate.transform) continue;
            var img = child.GetComponent<Image>();
            if (img != null) dots.Add(img);
        }
    }
}
