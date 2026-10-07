using System.Collections.Generic;
using Maruyama.Core;
using UnityEngine;

/// <summary>
/// 支援制度のデータ（制度1つ ＝ アセット1つ）。support-systems.md 3.5 ＋ support-apply.md 5.1。
/// 申請済み／受給中といった「今の状態」はここに書かない（SupportSchemeRuntime に id をキーにして持つ）。
/// ※ covers（支出の肩代わり）はステップ6で足す。
/// </summary>
[CreateAssetMenu(menuName = "子育て/支援制度")]
public class SupportScheme : ScriptableObject
{
    [Header("表示")]
    public string id;                        // 例 "child_allowance"（セーブ用。後から変えない）
    public string title;                     // 例 "児童手当"
    [TextArea] public string description;    // 例 "高校生年代まで毎月もらえる"
    public Sprite icon;
    public SupportCategory category;         // 政府 / 地方自治体 / 会社 / アプリ（タブ用）
    public List<string> merits = new List<string>();     // 緑のチップ
    public List<string> demerits = new List<string>();   // 赤のチップ

    [Header("効果")]
    public EffectTiming effectTiming = EffectTiming.OnApply;   // 申請したとき1回／受給中は毎月
    [Tooltip("OnApply：申請したとき1回。Monthly：申請したとき1回だけ起きること（手間のストレスなど。空でよい）")]
    public List<Effect> effects = new List<Effect>();

    [Header("申請条件")]
    [Tooltip("全部満たしたら申請できる（空＝条件なし）")]
    public List<RequirementSO> applyRequirements = new List<RequirementSO>();

    [Header("毎月の支給（Monthly のとき）")]
    public List<PaymentTier> monthlyTiers = new List<PaymentTier>();   // 3.2 の支給表

    [Header("申請できる期間")]
    public GameDay      start    = new GameDay { year = 0, month = 1, day = 1 };
    public DeadlineType deadlineType = DeadlineType.None;
    public GameDay      deadline = new GameDay { year = 0, month = 1, day = 1 };  // Once のとき
    public MonthDay     yearly   = new MonthDay { month = 8, day = 31 };          // Yearly のとき
    [Min(1)] public int lapseYears = 2;   // Yearly：期限から何年手続きしないと受給資格を失うか

    [Header("毎年の手続き（Yearly のとき）")]
    [Min(1)] public int renewWindowDays = 31;   // 期限日の何日前から［更新する］を押せるか

    public int StartAgeDays    => start.ToAgeDays();
    public int DeadlineAgeDays => deadline.ToAgeDays();

    // 入力ミスを自動で直す（2月30日 → 2月28日 など）
    private void OnValidate()
    {
        start    = Fix(start);
        deadline = Fix(deadline);
        yearly.month = Mathf.Clamp(yearly.month, 1, 12);
        yearly.day   = Mathf.Clamp(yearly.day, 1, GameDate.DaysInMonth(yearly.month));

        if (monthlyTiers != null)
        {
            for (int i = 0; i < monthlyTiers.Count; i++)
            {
                if (monthlyTiers[i] == null) continue;
                monthlyTiers[i].fromAge = Fix(monthlyTiers[i].fromAge);
                monthlyTiers[i].toAge   = Fix(monthlyTiers[i].toAge);
            }
        }
    }

    private static GameDay Fix(GameDay d)
    {
        d.year  = Mathf.Clamp(d.year, 0, 19);
        d.month = Mathf.Clamp(d.month, 1, 12);
        d.day   = Mathf.Clamp(d.day, 1, GameDate.DaysInMonth(d.month));
        return d;
    }
}
