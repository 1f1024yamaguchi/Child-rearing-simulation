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
    if (choice.insuredMedicalExpense && moneyEffect.value < 0f)
    {
        int copay = Mathf.RoundToInt(Mathf.Abs(moneyEffect.value));

        // 助成状況を確認する（必要なManagerが存在しない場合は0円扱い）
        int covered = (SupportManager.I != null && GameState.I != null)
            ? SupportManager.I.MedicalExpenseCoverage(
                copay,
                Mathf.FloorToInt(GameState.I.AgeDays))
            : 0;

        if (SupportManager.I != null && GameState.I != null)
        {
            if (covered >= copay)
            {
                label += $" (医療費 → 助成申請しているため 0円!)";
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