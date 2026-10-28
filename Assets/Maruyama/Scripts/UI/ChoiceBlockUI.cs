using System;
using System.Collections.Generic;
using Maruyama.Core;
using Maruyama.Events;
using UnityEngine;

namespace Maruyama.UI
{
    /// <summary>
    /// 選択肢1個に対応する UI ブロック。
    /// 選択肢に含まれるステータス変化 Effect からチップを生成してボタンの上に並べ、
    /// 下部に選択肢ボタンを表示する。
    /// </summary>
    public class ChoiceBlockUI : MonoBehaviour
    {
        [SerializeField] private Transform chipsContainer;
        [SerializeField] private EffectChipUI chipPrefab;
        [SerializeField] private EventChoiceButtonUI choiceButton;

        public void Setup(EventChoice choice, Action onChosen)
        {
            Setup(choice, string.Empty, onChosen);
        }

        public void Setup(EventChoice choice, string eventTitle, Action onChosen)
        {
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
                    if (effect == null || effect.kind == EffectKind.Money) continue;

                    var chipInstance = Instantiate(chipPrefab, chipsContainer);
                    chipInstance.SetEffect(effect);
                }
            }

            // ボタンのセットアップ
            if (choiceButton != null)
            {
                choiceButton.Setup(choice, eventTitle, onChosen);
            }
        }
    }
}
