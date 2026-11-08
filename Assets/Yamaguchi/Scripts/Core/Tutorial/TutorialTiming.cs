using System;
using UnityEngine;

/// <summary>
/// チュートリアルを「いつ出すか」の種類。
/// 種類を足すときは、ここに1つ足し、TutorialScheduler.IsDue と TutorialTiming.Describe に1行ずつ足す
/// （編集ウィンドウの入力欄は TutorialEditorWindow.DrawTiming）。数字は後から変えない（アセットに保存されるため）。
/// </summary>
public enum TutorialTimingKind
{
    [InspectorName("コードから呼んだときだけ（自動では出さない）")] Manual = 0,
    [InspectorName("ゲームを始めてすぐ")] GameStart = 1,
    [InspectorName("決まった年齢の日")] AtAge = 2,
    [InspectorName("家出の判定が始まる日（ずらせる）")] RunawayStart = 3,
    [InspectorName("子どものストレスが ○ 以上になったら")] ChildStressAtLeast = 4,
    [InspectorName("お金が ○ 円より少なくなったら")] MoneyBelow = 5,
}

/// <summary>
/// チュートリアルを出すタイミング（TutorialData が1つ持つ）。
/// 判定は TutorialScheduler が毎日1回行い、条件を満たした最初の日に1回だけ出す（TutorialManager.ShowOnce）。
/// 条件の日を過ぎたセーブをロードした場合も、まだ見ていなければ次の日に出る。
/// </summary>
[Serializable]
public class TutorialTiming
{
    [Tooltip("いつ出すか")]
    public TutorialTimingKind kind = TutorialTimingKind.Manual;

    [Tooltip("「決まった年齢の日」はその日。ストレス・お金のときは「この日より前は出さない」（0歳1月1日なら制限なし）")]
    [Min(0)] public int ageYears = 0;
    [Range(1, 12)] public int ageMonth = 1;
    [Range(1, 31)] public int ageDay = 1;

    [Tooltip("「家出の判定が始まる日」からずらす日数。-30 なら30日前（予告）、+10 なら10日後")]
    public int offsetDays = 0;

    [Tooltip("ストレスのときは 0〜100、お金のときは円")]
    public int threshold = 0;

    /// 年齢の欄を通算日数にしたもの。
    public int AgeDays => GameDate.ToAgeDays(ageYears, ageMonth, ageDay);

    /// 年齢の欄が 0歳1月1日（制限なし）か。
    public bool HasNoAgeLimit => AgeDays <= 0;

    /// 人が読む説明（編集ウィンドウの一覧に出す）。runawayStartDay を渡すと、家出の日を具体的な日付で書く。
    public string Describe(int runawayStartDay = -1)
    {
        switch (kind)
        {
            case TutorialTimingKind.Manual:
                return "自動では出さない（コードから TutorialManager.I.ShowOnce を呼んだとき）";
            case TutorialTimingKind.GameStart:
                return "ゲームを始めてすぐ（最初の1日目）";
            case TutorialTimingKind.AtAge:
                return $"{ageYears}歳{ageMonth}月{ageDay}日";
            case TutorialTimingKind.RunawayStart:
            {
                string shift = offsetDays < 0 ? $"の{-offsetDays}日前" : offsetDays > 0 ? $"の{offsetDays}日後" : "";
                string when = runawayStartDay >= 0 ? $"（{GameDate.Format(runawayStartDay + offsetDays)}）" : "";
                return $"家出の判定が始まる日{shift}{when}";
            }
            case TutorialTimingKind.ChildStressAtLeast:
                return $"子どものストレスが {threshold} 以上になったとき{AgeLimitText()}";
            case TutorialTimingKind.MoneyBelow:
                return $"お金が {threshold:N0} 円より少なくなったとき{AgeLimitText()}";
            default:
                return kind.ToString();
        }
    }

    private string AgeLimitText() => HasNoAgeLimit ? "" : $"（{ageYears}歳{ageMonth}月{ageDay}日から）";
}
