using System;
using Maruyama.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Maruyama.UI
{
    /// <summary>
    /// イベントカード1枚ぶん（左:デメリット / 右:メリット）の表示・UI制御を行うコンポーネント。
    /// </summary>
    public class EventCardUI : MonoBehaviour
    {
        [Header("ヘッダー・ラベル")]
        [SerializeField] private Image headerBgImage;
        [SerializeField] private TextMeshProUGUI categoryBadgeText;
        [SerializeField] private Color demeritHeaderColor = new Color(1.0f, 0.9f, 0.9f);
        [SerializeField] private Color meritHeaderColor = new Color(0.9f, 1.0f, 0.9f);

        [Header("メイン情報")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;

        [Header("選択肢コンテナ")]
        [SerializeField] private Transform choicesContainer;
        [SerializeField] private ChoiceBlockUI choiceBlockPrefab;

        private Action onCardCompletedCallback;

        public void Bind(EventData data, Action onCompleted)
        {   
            isCompleted = false;
            onCardCompletedCallback = onCompleted;

            if (data == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            // カテゴリ表示
            bool isDemerit = data.category == EventCategory.Demerit;
            if (categoryBadgeText != null)
            {
                categoryBadgeText.text = isDemerit ? "[デメリット]" : "[メリット]";
            }
            if (headerBgImage != null)
            {
                headerBgImage.color = isDemerit ? demeritHeaderColor : meritHeaderColor;
            }

            // タイトル & 説明
            if (titleText != null) titleText.text = data.title;
            if (descriptionText != null) descriptionText.text = data.description;

            // アイコン
            if (iconImage != null)
            {
                if (data.iconSprite != null)
                {
                    iconImage.sprite = data.iconSprite;
                    iconImage.gameObject.SetActive(true);
                }
                else
                {
                    iconImage.gameObject.SetActive(false);
                }
            }

            // 選択肢ブロックの動的生成
            if (choicesContainer != null)
            {
                foreach (Transform child in choicesContainer)
                {
                    Destroy(child.gameObject);
                }

                if (data.choices != null && choiceBlockPrefab != null)
                {
                    foreach (var choice in data.choices)
                    {
                        if (choice == null) continue;

                        var blockInstance = Instantiate(choiceBlockPrefab, choicesContainer);
                        blockInstance.Setup(choice, data.title, HandleChoiceSelected);
                    }
                }
            }
        }

        private bool isCompleted = false;

        public bool IsCompleted => isCompleted;

        private void HandleChoiceSelected()
        {
            if (isCompleted) return;
            isCompleted = true;

            // カード内のすべての選択肢ボタンを非活性化（選択済み状態にする）
            if (choicesContainer != null)
            {
                var buttons = choicesContainer.GetComponentsInChildren<Button>(true);
                foreach (var btn in buttons)
                {
                    if (btn != null) btn.interactable = false;
                }
            }

            Debug.Log($"[EventCardUI] カード '{titleText?.text}' の選択が完了しました。");

            // 親ダイアログへ完了を通知
            onCardCompletedCallback?.Invoke();
        }
    }
}
