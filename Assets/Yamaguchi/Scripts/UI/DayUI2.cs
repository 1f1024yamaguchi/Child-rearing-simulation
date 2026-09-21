using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DayUI2 : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI textMeshPro;
    [SerializeField] private Slider slider;

    private int lastDay = -1;   // 日が変わった時だけ更新するためのキャッシュ


    private void OnEnable()
    {
        if(GameState.I != null)
        {
            GameState.I.OnAgeChanged += HandleAgeChanged;
            HandleAgeChanged(GameState.I.AgeDays);
        }
    }

    private void OnDisable()
    {
        if(GameState.I != null)
        {
            GameState.I.OnAgeChanged -= HandleAgeChanged;
        }
    }

    private void HandleAgeChanged(float ageDays)
    {
        int d = Mathf.FloorToInt(ageDays);
        if (d == lastDay) return;    // 同じ日のうちは書き換えない
        lastDay = d;
        textMeshPro.text = GameDate.Format(ageDays);
    }

}
