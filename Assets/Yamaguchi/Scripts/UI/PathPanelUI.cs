using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 進路の判断・お知らせの専用パネル（ending.md 依頼6）。イベントのダイアログ（EventManager）は使わないので「[メリット]」の帯は出ない。
/// ・選択肢のカードを横に並べる。カードごとに「模試の判定（A〜E）」「★本人の希望」「選べない理由」を出す。
/// ・開いている間は時間を止める（GameClock.PushPause）。選ぶまで閉じられない。
/// ・同時に複数の依頼が来たら順番に出す。
/// ・画面は自分で作る（シーンに置かない）。本編シーンの "School" オブジェクトに SchoolBootstrap が付ける。
/// </summary>
public class PathPanelUI : MonoBehaviour
{
    public static PathPanelUI I { get; private set; }

    /// <summary>カード1枚。</summary>
    public class Option
    {
        public string title = "";       // 「大学」
        public string sub = "";         // 「4年通う」
        public string rank = "";        // 「B」（空なら判定欄は出さない）
        public bool wish;               // ★本人の希望
        public string note = "";        // 補足・選べない理由
        public bool enabled = true;
        public string buttonLabel = "これにする";
        public Action onSelect;
    }

    private class Request
    {
        public string title, description;
        public List<Option> options;
        public bool isNotice;
        public Sprite image;
    }

    private readonly Queue<Request> queue = new Queue<Request>();
    private Request current;
    private bool paused;

    private GameObject root;
    private TextMeshProUGUI titleText, descText;
    private Image pictureImage;
    private RectTransform cardsRow, panelRect;
    private TMP_FontAsset font;

    private static readonly Color Ink = new Color(0.16f, 0.16f, 0.16f, 1f);
    private static readonly Color Gray = new Color(0.45f, 0.45f, 0.45f, 1f);
    private static readonly Color Orange = new Color(0.93f, 0.52f, 0.05f, 1f);
    private static readonly Color PanelColor = new Color(1f, 0.965f, 0.9f, 1f);
    private static readonly Color ButtonColor = new Color(1f, 0.93f, 0.8f, 1f);

    public bool IsOpen => current != null;

    // ─── 使う側 ─────────────────────────────────
    /// <summary>パネルを出す（開いていれば順番待ち）。{name} などは StoryTextFormatter で置き換える。</summary>
    public static void Show(string title, string description, List<Option> options, Sprite image = null)
    {
        if (I == null)
        {
            Debug.LogWarning($"[PathPanelUI] 画面（School オブジェクト）が無いので出せません：{title}");
            return;
        }
        I.Enqueue(new Request { title = title, description = description, options = options, image = image });
    }

    /// <summary>お知らせ1枚（ボタン1つだけ）。画像があれば大きく出す。</summary>
    public static void ShowNotice(string title, string description, Sprite image = null, string button = "わかった")
    {
        if (I == null) { Debug.LogWarning($"[PathPanelUI] 画面（School オブジェクト）が無いので出せません：{title}"); return; }
        I.Enqueue(new Request { title = title, description = description, isNotice = true, image = image, options = new List<Option> { new Option { buttonLabel = string.IsNullOrEmpty(button) ? "わかった" : button } } });
    }

    /// <summary>デバッグ用：開いているパネルを、人の代わりに閉じる（★の希望があればそれ、無ければ最初に選べるもの）。</summary>
    public bool AutoResolve()
    {
        if (current == null) return false;
        Option pick = null;
        foreach (var o in current.options) if (o.enabled && o.wish) { pick = o; break; }
        if (pick == null) foreach (var o in current.options) if (o.enabled) { pick = o; break; }
        if (pick == null) return false;
        Select(pick);
        return true;
    }

    // ─── ライフサイクル ─────────────────────────────────
    private void Awake()
    {
        if (I != null && I != this) { Destroy(this); return; }
        I = this;
        font = Resources.Load<TMP_FontAsset>("JPFont");
        BuildRoot();
    }

    private void OnDestroy()
    {
        if (I == this) I = null;
        if (paused && GameClock.I != null) GameClock.I.PopPause();
        paused = false;
    }

    private void Enqueue(Request r)
    {
        queue.Enqueue(r);
        if (current == null) ShowNext();
    }

    private void ShowNext()
    {
        if (queue.Count == 0) { current = null; return; }
        current = queue.Dequeue();
        if (!paused && GameClock.I != null) { GameClock.I.PushPause(); paused = true; }

        titleText.text = StoryTextFormatter.Format(current.title);
        descText.text = StoryTextFormatter.Format(current.description ?? "");
        descText.gameObject.SetActive(!string.IsNullOrEmpty(current.description));

        for (int i = cardsRow.childCount - 1; i >= 0; i--) Destroy(cardsRow.GetChild(i).gameObject);
        // お知らせは小さな板にボタン1つだけ（画像があれば真ん中に大きく）。判断はカードを横に並べる（画像は左上に小さく）
        Layout(current.isNotice, current.image);
        cardsRow.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = !current.isNotice;
        cardsRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = !current.isNotice;
        descText.color = current.isNotice ? Ink : Gray;
        descText.fontSize = current.isNotice ? 30 : 24;
        cardsRow.GetComponent<HorizontalLayoutGroup>().childAlignment = current.isNotice ? TextAnchor.MiddleCenter : TextAnchor.UpperLeft;
        foreach (var o in current.options) { if (current.isNotice) BuildNoticeButton(o); else BuildCard(o); }
        root.SetActive(true);
    }

    private void Layout(bool notice, Sprite image)
    {
        bool hasImage = image != null;
        pictureImage.sprite = image;
        pictureImage.gameObject.SetActive(hasImage);
        var ir = pictureImage.rectTransform;
        if (notice)
        {
            panelRect.sizeDelta = hasImage ? new Vector2(1000, 660) : new Vector2(1000, 360);
            if (hasImage)
            {
                ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0.5f, 1f);
                ir.anchoredPosition = new Vector2(0, -80);
                ir.sizeDelta = new Vector2(600, 320);
            }
            SetTop(descText.rectTransform, hasImage ? -410 : -84, 80, 60);
            cardsRow.offsetMax = new Vector2(-30, hasImage ? -500 : -156);
        }
        else
        {
            panelRect.sizeDelta = new Vector2(1160, 600);
            if (hasImage)
            {
                ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0f, 1f);
                ir.anchoredPosition = new Vector2(24, -14);
                ir.sizeDelta = new Vector2(150, 130);
            }
            SetTop(descText.rectTransform, -84, 64, hasImage ? 190 : 60);
            cardsRow.offsetMax = new Vector2(-30, -156);
        }
    }

    private void Select(Option o)
    {
        root.SetActive(false);
        current = null;
        if (paused && GameClock.I != null) { GameClock.I.PopPause(); }
        paused = false;
        try { o.onSelect?.Invoke(); }
        catch (Exception e) { Debug.LogException(e); }
        ShowNext();   // 選んだ結果で新しい依頼が入っていれば、続けて出る
    }

    // ─── 画面を作る ─────────────────────────────────
    private void BuildRoot()
    {
        root = new GameObject("PathPanelCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;   // 横幅に合わせる（4:3 でも横がはみ出さない）

        var dim = MakeImage(root.transform, "Dim", new Color(0, 0, 0, 0.55f));
        Stretch(dim.rectTransform);

        var panel = MakeImage(root.transform, "Panel", PanelColor);
        var pr = panel.rectTransform;
        pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(1160, 600);
        panelRect = pr;

        titleText = MakeText(panel.transform, "Title", "", 40, TextAlignmentOptions.Center, FontStyles.Bold);
        SetTop(titleText.rectTransform, -22, 56, 40);
        descText = MakeText(panel.transform, "Description", "", 24, TextAlignmentOptions.Center, FontStyles.Normal);
        descText.color = Gray;
        descText.enableWordWrapping = true;
        SetTop(descText.rectTransform, -84, 64, 60);

        pictureImage = MakeImage(panel.transform, "Picture", Color.white);
        pictureImage.preserveAspect = true;
        pictureImage.raycastTarget = false;
        pictureImage.gameObject.SetActive(false);

        var row = new GameObject("Cards", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(panel.transform, false);
        cardsRow = row.GetComponent<RectTransform>();
        cardsRow.anchorMin = new Vector2(0, 0);
        cardsRow.anchorMax = new Vector2(1, 1);
        cardsRow.offsetMin = new Vector2(30, 24);
        cardsRow.offsetMax = new Vector2(-30, -156);
        var h = row.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 20;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = true;

        root.SetActive(false);
    }

    private void BuildCard(Option o)
    {
        var card = MakeImage(cardsRow, "Card", o.enabled ? Color.white : new Color(0.9f, 0.9f, 0.9f, 1f));
        var cardLe = card.gameObject.AddComponent<LayoutElement>();
        cardLe.minWidth = 0; cardLe.preferredWidth = 0; cardLe.flexibleWidth = 1;   // どのカードも同じ幅
        var v = card.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(14, 14, 14, 16);
        v.spacing = 6;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        var badge = MakeText(card.transform, "Badge", o.wish ? "★ 本人の希望" : "", 24, TextAlignmentOptions.Center, FontStyles.Bold);
        badge.color = Orange;
        Height(badge, 32);

        var title = MakeText(card.transform, "Title", o.title, 36, TextAlignmentOptions.Center, FontStyles.Bold);
        title.color = o.enabled ? Ink : Gray;
        title.enableAutoSizing = true;   // 長い名前（推薦（スポーツ・芸術）など）は縮めて1行に収める
        title.fontSizeMin = 18; title.fontSizeMax = 36;
        Height(title, 52);

        var sub = MakeText(card.transform, "Sub", o.sub, 22, TextAlignmentOptions.Center, FontStyles.Normal);
        sub.color = Gray;
        Height(sub, 30);

        if (!string.IsNullOrEmpty(o.rank))
        {
            var rank = MakeText(card.transform, "Rank", $"<size=88><b>{o.rank}</b></size><size=32>判定</size>", 32, TextAlignmentOptions.Center, FontStyles.Normal);
            rank.color = o.enabled ? RankColor(o.rank) : Gray;
            Height(rank, 112);
        }

        var note = MakeText(card.transform, "Note", o.note, 21, TextAlignmentOptions.Top, FontStyles.Normal);
        note.color = o.enabled ? Gray : new Color(0.8f, 0.25f, 0.2f, 1f);
        note.enableWordWrapping = true;
        var ne = note.gameObject.AddComponent<LayoutElement>();
        ne.flexibleHeight = 1; ne.minHeight = 20;

        var btnImg = MakeImage(card.transform, "Button", o.enabled ? ButtonColor : new Color(0.82f, 0.82f, 0.82f, 1f));
        var outline = btnImg.gameObject.AddComponent<Outline>();
        outline.effectColor = o.enabled ? Color.black : Gray;
        outline.effectDistance = new Vector2(2, -2);
        var btn = btnImg.gameObject.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.interactable = o.enabled;
        var label = MakeText(btnImg.transform, "Label", o.enabled ? o.buttonLabel : "選べない", 28, TextAlignmentOptions.Center, FontStyles.Bold);
        label.color = o.enabled ? Ink : Gray;
        Stretch(label.rectTransform);
        var le = btnImg.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = le.minHeight = 58;
        var captured = o;
        btn.onClick.AddListener(() => Select(captured));
    }

    private void BuildNoticeButton(Option o)
    {
        var btnImg = MakeImage(cardsRow, "Button", ButtonColor);
        var outline = btnImg.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, -2);
        var btn = btnImg.gameObject.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        var label = MakeText(btnImg.transform, "Label", o.buttonLabel, 30, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(label.rectTransform);
        var le = btnImg.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 320; le.preferredHeight = 72;
        var captured = o;
        btn.onClick.AddListener(() => Select(captured));
    }

    private static Color RankColor(string r)
    {
        switch (r)
        {
            case "A": return new Color(0.16f, 0.62f, 0.34f, 1f);
            case "B": return new Color(0.2f, 0.55f, 0.75f, 1f);
            case "C": return new Color(0.78f, 0.6f, 0.05f, 1f);
            case "D": return new Color(0.9f, 0.45f, 0.1f, 1f);
            default:  return new Color(0.82f, 0.2f, 0.2f, 1f);
        }
    }

    // ─── UI 部品 ─────────────────────────────────
    private Image MakeImage(Transform parent, string name, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = c;
        return img;
    }

    private TextMeshProUGUI MakeText(Transform parent, string name, string text, float size, TextAlignmentOptions align, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.fontStyle = style;
        t.color = Ink;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    private static void Height(Graphic g, float h)
    {
        var le = g.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = le.minHeight = h;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void SetTop(RectTransform rt, float y, float height, float side)
    {
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, y);
        rt.sizeDelta = new Vector2(-side * 2, height);
    }
}
