using UnityEngine;

public enum Season { Spring, Summer, Autumn, Winter }

/// 家の外の景色を、ゲーム内の月に合わせて季節ごとに切り替える。
/// 季節ごとの景色は子オブジェクト（春・夏・秋・冬）として用意しておき、今の季節だけを表示する。
/// 3〜5月=春 / 6〜8月=夏 / 9〜11月=秋 / 12〜2月=冬
public class SeasonBackdrop : MonoBehaviour
{
    [Tooltip("春の景色（3〜5月）")]
    [SerializeField] private GameObject spring;
    [Tooltip("夏の景色（6〜8月）")]
    [SerializeField] private GameObject summer;
    [Tooltip("秋の景色（9〜11月）")]
    [SerializeField] private GameObject autumn;
    [Tooltip("冬の景色（12〜2月）")]
    [SerializeField] private GameObject winter;

    private Season? current;
    private bool subscribed;

    public Season? Current => current;

    public static Season FromMonth(int month)
    {
        if (month >= 3 && month <= 5) return Season.Spring;
        if (month >= 6 && month <= 8) return Season.Summer;
        if (month >= 9 && month <= 11) return Season.Autumn;
        return Season.Winter;
    }

    // GameState.I は Awake で入るので Start で購読する。
    // 月の変わり目は MonthTicker でも分かるが、ロード時は通知が出ないので、年齢（日数）の変化から毎回判定する
    private void Start()
    {
        if (GameState.I != null)
        {
            GameState.I.OnAgeChanged += HandleAgeChanged;
            subscribed = true;
            HandleAgeChanged(GameState.I.AgeDays);
        }
        else
        {
            Show(Season.Spring);
        }
    }

    private void OnDestroy()
    {
        if (subscribed && GameState.I != null) GameState.I.OnAgeChanged -= HandleAgeChanged;
    }

    private void HandleAgeChanged(float ageDays)
    {
        GameDate.FromAgeDays(ageDays, out _, out int month, out _);
        Show(FromMonth(month));
    }

    /// 指定した季節の景色だけを表示する（同じ季節なら何もしない）
    public void Show(Season season)
    {
        if (current == season) return;
        current = season;
        if (spring != null) spring.SetActive(season == Season.Spring);
        if (summer != null) summer.SetActive(season == Season.Summer);
        if (autumn != null) autumn.SetActive(season == Season.Autumn);
        if (winter != null) winter.SetActive(season == Season.Winter);
    }

    [ContextMenu("テスト：次の季節にする")]
    private void ShowNextForTest()
    {
        var next = current.HasValue ? (Season)(((int)current.Value + 1) % 4) : Season.Spring;
        current = null;
        Show(next);
    }
}
