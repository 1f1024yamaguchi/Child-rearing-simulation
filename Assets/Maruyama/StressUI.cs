using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class StressUI : MonoBehaviour
{
    private Slider slider;

    private void Awake()
    {
        slider = GetComponent<Slider>();
    }

    private void Start()
    {
        if (GameState.I == null) return;

        // Stress（0～100）をSlider（0～1）に変換
        slider.SetValueWithoutNotify(GameState.I.Stress / 100f);

        GameState.I.OnStressChanged += UpdateStressUI;
    }

    private void OnDestroy()
    {
        if (GameState.I != null)
        {
            GameState.I.OnStressChanged -= UpdateStressUI;
        }
    }

    private void UpdateStressUI(int stress)
    {
        // Stress（0～100）をSlider（0～1）に変換
        slider.SetValueWithoutNotify(stress / 100f);
    }
}