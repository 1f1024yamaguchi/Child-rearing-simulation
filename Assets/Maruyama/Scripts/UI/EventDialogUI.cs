using Maruyama.Core;
using Maruyama.Events;
using UnityEngine;

namespace Maruyama.UI
{
    /// <summary>
    /// 左:デメリットカード / 右:メリットカード の 2枚を横並び表示し、
    /// MaruyamaPauseManager により表示中のゲーム内時間停止を制御するダイアログ UI。
    /// </summary>
    public class EventDialogUI : MonoBehaviour
    {
        [Header("カード参照")]
        [SerializeField] private EventCardUI demeritCardUI;
        [SerializeField] private EventCardUI meritCardUI;

        [Header("ルートパネル")]
        [SerializeField] private GameObject dialogPanel;

        private bool demeritCompleted = false;
        private bool meritCompleted = false;
        private bool isOpen = false;

        public void Show(EventData demeritData, EventData meritData)
        {
            if (isOpen) return;
            isOpen = true;

            demeritCompleted = demeritData == null;
            meritCompleted = meritData == null;

            if (dialogPanel != null) dialogPanel.SetActive(true);

            // 時間停止を要求
            if (MaruyamaPauseManager.Instance != null)
            {
                MaruyamaPauseManager.Instance.PushPause();
            }

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

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;

            if (dialogPanel != null) dialogPanel.SetActive(false);

            // 時間停止要求を解除
            if (MaruyamaPauseManager.Instance != null)
            {
                MaruyamaPauseManager.Instance.PopPause();
            }

            Debug.Log("[EventDialogUI] イベント画面を閉じました。時間が再開されます。");
        }
    }
}
