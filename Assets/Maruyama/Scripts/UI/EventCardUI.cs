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

        [Header("開発用プレースホルダー")]
        [Tooltip("画像未設定かつ指示メモがある場合に、ゲーム内で差し替え枠を表示するか")]
        [SerializeField] private bool showPlaceholderInGame = false;
        [Tooltip("画像未設定時に表示するプレースホルダー枠（灰色の板）")]
        [SerializeField] private GameObject placeholder;
        [Tooltip("プレースホルダー枠内に表示する絵の指示メモ")]
        [SerializeField] private TextMeshProUGUI placeholderNote;

        [Header("選択肢コンテナ")]
        [Tooltip("縦1列用のコンテナ（1〜3個の選択肢で使用。VerticalLayoutGroup）")]
        [SerializeField] private Transform choicesContainer;
        [Tooltip("2×2（2列）用のコンテナ（4個の選択肢で使用。GridLayoutGroup）")]
        [SerializeField] private Transform gridChoicesContainer;
        [SerializeField] private ChoiceBlockUI choiceBlockPrefab;

        private EventData boundData;
        private Action onCardCompletedCallback;
        private bool isCompleted = false;

        public bool IsCompleted => isCompleted;
        public EventData BoundData => boundData;

        public void Bind(EventData data, Action onCompleted)
        {   
            isCompleted = false;
            onCardCompletedCallback = onCompleted;
            boundData = data;

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

            // タイトル & 説明（{name} を子どもの名前に置換）
            if (titleText != null) titleText.text = StoryTextFormatter.Format(data.title);
            if (descriptionText != null) descriptionText.text = StoryTextFormatter.Format(data.description);

            // アイコン / プレースホルダー（画像が無いときは非アクティブ化して VerticalLayoutGroup で詰める）
            bool hasIcon = data.iconSprite != null;
            if (hasIcon)
            {
                if (iconImage != null)
                {
                    iconImage.sprite = data.iconSprite;
                    iconImage.gameObject.SetActive(true);
                }
                if (placeholder != null) placeholder.SetActive(false);
            }
            else
            {
                if (iconImage != null) iconImage.gameObject.SetActive(false);

                bool showPh = showPlaceholderInGame && !string.IsNullOrEmpty(data.imageNote) && placeholder != null;
                if (placeholder != null)
                {
                    placeholder.SetActive(showPh);
                    if (showPh && placeholderNote != null)
                    {
                        placeholderNote.text = data.imageNote;
                    }
                }
            }

            // 選択肢ブロックの動的生成
            BindChoices(data);
        }

        private void BindChoices(EventData data)
        {
            // 両コンテナの既存子要素をクリア
            ClearContainer(choicesContainer);
            ClearContainer(gridChoicesContainer);

            if (data.choices == null || choiceBlockPrefab == null) return;

            int totalChoices = data.choices.Count;
            if (totalChoices >= 5)
            {
                Debug.LogWarning($"[EventCardUI] イベント「{data.title}」の選択肢が{totalChoices}個あります。先頭4つのみ表示します。");
            }

            int displayCount = Mathf.Min(totalChoices, 4);
            if (displayCount == 0)
            {
                if (choicesContainer != null) choicesContainer.gameObject.SetActive(false);
                if (gridChoicesContainer != null) gridChoicesContainer.gameObject.SetActive(false);
                return;
            }

            // 4個のときは 2×2 用（gridChoicesContainer がなければ choicesContainer にフォールバック）
            // 1〜3個のときは縦1列用
            bool useGrid = (displayCount == 4) && (gridChoicesContainer != null);
            Transform activeContainer = useGrid ? gridChoicesContainer : choicesContainer;
            Transform inactiveContainer = useGrid ? choicesContainer : gridChoicesContainer;

            if (activeContainer != null) activeContainer.gameObject.SetActive(true);
            if (inactiveContainer != null) inactiveContainer.gameObject.SetActive(false);

            if (activeContainer != null)
            {
                // 各選択肢の条件をチェック
                bool[] available = new bool[displayCount];
                string[] reasons = new string[displayCount];
                int availableCount = 0;

                for (int i = 0; i < displayCount; i++)
                {
                    var choice = data.choices[i];
                    if (choice != null)
                    {
                        available[i] = choice.IsAvailable(out reasons[i]);
                        if (available[i]) availableCount++;
                    }
                    else
                    {
                        available[i] = false;
                        reasons[i] = "無効な選択肢";
                    }
                }

                // すべて押せない場合は、ゲーム進行が停止しないよう最初の選択肢を強制有効化する
                if (availableCount == 0 && displayCount > 0)
                {
                    Debug.LogWarning($"[EventCardUI] イベント「{data.title}」のすべての選択肢が条件を満たしていません。ゲーム進行を優先し、最初の選択肢「{data.choices[0].label}」を強制的に有効化します。");
                    available[0] = true;
                    reasons[0] = string.Empty;
                }

                for (int i = 0; i < displayCount; i++)
                {
                    var choice = data.choices[i];
                    if (choice == null) continue;

                    int index = i;
                    var blockInstance = Instantiate(choiceBlockPrefab, activeContainer);
                    blockInstance.Setup(choice, data.title, displayCount, () => HandleChoiceSelected(choice, index), available[i], reasons[i]);
                }
            }
        }

        private void ClearContainer(Transform container)
        {
            if (container == null) return;
            foreach (Transform child in container)
            {
                Destroy(child.gameObject);
            }
        }

        private void HandleChoiceSelected(EventChoice choice, int index)
        {
            if (isCompleted) return;
            isCompleted = true;

            // カード内のすべての選択肢ボタンを非活性化（選択済み状態にする）
            DisableButtonsIn(choicesContainer);
            DisableButtonsIn(gridChoicesContainer);

            Debug.Log($"[EventCardUI] カード '{titleText?.text}' の選択が完了しました。");

            // 完了通知の前に呼ぶ
            EventManager.NotifyResolved(boundData, choice, index);

            // 親ダイアログへ完了を通知
            onCardCompletedCallback?.Invoke();
        }

        private void DisableButtonsIn(Transform container)
        {
            if (container == null) return;
            var buttons = container.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                if (btn != null) btn.interactable = false;
            }
        }
    }
}
