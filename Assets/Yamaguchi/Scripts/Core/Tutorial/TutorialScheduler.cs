using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 登録したチュートリアルを、それぞれの「出すタイミング」（TutorialData.Timing）で出す。
/// 毎日1回すべてを確かめ、条件を満たしていて、まだ見ていないものを TutorialManager.ShowOnce に渡す。
/// チュートリアルごとにスクリプトを作る必要はない。TutorialCanvas/Triggers に1つだけ置く。
/// 登録は「子育て → チュートリアル → 編集ウィンドウ」から行える（新しく作ると自動で登録される）。
/// </summary>
public class TutorialScheduler : MonoBehaviour
{
    [Tooltip("自動で出すチュートリアル。タイミングが「コードから呼んだときだけ」のものは入れても出ない")]
    [SerializeField] private List<TutorialData> tutorials = new List<TutorialData>();

    [Tooltip("家出の判定（「家出の判定が始まる日」を読む）。未設定なら自動で探す")]
    [SerializeField] private RunawayWatcher runawayWatcher;

    private bool subscribed;

    public IReadOnlyList<TutorialData> Tutorials => tutorials;

    public bool Contains(TutorialData tutorial) => tutorial != null && tutorials.Contains(tutorial);

    /// 家出の判定が始まる日（通算日数）。RunawayWatcher が無ければ -1。
    public int RunawayStartDay
    {
        get
        {
            if (runawayWatcher == null) runawayWatcher = FindFirstObjectByType<RunawayWatcher>(FindObjectsInactive.Include);
            return runawayWatcher != null ? runawayWatcher.ActiveFromDay : -1;
        }
    }

    private void Start()
    {
        if (runawayWatcher == null) runawayWatcher = FindFirstObjectByType<RunawayWatcher>();

        if (DayTicker.I == null)
        {
            Debug.LogError("[TutorialScheduler] DayTicker が見つかりません", this);
            return;
        }
        DayTicker.I.OnDayPassed += HandleDayPassed;
        subscribed = true;
    }

    private void OnDestroy()
    {
        if (subscribed && DayTicker.I != null) DayTicker.I.OnDayPassed -= HandleDayPassed;
    }

    private void HandleDayPassed(int day)
    {
        var manager = TutorialManager.I;
        if (manager == null) return;

        foreach (var t in tutorials)
        {
            if (t == null || manager.HasSeen(t)) continue;
            if (IsDue(t.Timing, day)) manager.ShowOnce(t);
        }
    }

    /// その日にタイミングの条件を満たしているか。
    public bool IsDue(TutorialTiming timing, int day)
    {
        if (timing == null) return false;

        switch (timing.kind)
        {
            case TutorialTimingKind.Manual:
                return false;

            case TutorialTimingKind.GameStart:
                return true;   // 最初に日が進んだときに出る（見たらもう出ない）

            case TutorialTimingKind.AtAge:
                return day >= timing.AgeDays;

            case TutorialTimingKind.RunawayStart:
            {
                int start = RunawayStartDay;
                return start >= 0 && day >= start + timing.offsetDays;
            }

            case TutorialTimingKind.ChildStressAtLeast:
            {
                if (day < timing.AgeDays) return false;
                var child = Household.I != null ? Household.I.Child : null;
                return child != null && child.Stress >= timing.threshold;
            }

            case TutorialTimingKind.MoneyBelow:
                if (day < timing.AgeDays) return false;
                return GameState.I != null && GameState.I.Money < timing.threshold;

            case TutorialTimingKind.FlagSet:
            {
                if (timing.targetFlag == null || string.IsNullOrEmpty(timing.targetFlag.id)) return false;
                if (StoryState.I == null || !StoryState.I.HasFlag(timing.targetFlag)) return false;
                int setDay = StoryState.I.GetFlagSetDay(timing.targetFlag.id);
                if (setDay < 0) return false;
                return day >= setDay + timing.offsetDays;
            }

            default:
                return false;
        }
    }
}
