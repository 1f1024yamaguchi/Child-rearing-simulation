using UnityEngine;
using LitMotion;
using LitMotion.Extensions;

[RequireComponent(typeof(RectTransform))]
public class SlidePanel : MonoBehaviour
{
    //画面の右外から横スライドして出し入れする
    //中身が何か重要ではないので他のモノに使える

    [Header("アニメーション")]
    [SerializeField] private float duration = 0.25f;
    [SerializeField] private Ease ease = Ease.OutCubic;
    [SerializeField] private bool openOnStart = false;
    [Tooltip("OnにするとTime.timeScaleの影響を受けない(ポーズ中もUIが動く)")]
    [SerializeField] private bool ignoreTimeScale = true;

    [Header("位置設定")]
    [Tooltip("自動計算せずに手動で開閉位置(anchoredPosition)を指定する場合はチェック")]
    [SerializeField] private bool useManualPositions = false;
    [SerializeField] private Vector2 manualOpenPos;
    [SerializeField] private Vector2 manualClosedPos;

    [Header("自動計算用スライド距離 (手動オフの時)")]
    [Tooltip("0なら子オブジェクトの実サイズから自動計算する。合わない時だけ手入力する。")]
    [SerializeField] private float slideDistance = 0f;
    [SerializeField] private float margin = 40f;

    [Header("閉じている間のクリック遮断")]
    [Tooltip("矢印ボタンをこのパネルの子に置いている場合は必ず OFF にする（ONだと二度と開けなくなる）")]
    [SerializeField] private bool blockRaycastsWhenClosed = false;

    private RectTransform rt;
    private CanvasGroup cg;
    private Vector2 openPos;
    private Vector2 closedPos;
    private MotionHandle handle;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        rt = (RectTransform)transform;
        cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

        if (useManualPositions)
        {
            openPos = manualOpenPos;
            closedPos = manualClosedPos;
        }
        else
        {
            openPos   = rt.anchoredPosition;                              // 今の位置＝開いた位置
            closedPos = openPos + new Vector2(ComputeSlideDistance(), 0f);
        }

        IsOpen = openOnStart;
        rt.anchoredPosition = IsOpen ? openPos : closedPos;
        SetInteractable(IsOpen);
    }

    /// <summary>
    /// 親の RectTransform が中身より小さくても正しく画面外へ逃がせるよう、
    /// 子オブジェクトの実際の広がりからスライド距離を求める。
    /// </summary>
    private float ComputeSlideDistance()
    {
        if (slideDistance > 0f) return slideDistance + margin;

        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(rt);
        float width = Mathf.Max(rt.rect.width, bounds.size.x);
        return width + margin;
    }

    public void Toggle() => SetOpen(!IsOpen);
    public void Open()   => SetOpen(true);
    public void Close()  => SetOpen(false);

    public void SetOpen(bool open)
    {
        IsOpen = open;
        Vector2 target = open ? openPos : closedPos;

        if (handle.IsActive()) handle.Cancel();   // 途中反転はこれだけで済む

        if (!isActiveAndEnabled)
        {
            rt.anchoredPosition = target;
            SetInteractable(open);
            return;
        }

        if (open) SetInteractable(true);

        // 残り距離に応じて所要時間を按分する（途中で反転しても速度が一定になる）
        float fullDist = Vector2.Distance(openPos, closedPos);
        float dur = Mathf.Max(0.01f,
            duration * (Vector2.Distance(rt.anchoredPosition, target) / Mathf.Max(1f, fullDist)));

        handle = LMotion.Create(rt.anchoredPosition, target, dur)
            .WithEase(ease)
            .WithScheduler(ignoreTimeScale
                ? MotionScheduler.UpdateIgnoreTimeScale
                : MotionScheduler.Update)
            .WithOnComplete(() => { if (!open) SetInteractable(false); })
            .BindToAnchoredPosition(rt)
            .AddTo(gameObject);
    }

    private void SetInteractable(bool on)
    {
        // 矢印ボタンが子にある構成では遮断してはいけない
        if (!blockRaycastsWhenClosed)
        {
            cg.interactable = true;
            cg.blocksRaycasts = true;
            return;
        }
        cg.interactable = on;
        cg.blocksRaycasts = on;
    }
}