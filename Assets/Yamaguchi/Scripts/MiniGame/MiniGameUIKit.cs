using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ミニゲームの UI を組み立てる小さな部品集。
/// 絵はすべて MiniGameArt のドット絵スプライトを使い、コードで絵を描かない。
/// ドット絵は「元のドット数 × 整数倍」の大きさで置く（ぼやけ・不揃いを防ぐ）。
/// 枠やメーターは HUD と同じ平らな四角。
/// </summary>
public static class MiniGameUIKit
{
    private static TMP_FontAsset font;
    private static bool fontTried;

    public static TMP_FontAsset Font
    {
        get
        {
            if (!fontTried)
            {
                fontTried = true;
                font = Resources.Load<TMP_FontAsset>("JPFont");
            }
            return font;
        }
    }

    /// アンカーで範囲を指定した空の RectTransform。
    public static RectTransform MakeRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    /// 親の anchor の位置に、ピクセル指定の大きさで置く RectTransform。
    public static RectTransform MakeCenterRect(string name, Transform parent, Vector2 anchor, Vector2 size)
    {
        var rt = MakeRect(name, parent, anchor, anchor);
        rt.sizeDelta = size;
        return rt;
    }

    /// 平らな四角（枠・メーター用）。
    public static Image MakeImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Color color, bool raycast = false)
    {
        var rt = MakeRect(name, parent, anchorMin, anchorMax);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    /// ドット絵を整数倍で置く。sprite が無いときは非表示（差し替え待ち）。
    public static Image MakePixel(string name, Transform parent, Sprite sprite, int scale, Vector2 anchor)
    {
        return MakePixel(name, parent, sprite, scale, anchor, new Vector2(0.5f, 0.5f));
    }

    public static Image MakePixel(string name, Transform parent, Sprite sprite, int scale, Vector2 anchor, Vector2 pivot)
    {
        var rt = MakeRect(name, parent, anchor, anchor);
        rt.pivot = pivot;
        var img = rt.gameObject.AddComponent<Image>();
        img.raycastTarget = false;
        SetPixelSprite(img, sprite, scale);
        return img;
    }

    /// 絵を差し替える（コマ送りなど）。大きさも絵に合わせ直す。
    public static void SetPixelSprite(Image img, Sprite sprite, int scale)
    {
        if (img == null) return;
        img.sprite = sprite;
        img.enabled = sprite != null;
        if (sprite != null) img.rectTransform.sizeDelta = sprite.rect.size * scale;
    }

    /// 親いっぱいに背景のドット絵を敷く（縦横比を保ってはみ出す分は親のマスクで切る）。
    public static Image MakeBackdrop(string name, Transform parent, Sprite sprite, Color tint)
    {
        var rt = MakeRect(name, parent, Vector2.zero, Vector2.one);
        var img = rt.gameObject.AddComponent<Image>();
        img.raycastTarget = false;
        img.sprite = sprite;
        img.color = tint;
        img.enabled = sprite != null;
        if (sprite != null)
        {
            var fit = rt.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = sprite.rect.width / sprite.rect.height;
        }
        return img;
    }

    public static TextMeshProUGUI MakeLabel(string name, Transform parent, string text, float size, Color color,
        Vector2 anchorMin, Vector2 anchorMax, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var rt = MakeRect(name, parent, anchorMin, anchorMax);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (Font != null) t.font = Font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.fontStyle = FontStyles.Bold;   // HUD に合わせて太字（細字だと背景の上で読みにくい）
        return t;
    }

    public static Button MakeButton(string name, Transform parent, string label, float size, Color bg,
        Vector2 anchorMin, Vector2 anchorMax, UnityAction onClick)
    {
        var img = MakeImage(name, parent, anchorMin, anchorMax, bg, true);
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        btn.colors = colors;
        MakeLabel("Label", img.transform, label, size, Color.white, Vector2.zero, Vector2.one);
        btn.onClick.AddListener(onClick);
        return btn;
    }

    /// ドット絵の溝とつまみで作る横スライダー。
    public static Slider MakePixelSlider(string name, Transform parent, Vector2 anchor, Sprite track, Sprite knob,
        int scale, float min, float max, float value, UnityAction<float> onChanged)
    {
        Vector2 trackSize = track != null ? track.rect.size * scale : new Vector2(336f, 56f);
        Vector2 knobSize = knob != null ? knob.rect.size * scale : new Vector2(70f, 70f);

        var rt = MakeCenterRect(name, parent, anchor, new Vector2(trackSize.x, Mathf.Max(trackSize.y, knobSize.y)));
        var slider = rt.gameObject.AddComponent<Slider>();

        var bg = MakeCenterRect("Track", rt, new Vector2(0.5f, 0.5f), trackSize);
        var bgImg = bg.gameObject.AddComponent<Image>();
        bgImg.sprite = track;
        if (track == null) bgImg.color = new Color(0.8f, 0.8f, 0.85f);

        var area = MakeRect("Handle Slide Area", rt, Vector2.zero, Vector2.one);
        area.offsetMin = new Vector2(knobSize.x * 0.5f, 0f);
        area.offsetMax = new Vector2(-knobSize.x * 0.5f, 0f);

        // Slider はつまみを縦いっぱいに伸ばすので、高さは「つまみ − スライダー」の差分だけ指定する
        var handle = MakeRect("Handle", area, new Vector2(0f, 0f), new Vector2(0f, 1f));
        handle.sizeDelta = new Vector2(knobSize.x, knobSize.y - Mathf.Max(trackSize.y, knobSize.y));
        var hImg = handle.gameObject.AddComponent<Image>();
        hImg.sprite = knob;

        slider.targetGraphic = hImg;
        slider.handleRect = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;
        slider.onValueChanged.AddListener(onChanged);
        return slider;
    }

    /// 横バー。返した RectTransform を SetBar で伸び縮みさせる。
    public static RectTransform MakeBar(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Color back, Color fill)
    {
        var bg = MakeImage(name, parent, anchorMin, anchorMax, back);
        var f = MakeImage("Fill", bg.transform, Vector2.zero, Vector2.one, fill);
        return f.rectTransform;
    }

    public static void SetBar(RectTransform fill, float value01)
    {
        if (fill == null) return;
        fill.anchorMax = new Vector2(Mathf.Clamp01(value01), 1f);
    }

    public static void ClearChildren(Transform t)
    {
        if (t == null) return;
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            var c = t.GetChild(i).gameObject;
            c.SetActive(false);
            Object.Destroy(c);
        }
    }
}

/// 押している間だけ IsHeld が true になる部品（長押し用）。指が外へ出たら離した扱い。
public class HoldRelay : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public bool IsHeld { get; private set; }

    public void OnPointerDown(PointerEventData eventData) { IsHeld = true; }
    public void OnPointerUp(PointerEventData eventData) { IsHeld = false; }
    public void OnPointerExit(PointerEventData eventData) { IsHeld = false; }
    private void OnDisable() { IsHeld = false; }
}

/// 押された瞬間を知らせるだけの部品（画面タップ用）。
public class PointerDownRelay : MonoBehaviour, IPointerDownHandler
{
    public System.Action onDown;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (onDown != null) onDown();
    }
}
