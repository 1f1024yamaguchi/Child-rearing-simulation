using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// グラフがスクロールで画面に入ったら、線を左から右へ「びーっ」と描いていく演出。
/// 線は RectMask2D の付いた入れ物（lines）の中に置き、その入れ物の幅を 0 → 全幅 に広げて見せる。
/// 目盛り・軸の文字は入れ物の外に置くので、最初から見えている。
/// 成長段階の絵など（popItems）は、線がその位置まで来たら飛び出すように出す。
/// 時間が止まっている画面でも動くよう、unscaledDeltaTime で進める。
public class GraphReveal : MonoBehaviour
{
    [Tooltip("線をまとめた入れ物（RectMask2D 付き・左下基準）。この幅を広げて線を見せる")]
    [SerializeField] private RectTransform lines;

    [Tooltip("描き終わったときの入れ物の幅。0 なら自分（グラフの枠）の幅")]
    [SerializeField] private float fullWidth = 0f;

    [Tooltip("左端から右端まで描くのにかける秒数")]
    [SerializeField] private float duration = 1.4f;

    [Tooltip("グラフの枠がこの割合以上見えたら描き始める（0〜1）")]
    [Range(0f, 1f)] [SerializeField] private float visibleRatioToStart = 0.6f;

    [Tooltip("線がその位置（x）まで来たら出す絵など。グラフの枠の左下からの位置で判定する")]
    [SerializeField] private List<RectTransform> popItems = new List<RectTransform>();

    [Tooltip("絵が飛び出すときの秒数")]
    [SerializeField] private float popDuration = 0.25f;

    [Tooltip("見えている範囲の判定に使う枠。空なら親の ScrollRect の viewport")]
    [SerializeField] private RectTransform viewport;

    private RectTransform self;
    private bool playing;
    private bool done;
    private float elapsed;
    private readonly List<float> popStarted = new List<float>();
    private static readonly Vector3[] corners = new Vector3[4];

    private void Awake()
    {
        self = (RectTransform)transform;
        if (viewport == null)
        {
            var sr = GetComponentInParent<ScrollRect>();
            if (sr != null) viewport = sr.viewport != null ? sr.viewport : (RectTransform)sr.transform;
        }
    }

    private void Start()
    {
        // シーンには描き終わった状態で保存してあるので、始まったら一度空にする
        SetProgress(0f);
        popStarted.Clear();
        foreach (var item in popItems)
        {
            popStarted.Add(-1f);
            if (item != null) item.localScale = Vector3.zero;
        }
    }

    private void Update()
    {
        if (done) return;

        if (!playing)
        {
            if (VisibleRatio() < visibleRatioToStart) return;
            playing = true;
            elapsed = 0f;
        }

        elapsed += Time.unscaledDeltaTime;
        float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
        float eased = 1f - Mathf.Pow(1f - t, 3f);   // 最初は速く、最後はゆっくり止まる
        SetProgress(eased);
        UpdatePops();

        if (t >= 1f && AllPopsFinished()) done = true;
    }

    /// 描いた割合（0〜1）に合わせて入れ物の幅を変える
    private void SetProgress(float p)
    {
        if (lines == null) return;
        var size = lines.sizeDelta;
        size.x = Width() * p;
        lines.sizeDelta = size;
    }

    private float Width()
    {
        if (fullWidth > 0f) return fullWidth;
        return self != null ? self.rect.width : 0f;
    }

    private void UpdatePops()
    {
        float edge = lines != null ? lines.sizeDelta.x : Width();
        for (int i = 0; i < popItems.Count; i++)
        {
            var item = popItems[i];
            if (item == null) continue;

            if (popStarted[i] < 0f)
            {
                if (item.anchoredPosition.x > edge) continue;
                popStarted[i] = elapsed;
            }

            float k = popDuration > 0f ? Mathf.Clamp01((elapsed - popStarted[i]) / popDuration) : 1f;
            // 少し大きくなってから戻る（ぽんっと出る）
            float s = k < 0.7f ? Mathf.Lerp(0f, 1.15f, k / 0.7f) : Mathf.Lerp(1.15f, 1f, (k - 0.7f) / 0.3f);
            item.localScale = new Vector3(s, s, 1f);
        }
    }

    private bool AllPopsFinished()
    {
        for (int i = 0; i < popItems.Count; i++)
        {
            if (popItems[i] == null) continue;
            if (popStarted[i] < 0f || elapsed - popStarted[i] < popDuration) return false;
        }
        return true;
    }

    /// グラフの枠のうち、見えている範囲（viewport）に入っている縦の割合
    private float VisibleRatio()
    {
        if (viewport == null) return 1f;
        self.GetWorldCorners(corners);
        float top = corners[1].y, bottom = corners[0].y;
        viewport.GetWorldCorners(corners);
        float vTop = corners[1].y, vBottom = corners[0].y;
        float h = top - bottom;
        if (h <= 0f) return 0f;
        float overlap = Mathf.Min(top, vTop) - Mathf.Max(bottom, vBottom);
        return Mathf.Clamp01(overlap / h);
    }

    /// 確認用：もう一度最初から描く
    [ContextMenu("もう一度描く")]
    public void Replay()
    {
        done = false;
        playing = false;
        Start();
    }
}
