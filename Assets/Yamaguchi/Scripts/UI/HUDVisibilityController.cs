using UnityEngine;

namespace Yamaguchi.UI
{
    /// <summary>
    /// サポート画面（制度一覧）、家計簿画面、詳細パネル（家族ステータス）の開閉に合わせて、
    /// 重なって邪魔になるHUDボタン群の表示/非表示を自動制御するコントローラー。
    /// 
    /// 1. サポート画面（Canvas/supports）が開いたとき：
    ///    - 画面下部のボタン群（Canvas/Buttons）および画面上部の倍速ボタン（Canvas/GameSpeedUI）を非表示にする。
    ///    - サポート画面が閉じたら、元の状態に再表示する。
    /// 
    /// 2. 家計簿画面（Canvas/LedgerPanel）が開いたとき：
    ///    - 画面上部の倍速ボタン（Canvas/GameSpeedUI）を非表示にする。
    ///    - 家計簿画面が閉じたら、再表示する。
    /// 
    /// 3. 右の矢印（Canvas/detail の SlidePanel）で家族詳細が見れるとき：
    ///    - 家計簿ボタン（Canvas/Buttons/Kakeibo）を非表示にする。
    ///    - 詳細パネルが閉じたら、家計簿ボタンを再表示する。
    /// </summary>
    public class HUDVisibilityController : MonoBehaviour
    {
        [Header("監視対象パネル")]
        [Tooltip("サポート画面 (Canvas/supports)")]
        [SerializeField] private GameObject supportsPanel;

        [Tooltip("家計簿画面 (Canvas/LedgerPanel)")]
        [SerializeField] private GameObject ledgerPanel;

        [Tooltip("人物詳細パネル (Canvas/detail)")]
        [SerializeField] private SlidePanel detailSlidePanel;

        [Header("表示制御するボタン")]
        [Tooltip("下部ボタン群ルート (Canvas/Buttons)")]
        [SerializeField] private GameObject buttonsRoot;

        [Tooltip("家計簿ボタン (Canvas/Buttons/Kakeibo)")]
        [SerializeField] private GameObject kakeiboButton;

        [Tooltip("倍速ボタンルート (Canvas/GameSpeedUI)")]
        [SerializeField] private GameObject gameSpeedUIRoot;

        private bool lastSupportsActive;
        private bool lastLedgerActive;
        private bool lastDetailOpen;

        private void Awake()
        {
            AutoResolveReferences();
        }

        private void Start()
        {
            AutoResolveReferences();
            UpdateVisibility(force: true);

            if (detailSlidePanel != null)
            {
                detailSlidePanel.OnOpenChanged += HandleDetailSlideChanged;
            }
        }

        private void OnDestroy()
        {
            if (detailSlidePanel != null)
            {
                detailSlidePanel.OnOpenChanged -= HandleDetailSlideChanged;
            }
        }

        private void Update()
        {
            // インスペクターで参照が未アサインの場合の自己解決フォールバック
            if (supportsPanel == null || buttonsRoot == null || ledgerPanel == null)
            {
                AutoResolveReferences();
            }

            // supportsPanel, ledgerPanel のアクティブ状態の変化、または detailSlidePanel の開閉変化を監視
            bool currentSupports = supportsPanel != null && supportsPanel.activeInHierarchy;
            bool currentLedger = ledgerPanel != null && ledgerPanel.activeInHierarchy;
            bool currentDetail = detailSlidePanel != null && detailSlidePanel.IsOpen;

            if (currentSupports != lastSupportsActive || currentLedger != lastLedgerActive || currentDetail != lastDetailOpen)
            {
                lastSupportsActive = currentSupports;
                lastLedgerActive = currentLedger;
                lastDetailOpen = currentDetail;
                UpdateVisibility();
            }
        }

        private void HandleDetailSlideChanged(bool isOpen)
        {
            lastDetailOpen = isOpen;
            UpdateVisibility();
        }

        public void UpdateVisibility(bool force = false)
        {
            bool isSupportsOpen = supportsPanel != null && supportsPanel.activeInHierarchy;
            bool isLedgerOpen = ledgerPanel != null && ledgerPanel.activeInHierarchy;
            bool isDetailOpen = detailSlidePanel != null && detailSlidePanel.IsOpen;

            // 1. サポート画面が開いているとき：
            //    下部ボタン群（Buttons）を非表示にする
            if (buttonsRoot != null)
            {
                buttonsRoot.SetActive(!isSupportsOpen);
            }

            // 2. 倍速ボタン（GameSpeedUI）の制御：
            //    サポート画面が開いているとき、または家計簿画面が開いているときは非表示にする
            if (gameSpeedUIRoot != null)
            {
                bool hideSpeed = isSupportsOpen || isLedgerOpen;
                gameSpeedUIRoot.SetActive(!hideSpeed);
            }

            // 3. 家計簿ボタンの制御：
            //    サポート画面が開いているときは Buttons ごと消える。
            //    サポート画面が閉じていても、詳細パネルが開いているときは家計簿ボタンのみ非表示にする。
            if (kakeiboButton != null && !isSupportsOpen)
            {
                kakeiboButton.SetActive(!isDetailOpen);
            }
        }

        /// <summary>
        /// シーン内の各オブジェクトを自動検出して参照をバインドする
        /// </summary>
        public void AutoResolveReferences()
        {
            var canvas = GetComponentInParent<Canvas>();
            Transform canvasT = canvas != null ? canvas.transform : transform.root;

            if (supportsPanel == null)
            {
                var t = canvasT.Find("supports");
                if (t != null) supportsPanel = t.gameObject;
            }

            if (ledgerPanel == null)
            {
                var t = canvasT.Find("LedgerPanel");
                if (t != null) ledgerPanel = t.gameObject;
            }

            if (detailSlidePanel == null)
            {
                var t = canvasT.Find("detail");
                if (t != null) detailSlidePanel = t.GetComponent<SlidePanel>();
            }

            if (buttonsRoot == null)
            {
                var t = canvasT.Find("Buttons");
                if (t != null) buttonsRoot = t.gameObject;
            }

            if (kakeiboButton == null)
            {
                if (buttonsRoot != null)
                {
                    var t = buttonsRoot.transform.Find("Kakeibo");
                    if (t != null) kakeiboButton = t.gameObject;
                }
                else
                {
                    var t = canvasT.Find("Buttons/Kakeibo");
                    if (t != null) kakeiboButton = t.gameObject;
                }
            }

            if (gameSpeedUIRoot == null)
            {
                var t = canvasT.Find("GameSpeedUI");
                if (t != null) gameSpeedUIRoot = t.gameObject;
            }
        }
    }
}
