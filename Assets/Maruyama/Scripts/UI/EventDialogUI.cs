using System;
using Maruyama.Events;
using UnityEngine;

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

