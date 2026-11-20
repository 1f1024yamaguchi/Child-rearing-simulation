using UnityEngine;
using UnityEngine.UI;

namespace Kusao.TutorialGuide
{
    /// <summary>
    /// 指定された UI 要素（RectTransform）の周囲以外を暗くし、
    /// スポットライトのようにくり抜く演出とクリック遮断を行うコンポーネント。
    /// 4枚の Image（Top, Bottom, Left, Right）により、くり抜かれた領域以外のタップを遮断します。
    /// </summary>
    public class TutorialGuideSpotlight : MonoBehaviour
    {
        [Header("暗幕カラー")]
        [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.72f);

        [Header("枠線カラー（ハイライト）")]
        [SerializeField] private Color borderColor = new Color(0.40f, 0.78f, 0.90f, 0.75f);
        [SerializeField] private float borderWidth = 2f;
        [SerializeField] private bool pulseBorder = true;

        [Header("参照（未アサイン時は自動生成）")]
        [SerializeField] private RectTransform topPanel;
        [SerializeField] private RectTransform bottomPanel;
        [SerializeField] private RectTransform leftPanel;
        [SerializeField] private RectTransform rightPanel;
        [SerializeField] private RectTransform focusBorder;

        private RectTransform selfRect;
        private Canvas rootCanvas;
        private Image borderImage;
        private float pulseTimer;
        private bool hasTarget;

        private void Awake()
        {
            selfRect = GetComponent<RectTransform>();
            rootCanvas = GetComponentInParent<Canvas>();
            EnsurePanels();
        }

        private void Update()
        {
            if (pulseBorder && hasTarget && borderImage != null)
            {
                pulseTimer += Time.unscaledDeltaTime * 3.5f;
                float pulse = (Mathf.Sin(pulseTimer) + 1f) * 0.5f;
                float alpha = Mathf.Lerp(0.05f, borderColor.a, pulse);
                Color c = borderColor;
                c.a = alpha;
                borderImage.color = c;
            }
        }

        /// <summary>
        /// 暗幕パネルと枠線が未構築の場合は自動構築する
        /// </summary>
        public void EnsurePanels()
        {
            if (selfRect == null) selfRect = GetComponent<RectTransform>();
            if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();

            // 全画面ストレッチにする
            selfRect.anchorMin = Vector2.zero;
            selfRect.anchorMax = Vector2.one;
            selfRect.offsetMin = Vector2.zero;
            selfRect.offsetMax = Vector2.zero;

            topPanel = GetOrCreatePanel("Dim_Top", topPanel);
            bottomPanel = GetOrCreatePanel("Dim_Bottom", bottomPanel);
            leftPanel = GetOrCreatePanel("Dim_Left", leftPanel);
            rightPanel = GetOrCreatePanel("Dim_Right", rightPanel);

            if (focusBorder == null)
            {
                var borderT = transform.Find("Focus_Border");
                if (borderT != null)
                {
                    focusBorder = borderT as RectTransform;
                }
                else
                {
                    var go = new GameObject("Focus_Border", typeof(RectTransform), typeof(Image));
                    go.transform.SetParent(transform, false);
                    focusBorder = go.GetComponent<RectTransform>();
                    borderImage = go.GetComponent<Image>();
                    borderImage.color = borderColor;
                    borderImage.raycastTarget = false;
                }
            }
            if (borderImage == null && focusBorder != null)
            {
                borderImage = focusBorder.GetComponent<Image>();
            }

            HidePanels();
        }

        private RectTransform GetOrCreatePanel(string name, RectTransform existing)
        {
            if (existing != null) return existing;
            var t = transform.Find(name);
            if (t != null) return t as RectTransform;

            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var img = go.GetComponent<Image>();
            img.color = dimColor;
            img.raycastTarget = true; // クリックを確実に遮断する
            return go.GetComponent<RectTransform>();
        }

        public Rect CurrentCutoutRect { get; private set; }

        /// <summary>
        /// ハイライト対象を設定し、その周囲をくり抜いて暗幕と枠線を配置する
        /// </summary>
        /// <param name="target">くり抜く対象のRectTransform（nullなら全画面暗幕）</param>
        /// <param name="padding">余白サイズ（幅・高さ）</param>
        /// <param name="offset">位置の手動微調整オフセット（X, Y）</param>
        public void SetTarget(RectTransform target, Vector2 padding, Vector2 offset = default)
        {
            EnsurePanels();
            gameObject.SetActive(true);

            if (target == null || !target.gameObject.activeInHierarchy)
            {
                // 対象がない場合は全画面を暗幕で覆う
                hasTarget = false;
                CurrentCutoutRect = Rect.zero;
                if (focusBorder != null) focusBorder.gameObject.SetActive(false);

                SetRect(topPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                SetRect(bottomPanel, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                SetRect(leftPanel, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                SetRect(rightPanel, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                return;
            }

            hasTarget = true;
            if (focusBorder != null) focusBorder.gameObject.SetActive(true);

            // 対象の Canvas ローカル座標を計算
            Rect cutoutRect = GetCutoutRectInSelfSpace(target, padding, offset);
            CurrentCutoutRect = cutoutRect;
            ApplyCutout(cutoutRect);
        }

        /// <summary>
        /// 暗幕を非表示にする
        /// </summary>
        public void Hide()
        {
            HidePanels();
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void HidePanels()
        {
            hasTarget = false;
            CurrentCutoutRect = Rect.zero;
            if (focusBorder != null) focusBorder.gameObject.SetActive(false);
            if (topPanel != null) topPanel.gameObject.SetActive(false);
            if (bottomPanel != null) bottomPanel.gameObject.SetActive(false);
            if (leftPanel != null) leftPanel.gameObject.SetActive(false);
            if (rightPanel != null) rightPanel.gameObject.SetActive(false);
        }

        private Rect GetCutoutRectInSelfSpace(RectTransform target, Vector2 padding, Vector2 offset)
        {
            Vector3[] worldCorners = new Vector3[4];
            target.GetWorldCorners(worldCorners);

            // ターゲット側の Canvas とカメラ
            Canvas targetCanvas = target.GetComponentInParent<Canvas>();
            Camera targetCam = (targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? targetCanvas.worldCamera : null;

            // スポットライト側の Canvas とカメラ
            Camera selfCam = (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? rootCanvas.worldCamera : null;

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);

            for (int i = 0; i < 4; i++)
            {
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(targetCam, worldCorners[i]);
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    selfRect,
                    screenPoint,
                    selfCam,
                    out localPoint
                );
                min = Vector2.Min(min, localPoint);
                max = Vector2.Max(max, localPoint);
            }

            // 余白の適用（片側ずつ padding / 2 を拡張）
            min -= padding * 0.5f;
            max += padding * 0.5f;

            // 手動オフセットの適用
            min += offset;
            max += offset;

            return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        }

        private void ApplyCutout(Rect rect)
        {
            Rect selfBounds = selfRect.rect;
            float leftEdge = selfBounds.xMin;
            float rightEdge = selfBounds.xMax;
            float bottomEdge = selfBounds.yMin;
            float topEdge = selfBounds.yMax;

            float xMin = Mathf.Clamp(rect.xMin, leftEdge, rightEdge);
            float xMax = Mathf.Clamp(rect.xMax, leftEdge, rightEdge);
            float yMin = Mathf.Clamp(rect.yMin, bottomEdge, topEdge);
            float yMax = Mathf.Clamp(rect.yMax, bottomEdge, topEdge);

            // Top: 画面上端 〜 くり抜き上端
            SetPanelBounds(topPanel, leftEdge, rightEdge, yMax, topEdge);

            // Bottom: 画面下端 〜 くり抜き下端
            SetPanelBounds(bottomPanel, leftEdge, rightEdge, bottomEdge, yMin);

            // Left: くり抜きの左側（yMin 〜 yMax）
            SetPanelBounds(leftPanel, leftEdge, xMin, yMin, yMax);

            // Right: くり抜きの右側（yMin 〜 yMax）
            SetPanelBounds(rightPanel, xMax, rightEdge, yMin, yMax);

            // Focus Border: くり抜き枠線
            if (focusBorder != null)
            {
                focusBorder.anchorMin = new Vector2(0.5f, 0.5f);
                focusBorder.anchorMax = new Vector2(0.5f, 0.5f);
                focusBorder.pivot = new Vector2(0.5f, 0.5f);
                focusBorder.sizeDelta = new Vector2(xMax - xMin, yMax - yMin);
                focusBorder.anchoredPosition = new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f);
            }
        }

        private void SetPanelBounds(RectTransform rt, float xMin, float xMax, float yMin, float yMax)
        {
            if (rt == null) return;
            float w = Mathf.Max(0f, xMax - xMin);
            float h = Mathf.Max(0f, yMax - yMin);

            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f);
            rt.gameObject.SetActive(w > 0.01f && h > 0.01f);
        }

        private void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            if (rt == null) return;
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = oMin;
            rt.offsetMax = oMax;
            rt.gameObject.SetActive(true);
        }
    }
}
