using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Button に付けておき、クリックされたときに AudioManager.I.PlaySe を呼ぶ。
/// Button が interactable でないときは鳴らさない。
/// </summary>
[DisallowMultipleComponent]
public class ButtonClickSE : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("個別のクリック音を使いたい場合に指定（空なら AudioManager の defaultClickClip を使用）。")]
    [SerializeField] private AudioClip overrideClip;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;

        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button == null || !button.interactable) return;
        if (AudioManager.I == null) return;

        AudioClip clip = overrideClip != null ? overrideClip : AudioManager.I.DefaultClickClip;
        AudioManager.I.PlaySe(clip);
    }
}

