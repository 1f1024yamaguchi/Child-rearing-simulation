using TMPro;
using UnityEngine;

/// <summary>
/// 成長ゲージ（Canvas/Slider）に文字を足す。
/// ゲージの割合 = いまの日数 ÷ ゴールの日数（GameState.GoalAgeDays）。DayUI と同じ式。
/// ・右端：ゴールの名前と年月（例「高校卒業 18歳4月」）。進路が決まって延びると自動で変わる。
/// ・中央：いまの割合（例「33%」）。
/// </summary>
public class GrowthGaugeLabelUI : MonoBehaviour
{
    [SerializeField] private TMP_Text goalText;      // 右端
    [SerializeField] private TMP_Text percentText;   // 中央

    private void Start()
    {
        if (GameState.I == null) return;
        GameState.I.OnAgeChanged += OnAge;
        GameState.I.OnGoalChanged += OnGoal;
        Refresh();
    }

    private void OnDestroy()
    {
        if (GameState.I == null) return;
        GameState.I.OnAgeChanged -= OnAge;
        GameState.I.OnGoalChanged -= OnGoal;
    }

    private void OnAge(float _) => Refresh();
    private void OnGoal(int _, string __) => Refresh();

    private void Refresh()
    {
        var gs = GameState.I;
        if (gs == null) return;
        int goal = gs.GoalAgeDays > 0 ? gs.GoalAgeDays : 7300;
        GameDate.FromAgeDays(goal, out int y, out int m, out _);
        if (goalText != null)
            goalText.text = string.IsNullOrEmpty(gs.GoalLabel) ? $"{y}歳{m}月" : $"{gs.GoalLabel} {y}歳{m}月";
        if (percentText != null)
            percentText.text = $"{Mathf.RoundToInt(Mathf.Clamp01(gs.AgeDays / goal) * 100f)}%";
    }
}
