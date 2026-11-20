using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Arisaka;

namespace Kusao.TutorialGuide
{
    /// <summary>
    /// ゲーム開始直後に実行されるスポットライト型チュートリアルの進行管理コントローラー。
    /// 時間の一時停止、各UIのハイライト、ボタン押下待ち、画面の開閉、閲覧済みフラグの保存を管理します。
    /// </summary>
    public class TutorialGuideController : MonoBehaviour
    {
        [Header("基本設定")]
        [Tooltip("テスト用: すでに閲覧済みでもゲーム開始時に常にチュートリアルを実行するか")]
        [SerializeField] private bool forceShowOnStart = true;

        [Tooltip("閲覧済みフラグを保存するキー")]
        [SerializeField] private string saveKey = "Tutorial_SystemGuide_Viewed";

        [Header("コンポーネント参照")]
        [SerializeField] private TutorialGuideSpotlight spotlight;
        [SerializeField] private TutorialGuidePopup popup;

        [Header("監視・操作対象画面")]
        [Tooltip("サポート画面 (Canvas/supports)")]
        [SerializeField] private GameObject supportsPanel;

        [Tooltip("家計簿画面 (Canvas/LedgerPanel)")]
        [SerializeField] private GameObject ledgerPanel;

        [Tooltip("カレンダー画面 (Canvas/CalendarUI など)")]
        [SerializeField] private GameObject calendarPanel;

        [Tooltip("家族詳細パネル (Canvas/detail)")]
        [SerializeField] private SlidePanel detailSlidePanel;

        [Header("チュートリアルステップ定義")]
        [SerializeField] private List<TutorialGuideStep> steps = new List<TutorialGuideStep>();

        /// <summary>現在チュートリアルガイドが実行中かどうか</summary>
        public static bool IsGuideRunning { get; private set; }

        private int currentStepIndex = -1;
        private bool isPausedBySelf = false;
        private Button currentWatchedButton = null;
        private UnityEngine.Events.UnityAction currentButtonAction = null;
        private readonly List<GameObject> suppressedModals = new List<GameObject>();

        private Vector2 lastInspectedOffset;
        private Vector2 lastInspectedPadding;
        private RectTransform lastInspectedTarget;

        private void Awake()
        {
            EnsureCanvasAndComponents();

            if (popup != null)
            {
                popup.OnNextClicked += HandleNextClicked;
                popup.OnSkipClicked += HandleSkipClicked;
            }
        }

        /// <summary>
        /// Canvas 設定や Spotlight / Popup コンポーネントが存在しない場合は自動構築する
        /// </summary>
        public void EnsureCanvasAndComponents()
        {
            var rootRect = transform as RectTransform;
            if (rootRect != null)
            {
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;
                rootRect.pivot = new Vector2(0.5f, 0.5f);
                rootRect.localScale = Vector3.one;
            }

            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 300;
            }

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1440, 1024);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            if (spotlight == null) spotlight = GetComponentInChildren<TutorialGuideSpotlight>(true);
            if (spotlight == null)
            {
                var spotGO = new GameObject("Spotlight", typeof(RectTransform));
                spotGO.transform.SetParent(transform, false);
                spotlight = spotGO.AddComponent<TutorialGuideSpotlight>();
                spotlight.EnsurePanels();
            }

            if (popup == null) popup = GetComponentInChildren<TutorialGuidePopup>(true);
            if (popup == null)
            {
                var popGO = new GameObject("Popup", typeof(RectTransform));
                popGO.transform.SetParent(transform, false);
                popup = popGO.AddComponent<TutorialGuidePopup>();
                popup.EnsureUIBuilt();
            }
        }

        /// <summary>
        /// シーンロード時、Main シーンに TutorialGuide が無ければ自動フォールバック生成する
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnSceneLoadedRuntime()
        {
            // 既に存在していれば何もしない
            if (FindFirstObjectByType<TutorialGuideController>(FindObjectsInactive.Include) != null) return;

            // Canvas または HUD_Canvas があるメインゲーム画面のみで起動
            var canvasGO = GameObject.Find("Canvas") ?? GameObject.Find("HUD_Canvas");
            if (canvasGO == null) return;

            // タイトル画面などでは起動しない
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.IndexOf("Start", StringComparison.OrdinalIgnoreCase) >= 0) return;

            // 動的生成
            var guideGO = new GameObject("TutorialGuide_Canvas");
            var ctrl = guideGO.AddComponent<TutorialGuideController>();
            ctrl.EnsureCanvasAndComponents();
        }

        private void Start()
        {
            AutoResolveReferences();

            // 既定のステップが未定義の場合は自動構築
            if (steps == null || steps.Count == 0)
            {
                CreateDefaultSteps();
            }

            // 閲覧済みチェック
            if (!forceShowOnStart && PlayerPrefs.GetInt(saveKey, 0) == 1)
            {
                gameObject.SetActive(false);
                return;
            }

            // 開始直後の1フレーム待ってから起動（他コンポーネントの初期化完了待ち）
            StartCoroutine(CoStartGuide());
        }

        private void Update()
        {
            if (!IsGuideRunning) return;

            // 食事選択ダイアログなどが割り込んできたら一時的に非表示にして退避
            SuppressMealModals();

            // インスペクターで offset や padding が変更されたらリアルタイムに画面へ反映
            if (currentStepIndex >= 0 && currentStepIndex < steps.Count)
            {
                var s = steps[currentStepIndex];
                if (s.positionOffset != lastInspectedOffset || s.padding != lastInspectedPadding || s.targetRect != lastInspectedTarget)
                {
                    lastInspectedOffset = s.positionOffset;
                    lastInspectedPadding = s.padding;
                    lastInspectedTarget = s.targetRect;
                    RefreshCurrentStepLayout();
                }
            }
        }

        private void OnDisable()
        {
            IsGuideRunning = false;
            CleanupButtonListener();
            RestoreMealModals();
            ResumeGameTime();
        }

        private IEnumerator CoStartGuide()
        {
            yield return null; // 1フレーム待機
            StartTutorial();
        }

        /// <summary>
        /// チュートリアルを開始する
        /// </summary>
        public void StartTutorial()
        {
            IsGuideRunning = true;
            gameObject.SetActive(true);
            SuppressMealModals();
            PauseGameTime();
            currentStepIndex = -1;
            NextStep();
        }

        /// <summary>
        /// 現在のステップのスポットライトとポップアップの位置を再計算する（インスペクター調整用）
        /// </summary>
        public void RefreshCurrentStepLayout()
        {
            if (currentStepIndex < 0 || currentStepIndex >= steps.Count) return;
            var step = steps[currentStepIndex];

            RectTransform targetRect = step.targetRect;
            if (targetRect == null && !string.IsNullOrEmpty(step.targetUiPath))
            {
                targetRect = ResolveTargetRect(step.targetUiPath);
            }

            if (spotlight != null)
            {
                spotlight.SetTarget(targetRect, step.padding, step.positionOffset);
            }

            if (popup != null)
            {
                bool isLast = (currentStepIndex == steps.Count - 1);
                string progress = $"{currentStepIndex + 1} / {steps.Count}";
                bool isWaitingButton = (step.actionType == GuideActionType.WaitButtonClick && targetRect != null);
                popup.Show(
                    step.title,
                    step.description,
                    progress,
                    isLast,
                    isWaitingButton,
                    step.buttonPromptHint,
                    targetRect,
                    spotlight != null ? spotlight.CurrentCutoutRect : Rect.zero
                );
            }
        }

        /// <summary>
        /// 次のステップに進む
        /// </summary>
        public void NextStep()
        {
            CleanupButtonListener();

            // 前のステップの後処理
            if (currentStepIndex >= 0 && currentStepIndex < steps.Count)
            {
                var prevStep = steps[currentStepIndex];
                if (prevStep.closeScreenOnExit)
                {
                    CloseScreen(prevStep.openScreen);
                }
            }

            currentStepIndex++;

            if (currentStepIndex >= steps.Count)
            {
                CompleteTutorial();
                return;
            }

            ShowCurrentStep();
        }

        private void ShowCurrentStep()
        {
            if (currentStepIndex < 0 || currentStepIndex >= steps.Count) return;

            var step = steps[currentStepIndex];

            lastInspectedOffset = step.positionOffset;
            lastInspectedPadding = step.padding;
            lastInspectedTarget = step.targetRect;

            // 画面を開く指定がある場合
            if (step.openScreen != GuideScreenTarget.None)
            {
                OpenScreen(step.openScreen);
            }

            // 対象の RectTransform を解決
            RectTransform targetRect = step.targetRect;
            if (targetRect == null && !string.IsNullOrEmpty(step.targetUiPath))
            {
                targetRect = ResolveTargetRect(step.targetUiPath);
            }

            // スポットライトの設定（余白と手動微調整オフセットを適用）
            if (spotlight != null)
            {
                spotlight.SetTarget(targetRect, step.padding, step.positionOffset);
            }

            // ボタンクリック待ちの設定
            bool isWaitingButton = (step.actionType == GuideActionType.WaitButtonClick && targetRect != null);
            if (isWaitingButton)
            {
                var btn = targetRect.GetComponentInChildren<Button>();
                if (btn != null)
                {
                    currentWatchedButton = btn;
                    currentButtonAction = () =>
                    {
                        // プレイヤーがボタンを押したときのハンドラ
                        NextStep();
                    };
                    btn.onClick.AddListener(currentButtonAction);
                }
            }

            // ポップアップの表示
            if (popup != null)
            {
                bool isLast = (currentStepIndex == steps.Count - 1);
                string progress = $"{currentStepIndex + 1} / {steps.Count}";
                popup.Show(
                    step.title,
                    step.description,
                    progress,
                    isLast,
                    isWaitingButton,
                    step.buttonPromptHint,
                    targetRect,
                    spotlight != null ? spotlight.CurrentCutoutRect : Rect.zero
                );
            }
        }

        private void HandleNextClicked()
        {
            NextStep();
        }

        private void HandleSkipClicked()
        {
            CompleteTutorial();
        }

        /// <summary>
        /// チュートリアルを完了・終了する
        /// </summary>
        public void CompleteTutorial()
        {
            IsGuideRunning = false;
            CleanupButtonListener();

            // 開いているパネル類をすべて閉じる
            CloseAllScreens();

            if (spotlight != null) spotlight.Hide();
            if (popup != null) popup.Hide();

            PlayerPrefs.SetInt(saveKey, 1);
            PlayerPrefs.Save();

            // 退避していた食事ダイアログ等を再表示
            RestoreMealModals();

            ResumeGameTime();
            gameObject.SetActive(false);
        }

        // ---------------- 食事選択ダイアログなどの退避制御 ----------------

        private void SuppressMealModals()
        {
            // 食事選択ダイアログ（MealSelectionDialog_Modal / MealSelectionDialogUI）
            var mealModals = GameObject.FindObjectsByType<MealSelectionDialogUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var m in mealModals)
            {
                if (m != null && m.gameObject.activeSelf)
                {
                    m.gameObject.SetActive(false);
                    if (!suppressedModals.Contains(m.gameObject))
                    {
                        suppressedModals.Add(m.gameObject);
                    }
                }
            }
        }

        private void RestoreMealModals()
        {
            foreach (var go in suppressedModals)
            {
                if (go != null)
                {
                    go.SetActive(true);
                }
            }
            suppressedModals.Clear();
        }

        /// <summary>
        /// 設定画面などから再度チュートリアルを見返すための公開メソッド
        /// </summary>
        public void ReplayTutorial()
        {
            gameObject.SetActive(true);
            StartTutorial();
        }

        // ---------------- 画面の開閉制御 ----------------

        private void OpenScreen(GuideScreenTarget screen)
        {
            switch (screen)
            {
                case GuideScreenTarget.DetailPanel:
                    if (detailSlidePanel != null) detailSlidePanel.Open();
                    break;

                case GuideScreenTarget.SupportPanel:
                    if (supportsPanel != null) supportsPanel.SetActive(true);
                    break;

                case GuideScreenTarget.CalendarPanel:
                    if (calendarPanel != null)
                    {
                        calendarPanel.SetActive(true);
                    }
                    else
                    {
                        var cal = CalendarUI.ShowCalendar();
                        if (cal != null) calendarPanel = cal.gameObject;
                    }
                    break;

                case GuideScreenTarget.LedgerPanel:
                    if (ledgerPanel != null) ledgerPanel.SetActive(true);
                    break;
            }
        }

        private void CloseScreen(GuideScreenTarget screen)
        {
            switch (screen)
            {
                case GuideScreenTarget.DetailPanel:
                    if (detailSlidePanel != null) detailSlidePanel.Close();
                    break;

                case GuideScreenTarget.SupportPanel:
                    if (supportsPanel != null) supportsPanel.SetActive(false);
                    break;

                case GuideScreenTarget.CalendarPanel:
                    if (calendarPanel != null) calendarPanel.SetActive(false);
                    break;

                case GuideScreenTarget.LedgerPanel:
                    if (ledgerPanel != null) ledgerPanel.SetActive(false);
                    break;
            }
        }

        private void CloseAllScreens()
        {
            CloseScreen(GuideScreenTarget.DetailPanel);
            CloseScreen(GuideScreenTarget.SupportPanel);
            CloseScreen(GuideScreenTarget.CalendarPanel);
            CloseScreen(GuideScreenTarget.LedgerPanel);
        }

        // ---------------- 時間停止制御 ----------------

        private void PauseGameTime()
        {
            if (!isPausedBySelf && GameClock.I != null)
            {
                GameClock.I.PushPause();
                isPausedBySelf = true;
            }
        }

        private void ResumeGameTime()
        {
            if (isPausedBySelf && GameClock.I != null)
            {
                GameClock.I.PopPause();
                isPausedBySelf = false;
            }
        }

        private void CleanupButtonListener()
        {
            if (currentWatchedButton != null && currentButtonAction != null)
            {
                currentWatchedButton.onClick.RemoveListener(currentButtonAction);
                currentWatchedButton = null;
                currentButtonAction = null;
            }
        }

        // ---------------- 対象UI解決（Auto-Resolve） ----------------

        public void AutoResolveReferences()
        {
            var canvas = GameObject.Find("Canvas") ?? GameObject.Find("HUD_Canvas");
            if (canvas == null) return;

            if (supportsPanel == null)
            {
                var t = canvas.transform.Find("supports");
                if (t != null) supportsPanel = t.gameObject;
            }

            if (ledgerPanel == null)
            {
                var t = canvas.transform.Find("LedgerPanel");
                if (t != null) ledgerPanel = t.gameObject;
            }

            if (detailSlidePanel == null)
            {
                var t = canvas.transform.Find("detail");
                if (t != null) detailSlidePanel = t.GetComponent<SlidePanel>();
            }

            if (calendarPanel == null)
            {
                var cal = FindObjectOfType<CalendarUI>(true);
                if (cal != null) calendarPanel = cal.gameObject;
            }
        }

        private RectTransform ResolveTargetRect(string pathOrName)
        {
            if (string.IsNullOrEmpty(pathOrName)) return null;

            var canvas = GameObject.Find("Canvas") ?? GameObject.Find("HUD_Canvas");

            // 1. 右端の開閉トグルボタン特別検索
            if (pathOrName.Equals("StatusToggle", StringComparison.OrdinalIgnoreCase) ||
                pathOrName.Equals("detail_btn", StringComparison.OrdinalIgnoreCase))
            {
                // StatusToggle を探す
                if (canvas != null)
                {
                    var toggle = FindChildRecursive(canvas.transform, "StatusToggle");
                    if (toggle != null) return toggle as RectTransform;
                }

                // detailSlidePanel の Toggle を呼んでいるボタンを探す
                if (detailSlidePanel != null && canvas != null)
                {
                    var buttons = canvas.GetComponentsInChildren<Button>(true);
                    foreach (var b in buttons)
                    {
                        for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                        {
                            if (b.onClick.GetPersistentTarget(i) == detailSlidePanel)
                            {
                                return b.transform as RectTransform;
                            }
                        }
                    }
                }

                // Canvas 直下の "Button" という名前のボタン（Main_test2の開閉矢印）
                if (canvas != null)
                {
                    var btnT = canvas.transform.Find("Button");
                    if (btnT != null) return btnT as RectTransform;
                }
            }

            // 2. お金（親コンテナより金額テキストを優先）
            if (pathOrName.Equals("money", StringComparison.OrdinalIgnoreCase) ||
                pathOrName.Equals("money_text", StringComparison.OrdinalIgnoreCase))
            {
                if (canvas != null)
                {
                    var moneyText = FindChildRecursive(canvas.transform, "money_text")
                                    ?? FindChildRecursive(canvas.transform, "MoneyText")
                                    ?? FindChildRecursive(canvas.transform, "MoneyBox");
                    if (moneyText != null) return moneyText as RectTransform;

                    // 金額テキストがない UI では親コンテナを対象にする
                    var moneyParent = FindChildRecursive(canvas.transform, "money");
                    if (moneyParent != null) return moneyParent as RectTransform;
                }
            }

            // 3. 日付（day_text を優先）
            if (pathOrName.Equals("Day", StringComparison.OrdinalIgnoreCase) ||
                pathOrName.Equals("day_text", StringComparison.OrdinalIgnoreCase))
            {
                if (canvas != null)
                {
                    var dt = FindChildRecursive(canvas.transform, "day_text")
                             ?? FindChildRecursive(canvas.transform, "AgeText")
                             ?? FindChildRecursive(canvas.transform, "AgeBox");
                    if (dt != null) return dt as RectTransform;
                }
            }

            // 4. 一般検索
            var go = GameObject.Find(pathOrName);
            if (go != null) return go.GetComponent<RectTransform>();

            if (canvas != null)
            {
                var t = FindChildRecursive(canvas.transform, pathOrName);
                if (t != null)
                {
                    if (pathOrName.Equals("money", StringComparison.OrdinalIgnoreCase))
                    {
                        var childText = t.Find("money_text");
                        if (childText != null) return childText as RectTransform;
                    }
                    else if (pathOrName.Equals("Day", StringComparison.OrdinalIgnoreCase))
                    {
                        var childText = t.Find("day_text");
                        if (childText != null) return childText as RectTransform;
                    }
                    return t as RectTransform;
                }
            }

            return null;
        }

        private Transform FindChildRecursive(Transform parent, string targetName)
        {
            if (parent == null) return null;
            if (parent.name.Equals(targetName, StringComparison.OrdinalIgnoreCase)) return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                var res = FindChildRecursive(parent.GetChild(i), targetName);
                if (res != null) return res;
            }
            return null;
        }

        // ---------------- デフォルトステップ定義 ----------------

        private void CreateDefaultSteps()
        {
            steps = new List<TutorialGuideStep>
            {
                // Step 1: お金
                new TutorialGuideStep
                {
                    stepId = "money",
                    title = "家計の所持金",
                    description = "現在の所持金です。毎月給与が振り込まれ、生活費や学費が引き落とされます。\nマイナス状態が続くとゲームオーバーになるので注意しましょう！",
                    targetUiPath = "money_text",
                    actionType = GuideActionType.WaitNextButton,
                    padding = new Vector2(30f, 16f),
                    positionOffset = Vector2.zero
                },
                // Step 2: 日付・年齢と倍速
                new TutorialGuideStep
                {
                    stepId = "day_age",
                    title = "子どもの年齢・日付と進行速度",
                    description = "子どもの現在の年齢と日付です。リアルタイムで時間が進み、子どもが成長していきます。\n右上の［1x］［2x］［3x］ボタンで時間の進む速さを変更できます（テンポよく進めたい時に便利です！）。",
                    targetUiPath = "day_text",
                    actionType = GuideActionType.WaitNextButton,
                    padding = new Vector2(30f, 16f),
                    positionOffset = Vector2.zero
                },
                // Step 3: 成長ゲージ
                new TutorialGuideStep
                {
                    stepId = "growth_gauge",
                    title = "子どもの成長ゲージ",
                    description = "あかちゃんから成人（巣立ち）までの成長段階を示しています。\n満期を迎えるとエンディングとなります。",
                    targetUiPath = "Slider",
                    actionType = GuideActionType.WaitNextButton,
                    padding = new Vector2(24f, 14f),
                    positionOffset = Vector2.zero
                },
                // Step 4: 家族ステータス（ボタン操作）
                new TutorialGuideStep
                {
                    stepId = "detail_btn",
                    title = "家族ステータス",
                    description = "家族（両親・子ども）のストレスや健康状態を確認できるパネルです。\n右端のボタンを押して開いてみましょう！",
                    targetUiPath = "StatusToggle",
                    actionType = GuideActionType.WaitButtonClick,
                    buttonPromptHint = "↑ 右端の矢印ボタンを押してみましょう！",
                    padding = new Vector2(16f, 16f),
                    positionOffset = Vector2.zero
                },
                // Step 5: 家族ステータス詳細
                new TutorialGuideStep
                {
                    stepId = "detail_panel",
                    title = "家族のコンディション",
                    description = "健康度や仕事、勉強の状況が確認できます。\nストレスが溜まりすぎると倒れたり家出の原因になります。定期的にチェックしましょう！",
                    targetUiPath = "detail",
                    actionType = GuideActionType.WaitNextButton,
                    openScreen = GuideScreenTarget.DetailPanel,
                    closeScreenOnExit = true,
                    padding = new Vector2(20f, 20f),
                    positionOffset = Vector2.zero
                },
                // Step 6: サポートボタン（ボタン操作）
                new TutorialGuideStep
                {
                    stepId = "support_btn",
                    title = "子育て支援制度",
                    description = "国や自治体、会社の子育て支援制度を申請できる画面です。\nボタンを押して開いてみましょう！",
                    targetUiPath = "suport",
                    actionType = GuideActionType.WaitButtonClick,
                    buttonPromptHint = "↑ 「サポート」ボタンを押してみましょう！",
                    padding = new Vector2(16f, 16f),
                    positionOffset = Vector2.zero
                },
                // Step 7: サポート画面の中身
                new TutorialGuideStep
                {
                    stepId = "support_panel",
                    title = "支援制度の活用",
                    description = "児童手当や医療費助成など、様々な支援制度が一覧で並んでいます。\n申請期限があるものもあるので、こまめに確認して忘れずに申請しましょう！",
                    targetUiPath = "supports",
                    actionType = GuideActionType.WaitNextButton,
                    openScreen = GuideScreenTarget.SupportPanel,
                    closeScreenOnExit = true,
                    padding = new Vector2(20f, 20f),
                    positionOffset = Vector2.zero
                },
                // Step 8: カレンダーボタン（ボタン操作）
                new TutorialGuideStep
                {
                    stepId = "calendar_btn",
                    title = "カレンダー機能",
                    description = "予定や申請期限を管理するカレンダーです。\nボタンを押して開いてみましょう！",
                    targetUiPath = "calendar",
                    actionType = GuideActionType.WaitButtonClick,
                    buttonPromptHint = "↑ 「カレンダー」ボタンを押してみましょう！",
                    padding = new Vector2(16f, 16f),
                    positionOffset = Vector2.zero
                },
                // Step 9: カレンダー画面の中身
                new TutorialGuideStep
                {
                    stepId = "calendar_panel",
                    title = "予定と期限の管理",
                    description = "月ごとの予定や、申請した制度の申請期限・受給予定日が表示されます。\n期限が近づいた予定を見逃さないようにしましょう！",
                    targetUiPath = "CalendarUI",
                    actionType = GuideActionType.WaitNextButton,
                    openScreen = GuideScreenTarget.CalendarPanel,
                    closeScreenOnExit = true,
                    padding = new Vector2(20f, 20f),
                    positionOffset = Vector2.zero
                },
                // Step 10: 家計簿ボタン（ボタン操作）
                new TutorialGuideStep
                {
                    stepId = "kakeibo_btn",
                    title = "家計簿機能",
                    description = "家計の収支を確認できる家計簿です。\nボタンを押して開いてみましょう！",
                    targetUiPath = "Kakeibo",
                    actionType = GuideActionType.WaitButtonClick,
                    buttonPromptHint = "↑ 「家計簿」ボタンを押してみましょう！",
                    padding = new Vector2(16f, 16f),
                    positionOffset = Vector2.zero
                },
                // Step 11: 家計簿画面の中身
                new TutorialGuideStep
                {
                    stepId = "kakeibo_panel",
                    title = "家計の収支チェック",
                    description = "月別・年別の収支や、食費・教育費などの内訳を確認できます。\n支出を見直して赤字を防ぎましょう！",
                    targetUiPath = "LedgerPanel",
                    actionType = GuideActionType.WaitNextButton,
                    openScreen = GuideScreenTarget.LedgerPanel,
                    closeScreenOnExit = true,
                    padding = new Vector2(20f, 20f),
                    positionOffset = Vector2.zero
                },
                // Step 12: 完了
                new TutorialGuideStep
                {
                    stepId = "finish",
                    title = "チュートリアル完了！",
                    description = "基本操作の説明は以上です！\n子どもの成長を見守りながら、立派に育て上げましょう！",
                    targetUiPath = null, // 全画面
                    actionType = GuideActionType.WaitNextButton,
                    padding = Vector2.zero,
                    positionOffset = Vector2.zero
                }
            };
        }
    }
}
