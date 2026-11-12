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
        [Tooltip("選択肢が無効な場合の理由表示用テキスト（未設定時はボタンラベルに小さく付与）")]
        [SerializeField] private TextMeshProUGUI disabledReasonText;

        private EventChoice currentChoice;
        private Action onChosenCallback;
        private string eventTitle = string.Empty;

        public EventChoice CurrentChoice => currentChoice;

        public void Setup(EventChoice choice, Action onChosen)
        {
            Setup(choice, string.Empty, onChosen, true, string.Empty);
        }

        public void Setup(EventChoice choice, string title, Action onChosen)
        {
            Setup(choice, title, onChosen, true, string.Empty);
        }

        public void Setup(EventChoice choice, string title, Action onChosen, bool canChoose, string disabledReason)
        {
            currentChoice = choice;
            onChosenCallback = onChosen;
            eventTitle = title ?? string.Empty;

            if (labelText != null && choice != null)
            {
                // ラベルの {name} を子どもの名前に置き換える（StoryTextFormatter）
                string label = StoryTextFormatter.Format(choice.label);

                // お金効果が含まれている場合は費用表記を自動補完する（手動ラベル優先）
                var moneyEffect = choice.effects?.Find(e => e.kind == EffectKind.Money);
                if (moneyEffect != null && !label.Contains("円"))
                {
                    if (choice.insuredMedicalExpense && moneyEffect.value < 0f)
                    {
                        int copay = Mathf.RoundToInt(Mathf.Abs(moneyEffect.value));

                        if (SupportManager.I != null && GameState.I != null)
                        {
                            // 助成状況を確認する（表示だけ。助成の記録は押したときに EffectApplier が行う）
                            int covered = SupportManager.I.MedicalExpenseCoverage(
                                copay, Mathf.FloorToInt(GameState.I.AgeDays));

                            if (covered >= copay)
                            {
                                label += " (医療費 → 助成申請しているため 0円!)";
                            }
                            else if (covered > 0)
                            {
                                int actualCost = copay - covered;
                                label += $" (医療費 {copay:#,0}円 → 助成で {actualCost:#,0}円（助成 {covered:#,0}円）)";
                            }
                            else
                            {
                                label += $" (医療費 {copay:#,0}円（助成の申請で軽くなります）)";
                            }
                        }
                        else
                        {
                            label += $" (医療費 {copay:#,0}円・助成適用前)";
                        }
                    }
                    else
                    {
                        label += $" ({moneyEffect.value:+#,0;-#,0;0}円)";
                    }
                }

                // 押せない選択肢は理由を小さく出す（EventChoice.showConditions）
                if (!canChoose && !string.IsNullOrEmpty(disabledReason))
                {
                    if (disabledReasonText != null)
                    {
                        disabledReasonText.text = disabledReason;
                        disabledReasonText.gameObject.SetActive(true);
                    }
                    else
                    {
                        label += $"\n<size=70%><color=#888888>（{disabledReason}）</color></size>";
                    }
                }
                else
                {
                    if (disabledReasonText != null)
                    {
                        disabledReasonText.gameObject.SetActive(false);
                    }
                }

                labelText.text = label;
            }

            if (button != null)
            {
                button.interactable = canChoose;
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
                EffectApplier.Apply(
                    currentChoice.effects,
                    new LedgerSource(ledgerName, currentChoice.ledgerCategory),
                    currentChoice.insuredMedicalExpense);
                Debug.Log($"[EventChoiceButtonUI] 選択肢 '{currentChoice.label}' の Effect 適用完了。");
            }

            onChosenCallback?.Invoke();
        }
    }
}
