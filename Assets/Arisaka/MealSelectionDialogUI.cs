using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Maruyama.Core;

namespace Arisaka
{
    /// <summary>
    /// 毎月1日の食事選択ダイアログ UI。
    /// 最安値・普通・高級の3プランを提示し、プレイヤーが選択完了するまでゲーム内時間を停止する。
    /// 上部に現在の資産額を表示し、カレンダー画面に近いコンパクトなサイズ感（720x460）で描画する。
    /// （費用はその月の日数で日割りされ、一日ごとに引き落とされる）
    /// </summary>
    public class MealSelectionDialogUI : MonoBehaviour
    {
        [Header("UI 参照（未接続時は自動構築）")]
        [SerializeField] private RectTransform containerPanel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI moneyText;
        [SerializeField] private Transform cardsParent;
        [SerializeField] private List<MealCardUI> cardUIs = new List<MealCardUI>();

        [Header("サイズ・カラー設定")]
        [SerializeField] private Vector2 windowSize = new Vector2(720f, 460f); // 程よいサイズ感（オート操作はカード下の空きに収める。これ以上高くすると画面から見切れる）
        [SerializeField] private Color panelBg = new Color(1.0f, 0.96f, 0.88f, 1f); // 薄ベージュ・クリーム色
        [SerializeField] private Color darkText = new Color(0.12f, 0.12f, 0.12f, 1f);
        [SerializeField] private Color moneyBadgeBg = new Color(0.93f, 0.90f, 0.82f, 1f);

        [Header("オート選択")]
        [Tooltip("オート ON のとき、ダイアログを開いてから自動で決定するまでの猶予（秒）。時間停止中なので実時間で数える")]
        [SerializeField] private float autoDelaySeconds = 5f;
        [SerializeField] private Button autoButton;
        [SerializeField] private TextMeshProUGUI autoButtonLabel;
        [SerializeField] private RectTransform autoGaugeRoot;   // ゲージ全体（OFF のときは非表示）
        [SerializeField] private RectTransform autoGaugeFill;   // 残り時間ぶんだけ幅が残る
        [SerializeField] private TextMeshProUGUI autoGaugeLabel;

        [SerializeField] private Color autoOnColor = new Color(0.30f, 0.65f, 0.40f, 1f);
        [SerializeField] private Color autoOffColor = new Color(0.62f, 0.62f, 0.62f, 1f);
        [SerializeField] private Color gaugeBgColor = new Color(0.87f, 0.83f, 0.74f, 1f);
        [SerializeField] private Color gaugeFillColor = new Color(0.38f, 0.70f, 0.98f, 1f);

        /// <summary>プランが決定されたときのイベント</summary>
        public event Action<MealPlan> OnPlanSelected;

        private List<MealPlan> plans;
        private MealGrade? previousSelectedGrade = null; // 先月選択したグレード（初回は null）
        private bool isPausedBySelf = false;

        private float autoElapsed = 0f;     // オート猶予の経過秒（実時間）
        private bool autoLocal = false;     // MealSystemManager が無いとき用のフォールバック

        private bool AutoOn
        {
            get => MealSystemManager.Instance != null ? MealSystemManager.Instance.AutoSelect : autoLocal;
            set
            {
                if (MealSystemManager.Instance != null) MealSystemManager.Instance.AutoSelect = value;
                else autoLocal = value;
            }
        }

        private void Awake()
        {
            EnsureUIBuilt();
            EnsureAutoUIBuilt();
        }

        private void Update()
        {
            if (!AutoOn) return;

            // 時間は PushPause で止まっているので、timeScale に左右されない実時間で数える
            autoElapsed += Time.unscaledDeltaTime;
            UpdateAutoGauge();

            if (autoElapsed >= autoDelaySeconds)
            {
                ChooseAutoPlan();
            }
        }

        private void OnEnable()
        {
            // 判断画面を開いたときに時間を停止する (Decision 0004)
            if (!isPausedBySelf)
            {
                if (GameClock.Instance != null)
                {
                    GameClock.Instance.PushPause();
                }
                else if (GameClock.I != null)
                {
                    GameClock.I.PushPause();
                }
                isPausedBySelf = true;
                Debug.Log("[MealSelectionDialogUI] 食事選択画面を開きました。時間を停止します。");
            }

            // 所持金の更新と購読
            UpdateMoneyDisplay();
            if (GameState.I != null)
            {
                GameState.I.OnMoneyChanged += HandleMoneyChanged;
            }

            RefreshCards();
            ResetAutoTimer();
        }

        private void OnDisable()
        {
            // 判断画面を閉じたときに時間停止を解除する (Decision 0004)
            if (isPausedBySelf)
            {
                if (GameClock.Instance != null)
                {
                    GameClock.Instance.PopPause();
                }
                else if (GameClock.I != null)
                {
                    GameClock.I.PopPause();
                }
                isPausedBySelf = false;
                Debug.Log("[MealSelectionDialogUI] 食事選択画面を閉じました。時間を再開します。");
            }

            if (GameState.I != null)
            {
                GameState.I.OnMoneyChanged -= HandleMoneyChanged;
            }
        }

        private void HandleMoneyChanged(int money)
        {
            UpdateMoneyDisplay();
        }

        private void UpdateMoneyDisplay()
        {
            if (moneyText != null)
            {
                int currentMoney = (GameState.I != null) ? GameState.I.Money : 0;
                moneyText.text = $"現在の所持金：{currentMoney:N0}円";
            }
        }

        /// <summary>
        /// ダイアログをセットアップして表示する。
        /// previousGrade が null の場合は初回選択（青枠なし）。
        /// </summary>
        public void Open(MealGrade? previousGrade, Action<MealPlan> onSelected = null)
        {
            previousSelectedGrade = previousGrade;
            if (onSelected != null)
            {
                OnPlanSelected = null;
                OnPlanSelected += onSelected;
            }

            gameObject.SetActive(true);
            UpdateMoneyDisplay();
            RefreshCards();
            ResetAutoTimer();
        }

        /// <summary>
        /// カード一覧の描画更新
        /// </summary>
        public void RefreshCards()
        {
            EnsureUIBuilt();
            EnsureAutoUIBuilt();
            UpdateMoneyDisplay();

            if (plans == null || plans.Count == 0)
            {
                plans = MealPlan.CreateDefaultPlans();
            }

            for (int i = 0; i < cardUIs.Count && i < plans.Count; i++)
            {
                var plan = plans[i];
                bool isPrev = previousSelectedGrade.HasValue && plan.grade == previousSelectedGrade.Value;
                cardUIs[i].Setup(plan, isPrev, OnCardPlanChosen);
            }
        }

        // ---------------- オート選択 ----------------

        /// <summary>オートで選ばれるプラン。先月と同じ（初回は「普通」）。</summary>
        private MealPlan GetAutoPlan()
        {
            if (plans == null || plans.Count == 0) plans = MealPlan.CreateDefaultPlans();
            var grade = previousSelectedGrade ?? MealGrade.Normal;
            return plans.Find(p => p.grade == grade) ?? plans[0];
        }

        private void ResetAutoTimer()
        {
            autoElapsed = 0f;
            UpdateAutoView();
        }

        private void OnAutoButtonClicked()
        {
            AutoOn = !AutoOn;
            ResetAutoTimer();   // ON にした瞬間から猶予を数え直す
        }

        private void ChooseAutoPlan()
        {
            var plan = GetAutoPlan();
            Debug.Log($"[MealSelectionDialogUI] オート選択：猶予 {autoDelaySeconds:0.#} 秒が経過したため '{plan.title}' を自動で選びました。");
            OnCardPlanChosen(plan);
        }

        /// <summary>ボタンの見た目とゲージの表示/非表示を、オートの ON/OFF に合わせる。</summary>
        private void UpdateAutoView()
        {
            bool on = AutoOn;
            if (autoButtonLabel != null) autoButtonLabel.text = on ? "オート：ON" : "オート：OFF";
            if (autoButton != null && autoButton.targetGraphic != null)
            {
                autoButton.targetGraphic.color = on ? autoOnColor : autoOffColor;
            }
            if (autoGaugeRoot != null) autoGaugeRoot.gameObject.SetActive(on);
            if (on) UpdateAutoGauge();
        }

        private void UpdateAutoGauge()
        {
            float remain = Mathf.Max(0f, autoDelaySeconds - autoElapsed);
            float ratio = autoDelaySeconds > 0f ? Mathf.Clamp01(remain / autoDelaySeconds) : 0f;

            if (autoGaugeFill != null)
            {
                // 残り時間ぶんだけ左から幅が残る（満タン→空に減っていく）
                autoGaugeFill.anchorMax = new Vector2(ratio, 1f);
            }
            if (autoGaugeLabel != null)
            {
                autoGaugeLabel.text = $"あと{Mathf.CeilToInt(remain)}秒で「{GetAutoPlan().title}」を自動選択";
            }
        }

        private void EnsureAutoUIBuilt()
        {
            if (autoButton != null && autoGaugeRoot != null)
            {
                autoButton.onClick.RemoveListener(OnAutoButtonClicked);
                autoButton.onClick.AddListener(OnAutoButtonClicked);
                return;
            }
            if (containerPanel == null) return;

            var jpFont = Resources.Load<TMP_FontAsset>("JPFont") ?? TMP_Settings.defaultFontAsset;

            // 1. オートボタン（右下）
            var btnGO = new GameObject("AutoButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(containerPanel, false);
            SetRect(btnGO.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(-20f, 24f), new Vector2(130f, 30f));
            var btnImg = btnGO.GetComponent<Image>();
            btnImg.color = autoOffColor;
            autoButton = btnGO.GetComponent<Button>();
            autoButton.targetGraphic = btnImg;
            autoButton.onClick.AddListener(OnAutoButtonClicked);

            var btnLabelGO = new GameObject("Label", typeof(RectTransform));
            btnLabelGO.transform.SetParent(btnGO.transform, false);
            autoButtonLabel = btnLabelGO.AddComponent<TextMeshProUGUI>();
            if (jpFont != null) autoButtonLabel.font = jpFont;
            autoButtonLabel.fontSize = 15f;
            autoButtonLabel.fontStyle = FontStyles.Bold;
            autoButtonLabel.alignment = TextAlignmentOptions.Center;
            autoButtonLabel.color = Color.white;
            autoButtonLabel.raycastTarget = false;
            SetStretchFull(autoButtonLabel.rectTransform, 0f, 0f);

            // 2. ゲージ（左下。背景＋残り時間バー＋文字）
            var gaugeGO = new GameObject("AutoGauge", typeof(RectTransform), typeof(Image));
            gaugeGO.transform.SetParent(containerPanel, false);
            autoGaugeRoot = gaugeGO.GetComponent<RectTransform>();
            SetRect(autoGaugeRoot, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(20f, 24f), new Vector2(520f, 22f));
            var gaugeBg = gaugeGO.GetComponent<Image>();
            gaugeBg.color = gaugeBgColor;
            gaugeBg.raycastTarget = false;

            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGO.transform.SetParent(gaugeGO.transform, false);
            autoGaugeFill = fillGO.GetComponent<RectTransform>();
            autoGaugeFill.anchorMin = Vector2.zero;
            autoGaugeFill.anchorMax = Vector2.one;     // 幅は UpdateAutoGauge が anchorMax.x で動かす
            autoGaugeFill.offsetMin = Vector2.zero;
            autoGaugeFill.offsetMax = Vector2.zero;
            var fillImg = fillGO.GetComponent<Image>();
            fillImg.color = gaugeFillColor;
            fillImg.raycastTarget = false;

            var gaugeLabelGO = new GameObject("Label", typeof(RectTransform));
            gaugeLabelGO.transform.SetParent(gaugeGO.transform, false);
            autoGaugeLabel = gaugeLabelGO.AddComponent<TextMeshProUGUI>();
            if (jpFont != null) autoGaugeLabel.font = jpFont;
            autoGaugeLabel.fontSize = 13f;
            autoGaugeLabel.fontStyle = FontStyles.Bold;
            autoGaugeLabel.alignment = TextAlignmentOptions.Center;
            autoGaugeLabel.color = darkText;
            autoGaugeLabel.raycastTarget = false;
            SetStretchFull(autoGaugeLabel.rectTransform, 0f, 0f);

            UpdateAutoView();
        }

        private void OnCardPlanChosen(MealPlan chosenPlan)
        {
            Debug.Log($"[MealSelectionDialogUI] 食事プラン '{chosenPlan.title}' が選択されました。（費用はその月の日数で日割りされ、一日ごとに引き落とされます）");

            previousSelectedGrade = chosenPlan.grade;

            // 1. 選択時効果（ストレス・イベント率）を EffectApplier 経由で適用（費用は一日ごとに日割りで引く）
            if (chosenPlan.selectEffects != null && chosenPlan.selectEffects.Count > 0)
            {
                EffectApplier.Apply(chosenPlan.selectEffects);

                // テスト環境等で Household が未配置の場合、GameState の世帯ストレスにもフォールバック反映
                if (Household.I == null && GameState.I != null)
                {
                    var stressEffect = chosenPlan.selectEffects.Find(e => e.kind == EffectKind.Stress);
                    if (stressEffect != null)
                    {
                        GameState.I.AddStress(Mathf.RoundToInt(stressEffect.value));
                    }
                }
            }

            // 2. コールバック通知
            OnPlanSelected?.Invoke(chosenPlan);

            // 3. 画面を閉じる（OnDisable により時間停止が解除される）
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 静的メソッド：シーン内の適切な Canvas にダイアログを表示する
        /// previousGrade が null の場合は初回（青枠なし）。
        /// </summary>
        public static MealSelectionDialogUI ShowDialog(MealGrade? previousGrade = null, Action<MealPlan> onPlanChosen = null)
        {
            var existing = FindObjectOfType<MealSelectionDialogUI>(true);
            if (existing != null)
            {
                existing.Open(previousGrade, onPlanChosen);
                return existing;
            }

            // Canvas を検索
            var canvasGO = GameObject.Find("HUD_Canvas") ?? GameObject.Find("Canvas") ?? FindObjectOfType<Canvas>()?.gameObject;
            if (canvasGO == null)
            {
                var newCanvas = new GameObject("MealDialog_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var c = newCanvas.GetComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 950;
                var scaler = newCanvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1440, 1024);
                canvasGO = newCanvas;
            }

            var dialogGO = new GameObject("MealSelectionDialog_Modal", typeof(RectTransform));
            dialogGO.transform.SetParent(canvasGO.transform, false);
            var rt = dialogGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var ui = dialogGO.AddComponent<MealSelectionDialogUI>();
            ui.Open(previousGrade, onPlanChosen);
            return ui;
        }

        // ---------------- UI 動的構築 ----------------

        private void EnsureUIBuilt()
        {
            if (cardUIs.Count >= 3 && titleText != null && moneyText != null) return;

            var jpFont = Resources.Load<TMP_FontAsset>("JPFont") ?? TMP_Settings.defaultFontAsset;

            // 1. 全画面モーダル背景（半透明黒）
            var bgImage = GetComponent<Image>();
            if (bgImage == null)
            {
                bgImage = gameObject.AddComponent<Image>();
                bgImage.color = new Color(0f, 0f, 0f, 0.45f);
            }

            // 2. メインパネル（ベージュ背景）
            if (containerPanel == null)
            {
                var panelGO = new GameObject("PanelContainer", typeof(RectTransform), typeof(Image));
                panelGO.transform.SetParent(transform, false);
                var panelImg = panelGO.GetComponent<Image>();
                panelImg.color = panelBg;
                containerPanel = panelGO.GetComponent<RectTransform>();
                SetRect(containerPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, windowSize);
            }
            else
            {
                containerPanel.sizeDelta = windowSize;
            }

            // 3. タイトル（「今月の食事選択」）
            if (titleText == null)
            {
                var titleGO = new GameObject("DialogTitle", typeof(RectTransform));
                titleGO.transform.SetParent(containerPanel, false);
                titleText = titleGO.AddComponent<TextMeshProUGUI>();
                if (jpFont != null) titleText.font = jpFont;
                titleText.text = "今月の食事選択";
                titleText.fontSize = 26f;
                titleText.fontStyle = FontStyles.Bold;
                titleText.alignment = TextAlignmentOptions.Center;
                titleText.color = darkText;

                var titleRT = titleGO.GetComponent<RectTransform>();
                SetRect(titleRT, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(400f, 34f));
            }

            // 4. 所持金バッジ（「現在の所持金：300,000円」）
            if (moneyText == null)
            {
                var moneyBadgeGO = new GameObject("MoneyBadge", typeof(RectTransform), typeof(Image));
                moneyBadgeGO.transform.SetParent(containerPanel, false);
                var badgeImg = moneyBadgeGO.GetComponent<Image>();
                badgeImg.color = moneyBadgeBg;
                var badgeRT = moneyBadgeGO.GetComponent<RectTransform>();
                SetRect(badgeRT, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(280f, 26f));

                var moneyTextGO = new GameObject("MoneyText", typeof(RectTransform));
                moneyTextGO.transform.SetParent(moneyBadgeGO.transform, false);
                moneyText = moneyTextGO.AddComponent<TextMeshProUGUI>();
                if (jpFont != null) moneyText.font = jpFont;
                moneyText.fontSize = 15f;
                moneyText.fontStyle = FontStyles.Bold;
                moneyText.alignment = TextAlignmentOptions.Center;
                moneyText.color = darkText;
                SetStretchFull(moneyText.rectTransform, 0f, 0f);

                UpdateMoneyDisplay();
            }

            // 5. カードコンテナ（横並びエリア）
            if (cardsParent == null)
            {
                var cardsAreaGO = new GameObject("CardsArea", typeof(RectTransform));
                cardsAreaGO.transform.SetParent(containerPanel, false);
                var cardsRT = cardsAreaGO.GetComponent<RectTransform>();
                SetRect(cardsRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -32f), new Vector2(660f, 350f));
                cardsParent = cardsAreaGO.transform;
            }

            // 6. 3枚のカードを配置
            if (plans == null || plans.Count == 0)
            {
                plans = MealPlan.CreateDefaultPlans();
            }

            cardUIs.Clear();
            float cardWidth = 200f;
            float cardHeight = 330f;
            float spacing = 16f;
            float startX = -((cardWidth * 3f + spacing * 2f) / 2f) + (cardWidth / 2f);

            for (int i = 0; i < plans.Count; i++)
            {
                var cardGO = new GameObject($"MealCard_{plans[i].grade}", typeof(RectTransform));
                cardGO.transform.SetParent(cardsParent, false);

                var cardRT = cardGO.GetComponent<RectTransform>();
                float posX = startX + i * (cardWidth + spacing);
                SetRect(cardRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(posX, 0f), new Vector2(cardWidth, cardHeight));

                var cardUI = cardGO.AddComponent<MealCardUI>();
                cardUI.EnsureUIElements();
                cardUIs.Add(cardUI);
            }
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
