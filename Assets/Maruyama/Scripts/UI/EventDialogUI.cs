using System;
using Maruyama.Events;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Maruyama.UI
{
    /// <summary>
    /// 左:デメリットカード / 右:メリットカード の 2枚を横並び表示し、
    /// 食事選択画面（MealSelectionDialogUI）と同じ OnEnable / OnDisable + isPausedBySelf パターンで
    /// 表示中のゲーム内時間を停止するダイアログ UI。
    ///
    /// ■ 時間停止の方式（MealSelectionDialogUI を参考に実装）
    /// - OnEnable で GameClock.I.PushPause() を呼ぶ（isPausedBySelf フラグで二重呼び出し防止）
    /// - OnDisable で GameClock.I.PopPause() を呼ぶ（isPausedBySelf フラグで対になるときだけ）
    /// - 表示は gameObject.SetActive(true/false) で行い、Unity の OnEnable/OnDisable に委ねる
    /// - MaruyamaPauseManager には依存しない（GameClock.PushPause と体系を統一）
    /// </summary>
    public class EventDialogUI : MonoBehaviour
    {
        // ---- 最前面表示 ----
        // 食事選択（950）・カレンダー（999）・HUD（100）など、他の画面より必ず手前に出すため、
        // ゲーム起動時に、シーン内のイベントダイアログを専用の最前面 Canvas に移す。

        /// <summary>イベントダイアログ専用 Canvas の描画順（他のどの画面より大きい値）。</summary>
        public const int TopCanvasSortingOrder = 1100;
        private const string TopCanvasName = "EventDialogCanvas";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoadedMoveToTop;
            SceneManager.sceneLoaded += OnSceneLoadedMoveToTop;
        }

        private static void OnSceneLoadedMoveToTop(Scene scene, LoadSceneMode mode)
        {
            // 非表示（inactive）のダイアログも対象にするため FindObjectsOfTypeAll を使う
            foreach (var dialog in Resources.FindObjectsOfTypeAll<EventDialogUI>())
            {
                if (dialog == null || !dialog.gameObject.scene.IsValid() || dialog.gameObject.scene != scene) continue;
                dialog.MoveToTopCanvas();
            }
        }

        /// <summary>このダイアログを、最前面の専用 Canvas の下へ移す（すでに移動済みなら何もしない）。</summary>
        public void MoveToTopCanvas()
        {
            var current = GetComponentInParent<Canvas>(true);
            if (current != null && current.gameObject.name == TopCanvasName) return;

            var scene = gameObject.scene;
            var canvasGo = new GameObject(TopCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasGo, scene);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = TopCanvasSortingOrder;

            // 元の Canvas と同じ拡大縮小の設定にそろえる（見た目の大きさが変わらないように）
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            var srcScaler = current != null ? current.rootCanvas.GetComponent<CanvasScaler>() : null;
            if (srcScaler != null)
            {
                scaler.uiScaleMode = srcScaler.uiScaleMode;
                scaler.referenceResolution = srcScaler.referenceResolution;
                scaler.screenMatchMode = srcScaler.screenMatchMode;
                scaler.matchWidthOrHeight = srcScaler.matchWidthOrHeight;
                scaler.referencePixelsPerUnit = srcScaler.referencePixelsPerUnit;
            }
            else
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(800f, 600f);
                scaler.matchWidthOrHeight = 0f;
            }

            transform.SetParent(canvasGo.transform, false);
            var rect = (RectTransform)transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            transform.SetAsLastSibling();
        }

        [Header("カード参照")]
        [SerializeField] private EventCardUI demeritCardUI;
        [SerializeField] private EventCardUI meritCardUI;

        [Header("ルートパネル（廃止・後方互換用）")]
        [Tooltip("旧バージョンとの互換のため残しています。このフィールドは使われません。")]
        [SerializeField] private GameObject dialogPanel;

        private bool demeritCompleted = false;
        private bool meritCompleted = false;
        private bool isOpen = false;

        /// <summary>食事選択と同じ方式: OnEnable で停止したかどうかを追跡するフラグ</summary>
        private bool isPausedBySelf = false;

        /// <summary>ダイアログが閉じたときに呼ぶコールバック（EventManager が isDialogOpen フラグを解除するために使う）</summary>
        private Action onClosedCallback;

        // ---- Unity ライフサイクル（MealSelectionDialogUI と同じパターン）----

        private void OnEnable()
        {
            // 判断画面を開いたときに時間を停止する（Decision 0004 / MealSelectionDialogUI と同方式）
            if (!isPausedBySelf)
            {
                if (GameClock.I != null)
                {
                    GameClock.I.PushPause();
                }
                isPausedBySelf = true;
                Debug.Log("[EventDialogUI] イベント画面を開きました。時間を停止します。");
            }
        }

        private void OnDisable()
        {
            // 判断画面を閉じたときに時間停止を解除する（MealSelectionDialogUI と同方式）
            if (isPausedBySelf)
            {
                if (GameClock.I != null)
                {
                    GameClock.I.PopPause();
                }
                isPausedBySelf = false;
                Debug.Log("[EventDialogUI] イベント画面を閉じました。時間を再開します。");
            }
        }

        // ---- 表示 API ----

        /// <summary>
        /// イベントダイアログを表示する（コールバックなし版・後方互換）。
        /// </summary>
        public void Show(EventData demeritData, EventData meritData)
        {
            Show(demeritData, meritData, null);
        }

        /// <summary>
        /// イベントダイアログを表示する。
        /// gameObject.SetActive(true) → OnEnable → GameClock.PushPause の順で時間を停止する。
        /// </summary>
        /// <param name="demeritData">デメリットカードのデータ（null なら非表示）</param>
        /// <param name="meritData">メリットカードのデータ（null なら非表示）</param>
        /// <param name="onClosed">ダイアログが閉じたときのコールバック</param>
        public void Show(EventData demeritData, EventData meritData, Action onClosed)
        {
            if (isOpen) return;
            isOpen = true;

            onClosedCallback = onClosed;

            demeritCompleted = demeritData == null;
            meritCompleted = meritData == null;

            // MealSelectionDialogUI と同じ: gameObject.SetActive(true) で OnEnable を呼ぶ
            gameObject.SetActive(true);

            // 左カード（デメリット）のバインド
            if (demeritCardUI != null)
            {
                demeritCardUI.Bind(demeritData, () =>
                {
                    demeritCompleted = true;
                    CheckAllCompleted();
                });
            }

            // 右カード（メリット）のバインド
            if (meritCardUI != null)
            {
                meritCardUI.Bind(meritData, () =>
                {
                    meritCompleted = true;
                    CheckAllCompleted();
                });
            }
        }

        private void CheckAllCompleted()
        {
            if (demeritCompleted && meritCompleted)
            {
                Close();
            }
        }

        /// <summary>
        /// ダイアログを閉じる。
        /// gameObject.SetActive(false) → OnDisable → GameClock.PopPause の順で時間を再開する。
        /// </summary>
        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;

            // MealSelectionDialogUI と同じ: gameObject.SetActive(false) で OnDisable を呼ぶ
            gameObject.SetActive(false);

            // 閉じたことを EventManager へ通知
            var callback = onClosedCallback;
            onClosedCallback = null;
            callback?.Invoke();
        }
    }
}

