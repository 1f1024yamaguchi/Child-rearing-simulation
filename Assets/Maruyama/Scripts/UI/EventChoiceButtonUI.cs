using System;
using Maruyama.Core;
using Maruyama.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Maruyama.UI
{
    /// <summary>
    /// 選択肢ボタン単体を制御する UI コンポーネント。
    /// クリック時に choice.effects を 1 回適用し、親コンポーネントへ選択完了を通知する。
    /// </summary>
    public class EventChoiceButtonUI : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI labelText;

        private EventChoice currentChoice;
        private Action onChosenCallback;
        private string eventTitle = string.Empty;

        public void Setup(EventChoice choice, Action onChosen)
        {
            Setup(choice, string.Empty, onChosen);
        }

        public void Setup(EventChoice choice, string title, Action onChosen)
        {
            currentChoice = choice;
            onChosenCallback = onChosen;
            eventTitle = title ?? string.Empty;

            if (labelText != null && choice != null)
            {
                // お金効果が含まれている場合は費用表記を自動補完する（手動ラベル優先）
                string label = choice.label;
                var moneyEffect = choice.effects?.Find(e => e.kind == EffectKind.Money);
                if (moneyEffect != null && !label.Contains("円"))
                {
                    label += $" ({moneyEffect.value:+#,0;-#,0;0}円)";
                }
                labelText.text = label;
            }

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnButtonClicked);
            }
        }

        private void OnButtonClicked()
        {
            if (button != null)
            {
                button.interactable = false; // 連打防止
            }

            if (currentChoice != null)
            {
                Debug.Log($"[EventChoiceButtonUI] 選択肢 '{currentChoice.label}' がクリックされました。Effect を適用します。");
                string ledgerName = string.IsNullOrEmpty(eventTitle) ? currentChoice.label : $"{eventTitle}：{currentChoice.label}";
                EffectApplier.Apply(currentChoice.effects, new LedgerSource(ledgerName, currentChoice.ledgerCategory));
                Debug.Log($"[EventChoiceButtonUI] 選択肢 '{currentChoice.label}' の Effect 適用完了。");
            }

            onChosenCallback?.Invoke();
        }
    }
}
