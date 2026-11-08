using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// チュートリアル画面（上に画像、下に見出しと説明文）。
/// 画像の枠を左右にスワイプ（ドラッグ）するか矢印を押すと、次のページの画像が横から滑って出てきて、
/// 見出し・説明文もそのページのものに切り替わる。最後のページで「わかった」を押すと閉じる。
///
/// 位置・大きさはすべて Canvas 上のオブジェクトで決める。このスクリプトは中身を差し替えて動かすだけで、配置はしない
/// （スライド中の画像3枚の横位置だけは、窓の幅に合わせてここで並べる）。
///
/// パネルのルート（開閉するオブジェクト）に付け、ふだんは非表示にしておく。
/// 表示している間は時間を止める（OnEnable で PushPause / OnDisable で PopPause。AGENTS.md ルール8）。
/// 開くのは TutorialManager から（直接 Open を呼んでもよい）。
/// </summary>
public class TutorialPanelUI : MonoBehaviour
{
    [Header("画像（スライドする部分）")]
    [Tooltip("指・マウスのドラッグを受け取る枠（TutorialSwipeArea）")]
    [SerializeField] private TutorialSwipeArea swipeArea;
    [Tooltip("はみ出しを隠す窓（RectMask2D）。この幅を1ページの幅として滑らせる")]
    [SerializeField] private RectTransform viewport;
    [Tooltip("3枚の画像を横に並べて動かす台（窓いっぱいの大きさ）")]
    [SerializeField] private RectTransform track;
    [Tooltip("前のページの画像（左に待機）")]
    [SerializeField] private TutorialImageSlot prevSlot;
    [Tooltip("今のページの画像")]
    [SerializeField] private TutorialImageSlot currentSlot;
    [Tooltip("次のページの画像（右に待機）")]
    [SerializeField] private TutorialImageSlot nextSlot;

    [Header("文字")]
    [SerializeField] private TMP_Text headingText;
    [SerializeField] private TMP_Text bodyText;
    [Tooltip("見出しと説明文をまとめた CanvasGroup。ページが変わったときにふわっと出す（未設定でも動く）")]
    [SerializeField] private CanvasGroup textGroup;

    [Header("操作")]
    [Tooltip("前のページへ（最初のページでは隠す）")]
    [SerializeField] private Button prevButton;
    [Tooltip("次のページへ（最後のページでは隠す）")]
    [SerializeField] private Button nextButton;
    [Tooltip("閉じるボタン。最後のページだけ表示する")]
    [SerializeField] private Button okButton;
    [Tooltip("ページ位置の点")]
    [SerializeField] private TutorialPageDots pageDots;

    [Header("動き")]
    [Tooltip("ページが滑りきるまでの時間（秒）")]
    [SerializeField] private float slideSeconds = 0.25f;
    [Tooltip("ページの幅に対して、どれだけドラッグしたら次のページへ進むか（0.2 = 2割）")]
    [Range(0.05f, 0.9f)][SerializeField] private float swipeThreshold = 0.2f;
    [Tooltip("この速さ（Canvas の単位/秒）以上ではじいたら、距離が短くても次へ進む")]
    [SerializeField] private float flickSpeed = 800f;
    [Tooltip("最初・最後のページでさらに引っぱったときの動きやすさ（1 = 抵抗なし、0 = 動かない）")]
    [Range(0f, 1f)][SerializeField] private float edgeResistance = 0.3f;
    [Tooltip("ページが変わったとき、見出し・説明文がふわっと出る時間（秒）")]
    [SerializeField] private float textFadeSeconds = 0.2f;

    private TutorialData data;
    private int index;
    private Action onClosed;
    private bool isPausing;
    private bool isDragging;
    private bool isSliding;
    private Coroutine slideRoutine;
    private Coroutine fadeRoutine;
    private bool wired;

    /// 開いているか。
    public bool IsOpen => gameObject.activeSelf && data != null;
    /// 表示中のチュートリアル（閉じていれば null）。
    public TutorialData Current => data;
    /// 表示中のページ番号（0 始まり）。
    public int PageIndex => index;

    private int LastIndex => data != null ? data.PageCount - 1 : 0;
    private float PageWidth => viewport != null ? viewport.rect.width : 0f;

    // ─── 開閉 ──────────────────────────────────────────────

    /// チュートリアルを1ページ目から表示する。閉じたら onClosed を呼ぶ。
    public void Open(TutorialData tutorial, Action closed = null)
    {
        if (tutorial == null || tutorial.PageCount == 0)
        {
            Debug.LogWarning("[TutorialPanelUI] ページの無いチュートリアルは開けません", this);
            closed?.Invoke();
            return;
        }

        data = tutorial;
        index = 0;
        onClosed = closed;
        gameObject.SetActive(true);
        WireOnce();
        ShowPage();
        SetTextAlpha(1f);
    }

    /// 閉じる（「わかった」ボタン）。
    public void Close()
    {
        var cb = onClosed;
        onClosed = null;
        data = null;
        gameObject.SetActive(false);
        cb?.Invoke();
    }

    /// 次のページへ（矢印・キーボード）。
    public void GoNext()
    {
        if (!CanControl() || index >= LastIndex) return;
        SlideTo(+1);
    }

    /// 前のページへ（矢印・キーボード）。
    public void GoPrev()
    {
        if (!CanControl() || index <= 0) return;
        SlideTo(-1);
    }

    // ─── Unity ────────────────────────────────────────────

    private void Awake() => WireOnce();

    private void Start()
    {
        // エディタのプレビューで表示したままシーンを保存しても、ゲーム開始時は閉じておく
        if (data == null) gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        if (!isPausing && GameClock.I != null)
        {
            GameClock.I.PushPause();
            isPausing = true;
        }
    }

    private void OnDisable()
    {
        if (isPausing && GameClock.I != null)
        {
            GameClock.I.PopPause();
        }
        isPausing = false;
        isDragging = false;
        isSliding = false;
        slideRoutine = null;
        fadeRoutine = null;
    }

    private void OnDestroy()
    {
        if (swipeArea != null)
        {
            swipeArea.DragBegan -= HandleDragBegan;
            swipeArea.Dragged -= HandleDragged;
            swipeArea.DragEnded -= HandleDragEnded;
        }
    }

    private void Update()
    {
        if (data == null) return;
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.rightArrowKey.wasPressedThisFrame) GoNext();
        else if (kb.leftArrowKey.wasPressedThisFrame) GoPrev();
        else if (index >= LastIndex && CanControl() &&
                 (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) Close();
    }

    // ボタンとスワイプの配線（onClick はシリアライズされないので実行時に繋ぐ）
    private void WireOnce()
    {
        if (wired || !Application.isPlaying) return;
        wired = true;

        if (prevButton != null) prevButton.onClick.AddListener(GoPrev);
        if (nextButton != null) nextButton.onClick.AddListener(GoNext);
        if (okButton != null) okButton.onClick.AddListener(Close);
        if (swipeArea != null)
        {
            swipeArea.DragBegan += HandleDragBegan;
            swipeArea.Dragged += HandleDragged;
            swipeArea.DragEnded += HandleDragEnded;
        }
    }

    // ─── 表示 ─────────────────────────────────────────────

    // 今のページと、その前後のページを並べ直す
    private void ShowPage()
    {
        if (data == null) return;

        if (prevSlot != null) prevSlot.Set(data.GetPage(index - 1));
        if (currentSlot != null) currentSlot.Set(data.GetPage(index));
        if (nextSlot != null) nextSlot.Set(data.GetPage(index + 1));
        PlaceSlots();
        SetTrackX(0f);

        var page = data.GetPage(index);
        if (headingText != null) headingText.text = page.heading ?? "";
        if (bodyText != null) bodyText.text = page.body ?? "";

        bool first = index <= 0;
        bool last = index >= LastIndex;
        if (prevButton != null) prevButton.gameObject.SetActive(!first);
        if (nextButton != null) nextButton.gameObject.SetActive(!last);
        if (okButton != null) okButton.gameObject.SetActive(last);

        if (pageDots != null)
        {
            pageDots.Build(data.PageCount);
            pageDots.SetCurrent(index);
        }
    }

    // 前・今・次の画像を、窓の幅ぶんずつ横に並べる
    private void PlaceSlots()
    {
        float w = PageWidth;
        SetSlotX(prevSlot, -w);
        SetSlotX(currentSlot, 0f);
        SetSlotX(nextSlot, w);
    }

    private static void SetSlotX(TutorialImageSlot slot, float x)
    {
        if (slot == null) return;
        var rt = slot.Rect;
        rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
    }

    private void SetTrackX(float x)
    {
        if (track == null) return;
        track.anchoredPosition = new Vector2(x, track.anchoredPosition.y);
    }

    private float TrackX => track != null ? track.anchoredPosition.x : 0f;

    private void SetTextAlpha(float a)
    {
        if (textGroup != null) textGroup.alpha = a;
    }

    // ─── スワイプ ─────────────────────────────────────────

    private bool CanControl() => data != null && !isSliding && !isDragging;

    private void HandleDragBegan()
    {
        if (data == null || isSliding) return;
        isDragging = true;
    }

    private void HandleDragged(float dx)
    {
        if (!isDragging) return;
        // 最初のページで右へ・最後のページで左へ引っぱったときは、少しだけ動く
        bool pullingPastEdge = (dx > 0f && index <= 0) || (dx < 0f && index >= LastIndex);
        SetTrackX(pullingPastEdge ? dx * edgeResistance : dx);
    }

    private void HandleDragEnded(float dx, float speed)
    {
        if (!isDragging) return;
        isDragging = false;

        float w = PageWidth;
        int dir = 0;
        bool farEnough = w > 0f && Mathf.Abs(dx) >= w * swipeThreshold;
        bool flicked = Mathf.Abs(speed) >= flickSpeed && Mathf.Sign(speed) == Mathf.Sign(dx);

        // 左へ動かした＝次のページ、右へ動かした＝前のページ
        if (dx < 0f && (farEnough || flicked) && index < LastIndex) dir = +1;
        else if (dx > 0f && (farEnough || flicked) && index > 0) dir = -1;

        SlideTo(dir);
    }

    // dir = +1 で次、-1 で前、0 で元の位置に戻す
    private void SlideTo(int dir)
    {
        if (slideRoutine != null) StopCoroutine(slideRoutine);
        slideRoutine = StartCoroutine(SlideRoutine(dir));
    }

    private IEnumerator SlideRoutine(int dir)
    {
        isSliding = true;
        float from = TrackX;
        float to = -dir * PageWidth;
        float t = 0f;
        float dur = Mathf.Max(0.01f, slideSeconds);

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);   // だんだん遅く止まる
            SetTrackX(Mathf.Lerp(from, to, e));
            yield return null;
        }
        SetTrackX(to);

        if (dir != 0)
        {
            index = Mathf.Clamp(index + dir, 0, LastIndex);
            ShowPage();   // 台を 0 に戻し、新しい「今のページ」を真ん中に入れ直す
            FadeInText();
        }
        else
        {
            SetTrackX(0f);
        }

        isSliding = false;
        slideRoutine = null;
    }

    private void FadeInText()
    {
        if (textGroup == null || textFadeSeconds <= 0f) return;
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / textFadeSeconds;
            SetTextAlpha(Mathf.Clamp01(t));
            yield return null;
        }
        SetTextAlpha(1f);
        fadeRoutine = null;
    }

    // ─── エディタのプレビュー用 ───────────────────────────

    /// 再生しないで Canvas 上に指定のページを表示する（チュートリアル編集ウィンドウから呼ぶ）。
    /// 再生中は使わない。表示したまま保存しても、ゲーム開始時に閉じる（Start）。
    public void PreviewInEditor(TutorialData tutorial, int page)
    {
        if (Application.isPlaying || tutorial == null || tutorial.PageCount == 0) return;
        data = tutorial;
        index = Mathf.Clamp(page, 0, tutorial.PageCount - 1);
        gameObject.SetActive(true);
        ShowPage();
        SetTextAlpha(1f);
    }

    /// エディタのプレビューを閉じる。
    public void ClearPreview()
    {
        if (Application.isPlaying) return;
        data = null;
        gameObject.SetActive(false);
    }
}
