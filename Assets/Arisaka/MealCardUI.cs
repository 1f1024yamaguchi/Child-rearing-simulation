using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Arisaka
{
    /// <summary>
    /// 食事選択ダイアログの1枚のカードを制御するコンポーネント。
    /// 器から湯気が出ているアイコン、タイトル（安価・普通・高価）、費用、効果、選択ボタンを表示する。
    /// 初回は枠線なし。2回目以降は先月に選択したものに薄い青枠と「先月に選択」ラベルを同色で表示。
    /// ボタンは常に「これにする」で統一。
    /// </summary>
    public class MealCardUI : MonoBehaviour
    {
        [Header("UI 参照")]
        [SerializeField] private Image cardBorderImage;          // 選択中の薄い青い枠線
        [SerializeField] private Image cardBackgroundImage;       // カード本体の白背景
        [SerializeField] private TextMeshProUGUI prevChoiceText;  // 「先月に選択」ラベル
        [SerializeField] private Image iconBackgroundImage;       // アイコン枠の背景
        [SerializeField] private Image bowlIconImage;             // 器と湯気の線画アイコン
        [SerializeField] private TextMeshProUGUI titleText;       // プラン名（安価/普通/高価）
        [SerializeField] private TextMeshProUGUI costText;        // 費用（30,000/月）
        [SerializeField] private TextMeshProUGUI stressText;      // ストレス効果
        [SerializeField] private TextMeshProUGUI eventRateText;   // イベント率効果
        [SerializeField] private Button selectButton;             // 選択ボタン
        [SerializeField] private TextMeshProUGUI buttonLabel;     // ボタンのラベル（「これにする」で統一）
        [SerializeField] private Image buttonBackgroundImage;    // ボタンの背景

        private MealPlan currentPlan;
        private Action<MealPlan> onSelectedCallback;
        private bool isPreviousChoice = false;

        private static Sprite cachedBowlSprite;

        // 前回選択時の薄い青色
        private readonly Color lightBlueColor = new Color(0.38f, 0.70f, 0.98f, 1f);

        public MealPlan CurrentPlan => currentPlan;

        /// <summary>
        /// カードの表示をセットアップする。
        /// </summary>
        public void Setup(MealPlan plan, bool isPrevChoice, Action<MealPlan> onSelected)
        {
            currentPlan = plan;
            isPreviousChoice = isPrevChoice;
            onSelectedCallback = onSelected;

            EnsureUIElements();
            UpdateView();
        }

        private void UpdateView()
        {
            if (currentPlan == null) return;

            if (titleText != null) titleText.text = currentPlan.title;
            if (costText != null) costText.text = $"{currentPlan.costPerMonth:N0}/月";
            if (stressText != null)
            {
                stressText.text = currentPlan.stressText;
                stressText.color = currentPlan.stressTextColor.a > 0
                    ? currentPlan.stressTextColor
                    : GetStressColorFallback(currentPlan.stressText);
            }

            if (eventRateText != null)
            {
                eventRateText.text = currentPlan.eventRateText;
                eventRateText.color = currentPlan.eventRateTextColor.a > 0
                    ? currentPlan.eventRateTextColor
                    : GetEventRateColorFallback(currentPlan.eventRateText);
            }

            if (iconBackgroundImage != null) iconBackgroundImage.color = currentPlan.iconBgColor;

            // 前回選択したカードには薄い青枠を表示、それ以外は枠なし
            if (cardBorderImage != null)
            {
                cardBorderImage.color = isPreviousChoice ? lightBlueColor : Color.clear;
            }

            // 「先月に選択」ラベルの表示（同じ薄い青色で記載）
            if (prevChoiceText != null)
            {
                prevChoiceText.text = isPreviousChoice ? "先月に選択" : "";
                prevChoiceText.color = lightBlueColor;
            }

            // ボタンのラベルは「これにする」で統一
            if (buttonLabel != null)
            {
                buttonLabel.text = "これにする";
            }

            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(OnSelectClicked);
            }
        }

        private void OnSelectClicked()
        {
            if (currentPlan != null)
            {
                onSelectedCallback?.Invoke(currentPlan);
            }
        }

        private Color GetStressColorFallback(string text)
        {
            if (string.IsNullOrEmpty(text)) return MealPlan.NeutralDark;
            if (text.Contains("+") || text.Contains("＋")) return MealPlan.NegativeRed;
            if (text.Contains("-") || text.Contains("−")) return MealPlan.HealingGreen;
            return MealPlan.NeutralDark;
        }

        private Color GetEventRateColorFallback(string text)
        {
            if (string.IsNullOrEmpty(text)) return MealPlan.NeutralDark;
            if (text.Contains("Bad")) return MealPlan.NegativeRed;
            if (text.Contains("Good")) return MealPlan.HealingGreen;
            return MealPlan.NeutralDark;
        }

        /// <summary>
        /// UI要素がインスペクター等で未接続の場合に動的に構築・探索する
        /// </summary>
        public void EnsureUIElements()
        {
            if (titleText != null) return;

            var jpFont = Resources.Load<TMP_FontAsset>("JPFont") ?? TMP_Settings.defaultFontAsset;

            // 1. 薄い青枠線（外側）
            cardBorderImage = GetComponent<Image>();
            if (cardBorderImage == null)
            {
                cardBorderImage = gameObject.AddComponent<Image>();
            }

            // 2. カード本体（白背景）
            var innerGO = new GameObject("CardInner", typeof(RectTransform), typeof(Image));
            innerGO.transform.SetParent(transform, false);
            var innerRT = innerGO.GetComponent<RectTransform>();
            SetStretchFull(innerRT, 3f, 3f); // 3pxのパディングで青枠線を見せる
            cardBackgroundImage = innerGO.GetComponent<Image>();
            cardBackgroundImage.color = Color.white;

            // 3. 「先月に選択」ラベル（カード最上部）
            var prevGO = new GameObject("PrevChoiceLabel", typeof(RectTransform));
            prevGO.transform.SetParent(innerRT, false);
            prevChoiceText = prevGO.AddComponent<TextMeshProUGUI>();
            if (jpFont != null) prevChoiceText.font = jpFont;
            prevChoiceText.fontSize = 12.5f;
            prevChoiceText.fontStyle = FontStyles.Bold;
            prevChoiceText.alignment = TextAlignmentOptions.Center;
            prevChoiceText.color = lightBlueColor;
            var prevRT = prevGO.GetComponent<RectTransform>();
            SetRect(prevRT, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(160f, 18f));

            // 4. アイコン枠背景（角丸風正方形）
            var iconBgGO = new GameObject("IconBg", typeof(RectTransform), typeof(Image));
            iconBgGO.transform.SetParent(innerRT, false);
            var iconBgRT = iconBgGO.GetComponent<RectTransform>();
            SetRect(iconBgRT, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(68f, 68f));
            iconBackgroundImage = iconBgGO.GetComponent<Image>();
            iconBackgroundImage.color = currentPlan != null ? currentPlan.iconBgColor : Color.gray;

            // 5. 器から湯気が出ている線画アイコン
            var iconGO = new GameObject("BowlIcon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(iconBgRT, false);
            var iconRT = iconGO.GetComponent<RectTransform>();
            SetStretchFull(iconRT, 3f, 3f);
            bowlIconImage = iconGO.GetComponent<Image>();
            bowlIconImage.sprite = GetOrCreateBowlSprite();
            bowlIconImage.preserveAspect = true;

            // 6. タイトル（安価/普通/高価）
            var titleGO = new GameObject("TitleText", typeof(RectTransform));
            titleGO.transform.SetParent(innerRT, false);
            titleText = titleGO.AddComponent<TextMeshProUGUI>();
            if (jpFont != null) titleText.font = jpFont;
            titleText.fontSize = 22f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            var titleRT = titleGO.GetComponent<RectTransform>();
            SetRect(titleRT, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(180f, 28f));

            // 7. 費用（30,000/月）
            var costGO = new GameObject("CostText", typeof(RectTransform));
            costGO.transform.SetParent(innerRT, false);
            costText = costGO.AddComponent<TextMeshProUGUI>();
            if (jpFont != null) costText.font = jpFont;
            costText.fontSize = 20f;
            costText.fontStyle = FontStyles.Bold;
            costText.alignment = TextAlignmentOptions.Center;
            costText.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            var costRT = costGO.GetComponent<RectTransform>();
            SetRect(costRT, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(180f, 28f));

            // 8. 区切り線（黒線）
            var lineGO = new GameObject("SeparatorLine", typeof(RectTransform), typeof(Image));
            lineGO.transform.SetParent(innerRT, false);
            var lineImg = lineGO.GetComponent<Image>();
            lineImg.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            var lineRT = lineGO.GetComponent<RectTransform>();
            SetRect(lineRT, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -156f), new Vector2(150f, 2f));

            // 9. ストレス効果
            var stressGO = new GameObject("StressText", typeof(RectTransform));
            stressGO.transform.SetParent(innerRT, false);
            stressText = stressGO.AddComponent<TextMeshProUGUI>();
            if (jpFont != null) stressText.font = jpFont;
            stressText.fontSize = 15f;
            stressText.fontStyle = FontStyles.Bold;
            stressText.alignment = TextAlignmentOptions.Center;
            stressText.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            var stressRT = stressGO.GetComponent<RectTransform>();
            SetRect(stressRT, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -182f), new Vector2(180f, 24f));

            // 10. イベント率効果
            var rateGO = new GameObject("EventRateText", typeof(RectTransform));
            rateGO.transform.SetParent(innerRT, false);
            eventRateText = rateGO.AddComponent<TextMeshProUGUI>();
            if (jpFont != null) eventRateText.font = jpFont;
            eventRateText.fontSize = 13.5f;
            eventRateText.fontStyle = FontStyles.Bold;
            eventRateText.alignment = TextAlignmentOptions.Center;
            eventRateText.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            var rateRT = rateGO.GetComponent<RectTransform>();
            SetRect(rateRT, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -210f), new Vector2(190f, 24f));

            // 11. ボタン（黒枠・クリーム色背景、「これにする」固定）
            var btnBorderGO = new GameObject("SelectButtonBorder", typeof(RectTransform), typeof(Image));
            btnBorderGO.transform.SetParent(innerRT, false);
            var btnBorderImg = btnBorderGO.GetComponent<Image>();
            btnBorderImg.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            var btnBorderRT = btnBorderGO.GetComponent<RectTransform>();
            SetRect(btnBorderRT, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(130f, 40f));

            var btnGO = new GameObject("SelectButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(btnBorderRT, false);
            var btnRT = btnGO.GetComponent<RectTransform>();
            SetStretchFull(btnRT, 1.5f, 1.5f);
            buttonBackgroundImage = btnGO.GetComponent<Image>();
            buttonBackgroundImage.color = new Color(1.0f, 0.95f, 0.86f, 1f);
            selectButton = btnGO.GetComponent<Button>();
            selectButton.targetGraphic = buttonBackgroundImage;

            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(btnGO.transform, false);
            buttonLabel = labelGO.AddComponent<TextMeshProUGUI>();
            if (jpFont != null) buttonLabel.font = jpFont;
            buttonLabel.fontSize = 18f;
            buttonLabel.fontStyle = FontStyles.Bold;
            buttonLabel.alignment = TextAlignmentOptions.Center;
            buttonLabel.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            buttonLabel.text = "これにする";
            SetStretchFull(buttonLabel.rectTransform, 0f, 0f);
        }

        /// <summary>
        /// 添付写真の「器から3本の湯気が出ている」線画スプライトを生成して返す
        /// </summary>
        public static Sprite GetOrCreateBowlSprite()
        {
            if (cachedBowlSprite != null) return cachedBowlSprite;

            int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            var lineCol = new Color(0.12f, 0.12f, 0.12f, 1f);

            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            int cx = size / 2; // 64

            void DrawDot(int px, int py, float radius = 1.5f)
            {
                int r = Mathf.CeilToInt(radius);
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int x = px + dx;
                        int y = py + dy;
                        if (x >= 0 && x < size && y >= 0 && y < size)
                        {
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            if (d <= radius)
                            {
                                float alpha = Mathf.Clamp01(1f - (d - (radius - 1f)));
                                Color current = pixels[y * size + x];
                                float newAlpha = Mathf.Max(current.a, alpha);
                                pixels[y * size + x] = new Color(lineCol.r, lineCol.g, lineCol.b, newAlpha);
                            }
                        }
                    }
                }
            }

            // 1. 湯気（3本のS字カーブ縦波線）: y = 72 〜 116
            int[] steamBaseX = { cx - 18, cx, cx + 18 };
            for (int y = 72; y <= 116; y++)
            {
                float t = (y - 72f) / 44f;
                float offset = Mathf.Sin(t * Mathf.PI * 2.2f) * 4.2f;
                foreach (int bx in steamBaseX)
                {
                    DrawDot(Mathf.RoundToInt(bx + offset), y, 1.6f);
                }
            }

            // 2. 器本体のボウル（下半分の半楕円）: y = 26 〜 66
            float bowlRx = 42f;
            float bowlRy = 38f;
            int bowlCenterY = 66;

            for (int y = 26; y <= bowlCenterY; y++)
            {
                float dy = (y - bowlCenterY) / bowlRy;
                float dxSq = 1f - dy * dy;
                if (dxSq >= 0f)
                {
                    float dx = Mathf.Sqrt(dxSq) * bowlRx;
                    DrawDot(Mathf.RoundToInt(cx - dx), y, 1.8f);
                    DrawDot(Mathf.RoundToInt(cx + dx), y, 1.8f);
                }
            }

            // 3. 器上部の開口部（横長の楕円リム）: y = 60 〜 72
            float rimRx = 42f;
            float rimRy = 7.5f;
            for (int angle = 0; angle < 360; angle += 2)
            {
                float rad = angle * Mathf.Deg2Rad;
                int x = Mathf.RoundToInt(cx + Mathf.Cos(rad) * rimRx);
                int y = Mathf.RoundToInt(bowlCenterY + Mathf.Sin(rad) * rimRy);
                DrawDot(x, y, 1.7f);
            }

            // 4. スープ液面（器内の水平線）: y = 52
            int soupY = 52;
            float soupDy = (soupY - bowlCenterY) / bowlRy;
            float soupDx = Mathf.Sqrt(Mathf.Max(0f, 1f - soupDy * soupDy)) * bowlRx;
            for (int x = Mathf.RoundToInt(cx - soupDx + 3f); x <= Mathf.RoundToInt(cx + soupDx - 3f); x++)
            {
                DrawDot(x, soupY, 1.3f);
            }

            // 5. 底の高台（台座）: y = 18 〜 26
            int standY = 18;
            for (int x = cx - 22; x <= cx + 22; x++)
            {
                DrawDot(x, standY, 1.6f);
            }
            DrawDot(cx - 15, 22, 1.4f);
            DrawDot(cx + 15, 22, 1.4f);

            tex.SetPixels(pixels);
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();

            cachedBowlSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return cachedBowlSprite;
        }

        private void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }

        private void SetStretchFull(RectTransform rt, float padX, float padY)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }
    }
}
