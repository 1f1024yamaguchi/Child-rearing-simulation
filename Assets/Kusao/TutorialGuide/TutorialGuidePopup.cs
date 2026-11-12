using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Kusao.TutorialGuide
{
    /// <summary>
    /// チュートリアルの説明文・タイトル・進行ボタン・ポインタを表示するポップアップUI。
    /// ハイライトされた要素の位置に合わせて自動で上下の配置を調整します。
    /// </summary>
    public class TutorialGuidePopup : MonoBehaviour
    {
        [Header("UI 参照")]
        [SerializeField] private RectTransform popupPanel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private Button nextButton;
        [SerializeField] private TextMeshProUGUI nextButtonLabel;
        [SerializeField] private Button skipButton;
        [SerializeField] private RectTransform arrowTop;     // 下向き矢印（ポップアップが上にあるとき）
        [SerializeField] private RectTransform arrowBottom;  // 上向き矢印（ポップアップが下にあるとき）

        [Header("レイアウト設定")]
        [SerializeField] private Vector2 defaultSize = new Vector2(560f, 260f);
        [SerializeField] private float targetMargin = 28f;
        [SerializeField] private float screenPadding = 24f;

        [Header("配色設定")]
        [SerializeField] private Color panelBgColor = new Color(1f, 0.98f, 0.94f, 0.98f);
        [SerializeField] private Color nextBtnNormalColor = new Color(0.16f, 0.83f, 0.96f, 1f);
        [SerializeField] private Color nextBtnWaitColor = new Color(0.85f, 0.85f, 0.85f, 1f);

        public event Action OnNextClicked;
        public event Action OnSkipClicked;

        private RectTransform selfRect;
        private Canvas rootCanvas;

        private void Awake()
        {
            selfRect = GetComponent<RectTransform>();
            rootCanvas = GetComponentInParent<Canvas>();
            EnsureUIBuilt();

            if (nextButton != null)
            {
                nextButton.onClick.AddListener(() => OnNextClicked?.Invoke());
            }
            if (skipButton != null)
            {
                skipButton.onClick.AddListener(() => OnSkipClicked?.Invoke());
            }
        }

        /// <summary>
        /// UI要素が未アサインの場合は動的に生成・構築する
        /// </summary>
        public void EnsureUIBuilt()
        {
            if (selfRect == null) selfRect = GetComponent<RectTransform>();
            if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();

            // 全画面ストレッチ
            selfRect.anchorMin = Vector2.zero;
            selfRect.anchorMax = Vector2.one;
            selfRect.offsetMin = Vector2.zero;
            selfRect.offsetMax = Vector2.zero;

            if (popupPanel != null) return;

            var font = Resources.Load<TMP_FontAsset>("JPFont") ?? TMP_Settings.defaultFontAsset;

            // Panel
            var panelGO = new GameObject("PopupPanel", typeof(RectTransform), typeof(Image));
            panelGO.transform.SetParent(transform, false);
            popupPanel = panelGO.GetComponent<RectTransform>();
            popupPanel.sizeDelta = defaultSize;
            var panelImg = panelGO.GetComponent<Image>();
            panelImg.color = panelBgColor;

            // Title
            var titleGO = new GameObject("Title", typeof(RectTransform));
            titleGO.transform.SetParent(popupPanel, false);
            titleText = titleGO.AddComponent<TextMeshProUGUI>();
            if (font != null) titleText.font = font;
            titleText.fontSize = 26;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = new Color32(35, 35, 35, 255);
            titleText.alignment = TextAlignmentOptions.Left;
            titleText.overflowMode = TextOverflowModes.Overflow;
            var tRt = titleGO.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0f, 1f);
            tRt.anchorMax = new Vector2(1f, 1f);
            tRt.pivot = new Vector2(0f, 1f);
            tRt.anchoredPosition = new Vector2(28f, -20f);
            tRt.sizeDelta = new Vector2(-150f, 40f);

            // Progress
            var progGO = new GameObject("Progress", typeof(RectTransform));
            progGO.transform.SetParent(popupPanel, false);
            progressText = progGO.AddComponent<TextMeshProUGUI>();
            if (font != null) progressText.font = font;
            progressText.fontSize = 18;
            progressText.color = new Color32(110, 110, 110, 255);
            progressText.alignment = TextAlignmentOptions.Right;
            progressText.overflowMode = TextOverflowModes.Overflow;
            var pRt = progGO.GetComponent<RectTransform>();
            pRt.anchorMin = new Vector2(1f, 1f);
            pRt.anchorMax = new Vector2(1f, 1f);
            pRt.pivot = new Vector2(1f, 1f);
            pRt.anchoredPosition = new Vector2(-28f, -22f);
            pRt.sizeDelta = new Vector2(100f, 36f);

            // Body
            var bodyGO = new GameObject("Body", typeof(RectTransform));
            bodyGO.transform.SetParent(popupPanel, false);
            bodyText = bodyGO.AddComponent<TextMeshProUGUI>();
            if (font != null) bodyText.font = font;
            bodyText.fontSize = 20;
            bodyText.color = new Color32(35, 35, 35, 255);
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.enableWordWrapping = true;
            bodyText.overflowMode = TextOverflowModes.Overflow;
            var bRt = bodyGO.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0f, 0f);
            bRt.anchorMax = new Vector2(1f, 1f);
            bRt.pivot = new Vector2(0.5f, 0.5f);
            bRt.anchoredPosition = new Vector2(0f, 5f);
            bRt.sizeDelta = new Vector2(-56f, -125f);

            // Hint
            var hintGO = new GameObject("Hint", typeof(RectTransform));
            hintGO.transform.SetParent(popupPanel, false);
            hintText = hintGO.AddComponent<TextMeshProUGUI>();
            if (font != null) hintText.font = font;
            hintText.fontSize = 18;
            hintText.fontStyle = FontStyles.Bold;
            hintText.color = nextBtnNormalColor;
            hintText.alignment = TextAlignmentOptions.Center;
            hintText.overflowMode = TextOverflowModes.Overflow;
            var hRt = hintGO.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0f, 0f);
            hRt.anchorMax = new Vector2(1f, 0f);
            hRt.pivot = new Vector2(0.5f, 0f);
            hRt.anchoredPosition = new Vector2(0f, 68f);
            hRt.sizeDelta = new Vector2(-56f, 32f);

            // Buttons Area
            var btnArea = new GameObject("ButtonsArea", typeof(RectTransform));
            btnArea.transform.SetParent(popupPanel, false);
            var baRt = btnArea.GetComponent<RectTransform>();
            baRt.anchorMin = new Vector2(0f, 0f);
            baRt.anchorMax = new Vector2(1f, 0f);
            baRt.pivot = new Vector2(0.5f, 0f);
            baRt.anchoredPosition = new Vector2(0f, 16f);
            baRt.sizeDelta = new Vector2(-56f, 46f);

            // Next Button
            var nextGO = new GameObject("NextButton", typeof(RectTransform), typeof(Image), typeof(Button));
            nextGO.transform.SetParent(baRt, false);
            nextButton = nextGO.GetComponent<Button>();
            var nbImg = nextGO.GetComponent<Image>();
            nbImg.color = nextBtnNormalColor;
            nextButton.targetGraphic = nbImg;
            var nbRt = nextGO.GetComponent<RectTransform>();
            nbRt.anchorMin = new Vector2(1f, 0.5f);
            nbRt.anchorMax = new Vector2(1f, 0.5f);
            nbRt.pivot = new Vector2(1f, 0.5f);
            nbRt.anchoredPosition = Vector2.zero;
            nbRt.sizeDelta = new Vector2(180f, 44f);

            var nbTextGO = new GameObject("Label", typeof(RectTransform));
            nbTextGO.transform.SetParent(nbRt, false);
            nextButtonLabel = nbTextGO.AddComponent<TextMeshProUGUI>();
            if (font != null) nextButtonLabel.font = font;
            nextButtonLabel.text = "次へ ＞";
            nextButtonLabel.fontSize = 20;
            nextButtonLabel.fontStyle = FontStyles.Bold;
            nextButtonLabel.color = Color.white;
            nextButtonLabel.alignment = TextAlignmentOptions.Center;
            nextButtonLabel.overflowMode = TextOverflowModes.Overflow;
            var nbtRt = nbTextGO.GetComponent<RectTransform>();
            nbtRt.anchorMin = Vector2.zero;
            nbtRt.anchorMax = Vector2.one;
            nbtRt.offsetMin = Vector2.zero;
            nbtRt.offsetMax = Vector2.zero;

            // Skip Button
            var skipGO = new GameObject("SkipButton", typeof(RectTransform), typeof(Image), typeof(Button));
            skipGO.transform.SetParent(baRt, false);
            skipButton = skipGO.GetComponent<Button>();
            var sbImg = skipGO.GetComponent<Image>();
            sbImg.color = new Color(0.88f, 0.88f, 0.88f, 1f);
            skipButton.targetGraphic = sbImg;
            var sbRt = skipGO.GetComponent<RectTransform>();
            sbRt.anchorMin = new Vector2(0f, 0.5f);
            sbRt.anchorMax = new Vector2(0f, 0.5f);
            sbRt.pivot = new Vector2(0f, 0.5f);
            sbRt.anchoredPosition = Vector2.zero;
            sbRt.sizeDelta = new Vector2(110f, 40f);

            var sbTextGO = new GameObject("Label", typeof(RectTransform));
            sbTextGO.transform.SetParent(sbRt, false);
            var sbText = sbTextGO.AddComponent<TextMeshProUGUI>();
            if (font != null) sbText.font = font;
            sbText.text = "スキップ";
            sbText.fontSize = 16;
            sbText.fontStyle = FontStyles.Bold;
            sbText.color = new Color32(35, 35, 35, 255);
            sbText.alignment = TextAlignmentOptions.Center;
            sbText.overflowMode = TextOverflowModes.Overflow;
            var sbtRt = sbTextGO.GetComponent<RectTransform>();
            sbtRt.anchorMin = Vector2.zero;
            sbtRt.anchorMax = Vector2.one;
            sbtRt.offsetMin = Vector2.zero;
            sbtRt.offsetMax = Vector2.zero;

            // Arrows
            var atGO = new GameObject("ArrowTop", typeof(RectTransform), typeof(Image));
            atGO.transform.SetParent(popupPanel, false);
            arrowTop = atGO.GetComponent<RectTransform>();
            arrowTop.sizeDelta = new Vector2(24f, 14f);
            arrowTop.anchorMin = new Vector2(0.5f, 0f);
            arrowTop.anchorMax = new Vector2(0.5f, 0f);
            arrowTop.pivot = new Vector2(0.5f, 1f);
            arrowTop.anchoredPosition = Vector2.zero;
            atGO.GetComponent<Image>().color = panelBgColor;

            var abGO = new GameObject("ArrowBottom", typeof(RectTransform), typeof(Image));
            abGO.transform.SetParent(popupPanel, false);
            arrowBottom = abGO.GetComponent<RectTransform>();
            arrowBottom.sizeDelta = new Vector2(24f, 14f);
            arrowBottom.anchorMin = new Vector2(0.5f, 1f);
            arrowBottom.anchorMax = new Vector2(0.5f, 1f);
            arrowBottom.pivot = new Vector2(0.5f, 0f);
            arrowBottom.anchoredPosition = Vector2.zero;
            abGO.GetComponent<Image>().color = panelBgColor;
        }

        /// <summary>
        /// ステップ情報と対象Rectに合わせてポップアップを表示・レイアウトする
        /// </summary>
        public void Show(
            string title,
            string body,
            string progress,
            bool isLastStep,
            bool isWaitingButtonClick,
            string buttonHint,
            RectTransform targetRect,
            Rect cutoutRect = default)
        {
            gameObject.SetActive(true);

            if (titleText != null) titleText.text = title;
            if (bodyText != null) bodyText.text = body;
            if (progressText != null) progressText.text = progress;

            // ヒント文の表示
            if (hintText != null)
            {
                if (isWaitingButtonClick && !string.IsNullOrEmpty(buttonHint))
                {
                    hintText.text = buttonHint;
                    hintText.gameObject.SetActive(true);
                }
                else
                {
                    hintText.gameObject.SetActive(false);
                }
            }

            // 次へボタンのラベルと色
            if (nextButtonLabel != null)
            {
                if (isLastStep)
                {
                    nextButtonLabel.text = "ゲームをはじめる！";
                }
                else if (isWaitingButtonClick)
                {
                    nextButtonLabel.text = "次へ（スキップ）";
                }
                else
                {
                    nextButtonLabel.text = "次へ ＞";
                }
            }

            if (nextButton != null && nextButton.image != null)
            {
                nextButton.image.color = isWaitingButtonClick ? nextBtnWaitColor : nextBtnNormalColor;
            }

            UpdateLayout(targetRect, cutoutRect);
        }

        /// <summary>
        /// ポップアップを非表示にする
        /// </summary>
        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void UpdateLayout(RectTransform targetRect, Rect cutoutRect)
        {
            if (popupPanel == null) return;
            if (selfRect == null) selfRect = GetComponent<RectTransform>();

            Vector2 popupSize = popupPanel.sizeDelta;
            if (popupSize.x <= 10f || popupSize.y <= 10f) popupSize = defaultSize;

            bool hasCutout = cutoutRect.width > 0.01f && cutoutRect.height > 0.01f;
            if (!hasCutout && (targetRect == null || !targetRect.gameObject.activeInHierarchy))
            {
                // 対象がない場合は画面中央に表示
                popupPanel.anchorMin = new Vector2(0.5f, 0.5f);
                popupPanel.anchorMax = new Vector2(0.5f, 0.5f);
                popupPanel.pivot = new Vector2(0.5f, 0.5f);
                popupPanel.anchoredPosition = Vector2.zero;

                if (arrowTop != null) arrowTop.gameObject.SetActive(false);
                if (arrowBottom != null) arrowBottom.gameObject.SetActive(false);
                return;
            }

            float targetMinY;
            float targetMaxY;
            float targetCenterX;
            float targetCenterY;

            if (hasCutout)
            {
                // スポットライトがくり抜いた矩形をそのまま基準にする（位置ズレなし）
                targetMinY = cutoutRect.yMin;
                targetMaxY = cutoutRect.yMax;
                targetCenterX = cutoutRect.center.x;
                targetCenterY = cutoutRect.center.y;
            }
            else
            {
                Canvas targetCanvas = targetRect.GetComponentInParent<Canvas>();
                Camera targetCam = (targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    ? targetCanvas.worldCamera : null;
                Camera selfCam = (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    ? rootCanvas.worldCamera : null;

                Vector3[] worldCorners = new Vector3[4];
                targetRect.GetWorldCorners(worldCorners);
                targetMinY = float.MaxValue;
                targetMaxY = float.MinValue;
                float minX = float.MaxValue;
                float maxX = float.MinValue;
                for (int i = 0; i < 4; i++)
                {
                    Vector2 lp;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        selfRect,
                        RectTransformUtility.WorldToScreenPoint(targetCam, worldCorners[i]),
                        selfCam,
                        out lp
                    );
                    targetMinY = Mathf.Min(targetMinY, lp.y);
                    targetMaxY = Mathf.Max(targetMaxY, lp.y);
                    minX = Mathf.Min(minX, lp.x);
                    maxX = Mathf.Max(maxX, lp.x);
                }
                targetCenterX = (minX + maxX) * 0.5f;
                targetCenterY = (targetMinY + targetMaxY) * 0.5f;
            }

            Rect selfBounds = selfRect.rect;
            bool placeAbove = targetCenterY <= 0f; // 画面下半分なら上側に配置、上半分なら下側に配置

            float popupY;
            if (placeAbove)
            {
                // 対象の上側に配置
                popupY = targetMaxY + targetMargin + (popupSize.y * 0.5f);
                if (arrowTop != null) arrowTop.gameObject.SetActive(true);
                if (arrowBottom != null) arrowBottom.gameObject.SetActive(false);
            }
            else
            {
                // 対象の下側に配置
                popupY = targetMinY - targetMargin - (popupSize.y * 0.5f);
                if (arrowTop != null) arrowTop.gameObject.SetActive(false);
                if (arrowBottom != null) arrowBottom.gameObject.SetActive(true);
            }

            // 画面上下のはみ出し防止
            float maxY = selfBounds.yMax - (popupSize.y * 0.5f) - screenPadding;
            float minY = selfBounds.yMin + (popupSize.y * 0.5f) + screenPadding;
            popupY = Mathf.Clamp(popupY, minY, maxY);

            // 画面左右のはみ出し防止
            float popupX = targetCenterX;
            float maxPX = selfBounds.xMax - (popupSize.x * 0.5f) - screenPadding;
            float minPX = selfBounds.xMin + (popupSize.x * 0.5f) + screenPadding;
            popupX = Mathf.Clamp(popupX, minPX, maxPX);

            popupPanel.anchorMin = new Vector2(0.5f, 0.5f);
            popupPanel.anchorMax = new Vector2(0.5f, 0.5f);
            popupPanel.pivot = new Vector2(0.5f, 0.5f);
            popupPanel.anchoredPosition = new Vector2(popupX, popupY);
        }
    }
}

