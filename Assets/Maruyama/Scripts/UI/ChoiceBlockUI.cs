using System;
using System.Collections.Generic;
using Maruyama.Core;
using Maruyama.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Maruyama.UI
{
    /// <summary>
    /// 選択肢1個に対応する UI ブロック。
    /// 選択肢に含まれるステータス変化 Effect からチップを生成してボタンの上に並べ、
    /// 下部に選択肢ボタンを表示する。
    /// </summary>
    public class ChoiceBlockUI : MonoBehaviour
    {
        [Header("選択肢数ごとの高さ設定")]
        [Tooltip("選択肢が1〜2個のときの高さ")]
        [SerializeField] private float heightFor1To2 = 90f;
        [Tooltip("選択肢が3個のときの高さ")]
        [SerializeField] private float heightFor3 = 72f;
        [Tooltip("選択肢が4個（2×2）のときの高さ（たたき台）")]
        [SerializeField] private float heightFor4 = 90f;

        [Header("コンポーネント参照")]
        [Tooltip("高さを制御する LayoutElement（未設定なら自身から取得）")]
        [SerializeField] private LayoutElement layoutElement;

        [Header("UI参照")]
        [SerializeField] private Transform chipsContainer;
        [SerializeField] private EffectChipUI chipPrefab;
        [SerializeField] private EventChoiceButtonUI choiceButton;

        public void Setup(EventChoice choice, Action onChosen)
        {
            Setup(choice, string.Empty, 2, onChosen, true, string.Empty);
        }

        public void Setup(EventChoice choice, string eventTitle, Action onChosen)
        {
            Setup(choice, eventTitle, 2, onChosen, true, string.Empty);
        }

        public void Setup(EventChoice choice, string eventTitle, int totalChoices, Action onChosen)
        {
            Setup(choice, eventTitle, totalChoices, onChosen, true, string.Empty);
        }

        public void Setup(EventChoice choice, string eventTitle, int totalChoices, Action onChosen, bool canChoose, string disabledReason)
        {
            ApplyHeight(totalChoices);

            // 既存のチップをクリア
            if (chipsContainer != null)
            {
                foreach (Transform child in chipsContainer)
                {
                    Destroy(child.gameObject);
                }
            }

            // Money 以外のステータス変化 Effect（体調、子ストレス、親ストレス等）からチップを動的生成
            if (choice != null && choice.effects != null && chipPrefab != null && chipsContainer != null)
            {
                foreach (var effect in choice.effects)
                {
                    if (effect == null) continue;

                    // お金・ストーリー系（フラグ・予約・ストーリー表示）はチップを出さない
                    if (effect.kind == EffectKind.Money ||
                        effect.kind == EffectKind.SetFlag ||
                        effect.kind == EffectKind.ClearFlag ||
                        effect.kind == EffectKind.ScheduleEvent ||
                        effect.kind == EffectKind.ShowStory)
                    {
                        continue;
                    }

                    var chipInstance = Instantiate(chipPrefab, chipsContainer);

                    if (effect.kind == EffectKind.SetHobby)
                    {
                        var hobby = (HobbyKind)Mathf.RoundToInt(effect.value);
                        string hobbyName = HobbyDisplayName.Get(hobby);
                        SetupCustomChip(chipInstance, $"趣味：{hobbyName}", false);
                    }
                    else if (effect.kind == EffectKind.AddTrait)
                    {
                        var traitDef = effect.asset as TraitDefinition;
                        string traitName = traitDef != null ? traitDef.displayName : "特性";
                        SetupCustomChip(chipInstance, $"特性：{traitName}", false);
                    }
                    else
                    {
                        chipInstance.SetEffect(effect);
                    }
                }
            }

            // ボタンのセットアップ
            if (choiceButton != null)
            {
                choiceButton.Setup(choice, eventTitle, onChosen, canChoose, disabledReason);
            }
        }

        /// <summary>選択肢の総数に応じて高さを preferredHeight に設定する。</summary>
        public void ApplyHeight(int totalChoices)
        {
            if (layoutElement == null) layoutElement = GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                float h;
                if (totalChoices <= 2) h = heightFor1To2;
                else if (totalChoices == 3) h = heightFor3;
                else h = heightFor4;

                layoutElement.preferredHeight = h;
            }
        }

        private void SetupCustomChip(EffectChipUI chip, string text, bool isDemerit)
        {
            if (chip == null) return;
            var label = chip.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (label != null)
            {
                label.text = text;
                label.color = isDemerit ? new Color(0.7f, 0.1f, 0.1f) : new Color(0.1f, 0.5f, 0.1f);
            }

            var image = chip.GetComponentInChildren<Image>();
            if (image != null)
            {
                image.color = isDemerit ? new Color(1.0f, 0.85f, 0.85f) : new Color(0.85f, 0.95f, 0.85f);
            }
        }
    }
}
