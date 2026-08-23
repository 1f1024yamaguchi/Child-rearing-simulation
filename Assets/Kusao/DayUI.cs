using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DayUI : MonoBehaviour
{
    private TextMeshProUGUI textMeshPro;
    private Text text;
    private Slider slider;

    private void Awake()
    {
        textMeshPro = GetComponent<TextMeshProUGUI>();
        text = GetComponent<Text>();
        slider = GetComponent<Slider>();
    }

    private void Start()
    {
        if (GameState.I == null) return;

        UpdateDayUI(GameState.I.AgeDays);

        GameState.I.OnAgeChanged += UpdateDayUI;
    }

    private void OnDestroy()
    {
        if (GameState.I != null)
        {
            GameState.I.OnAgeChanged -= UpdateDayUI;
        }
    }

    private void UpdateDayUI(float ageDays)
    {
        int day = Mathf.FloorToInt(ageDays);
        string dayText = $"Day: {day}";

        if (textMeshPro != null)
        {
            textMeshPro.text = dayText;
        }

        if (text != null)
        {
            text.text = dayText;
        }

        if (slider != null)
        {
            float maxDays = GameState.I != null && GameState.I.GoalAgeDays > 0 
                ? GameState.I.GoalAgeDays 
                : 7300f;
            slider.SetValueWithoutNotify(ageDays / maxDays);
        }
    }
}
