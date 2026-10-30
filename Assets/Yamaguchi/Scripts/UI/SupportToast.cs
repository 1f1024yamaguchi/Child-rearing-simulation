using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 画面上部に出す短い通知（support-apply.md 7.4・7.5、ステップ3後半）。
/// - 申請した直後：OnApply「○○を受け取りました　お金 +500,000」／Monthly「○○を申請しました　来月から支給または積立」
/// - 期限切れ（SupportManager.OnExpired）：「○○の申請期限が過ぎました」
/// - 毎月の支給（OnPaid）では出さない（10章・2026-10-07 決定）
/// 重なったときは順番に出す。サポート画面を閉じていても出るよう Canvas の直下に置く。
/// 時間が止まっていても消えるよう、待ち時間は実時間（unscaled）で数える。
/// </summary>
public class SupportToast : MonoBehaviour
{
    [Header("表示")]
    [SerializeField] private CanvasGroup group;      // 通知の見た目全体（フェード用）。このオブジェクトの子に置く
    [SerializeField] private TMP_Text label;

    [Header("時間（秒）")]
    [SerializeField, Min(0.1f)] private float showSeconds = 2f;
    [SerializeField, Min(0f)] private float fadeSeconds = 0.25f;

    private readonly Queue<string> queue = new Queue<string>();
    private readonly Dictionary<string, SupportState> lastStates = new Dictionary<string, SupportState>();
    private Coroutine runner;
    private bool subscribed;

    // ------------------------------------------------------------
    // ライフサイクル
    // ------------------------------------------------------------

    private void Awake()
    {
        SetAlpha(0f);
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void Start()
    {
        Subscribe();   // OnEnable の時点で SupportManager が無かったとき用
        transform.SetAsLastSibling();   // 他の画面より手前に出す
    }

    private void OnDisable()
    {
        Unsubscribe();
        runner = null;   // コルーチンは無効化で止まる
        SetAlpha(0f);
    }

    private void Subscribe()
    {
        if (subscribed || SupportManager.I == null) return;

        // 「申請した」を状態の変わり方から見分けるため、今の状態を覚えておく
        lastStates.Clear();
        foreach (var s in SupportManager.I.Schemes)
        {
            if (s != null && !string.IsNullOrEmpty(s.id)) lastStates[s.id] = SupportManager.I.GetState(s);
        }

        SupportManager.I.OnStateChanged += HandleStateChanged;
        SupportManager.I.OnExpired += HandleExpired;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (SupportManager.I != null)
        {
            SupportManager.I.OnStateChanged -= HandleStateChanged;
            SupportManager.I.OnExpired -= HandleExpired;
        }
        subscribed = false;
    }

    // ------------------------------------------------------------
    // 公開窓口
    // ------------------------------------------------------------

    /// <summary>通知を1つ足す。表示中なら順番待ちになる。</summary>
    public void Show(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        queue.Enqueue(message);
        if (runner == null && isActiveAndEnabled) runner = StartCoroutine(Run());
    }

    // ------------------------------------------------------------
    // イベント
    // ------------------------------------------------------------

    private void HandleStateChanged(SupportScheme s, SupportState state)
    {
        if (s == null || string.IsNullOrEmpty(s.id)) return;

        lastStates.TryGetValue(s.id, out var prev);
        bool known = lastStates.ContainsKey(s.id);
        lastStates[s.id] = state;
        if (!known || prev != SupportState.Available) return;

        // 「申請できる」から変わるのは申請したときだけ（Apply の結果）。
        // ※ セーブの読み込み（Restore）でも状態が変わって見えることがあるので、
        //   申請した日が今日のときだけ通知する。
        var mgr = SupportManager.I;
        if (mgr == null) return;
        if (mgr.GetRuntime(s).appliedAgeDays != Mathf.FloorToInt(GameState.I != null ? GameState.I.AgeDays : 0f)) return;

        if (state == SupportState.Received)
        {
            string money = FormatMoney(MoneyOfEffects(s));
            Show($"{s.title}を受け取りました" + (money != "" ? $"　お金 {money}" : ""));
        }
        else if (state == SupportState.Active)
        {
            string money = FormatMoney(MonthlyMoney(mgr, s));
            if (s.payoutMonths != null && s.payoutMonths.Count > 0)
            {
                string months = PayoutMonths(s);
                string payoutPeriod = months.Length > 0 ? $"{months}月" : "指定月";
                Show($"{s.title}を申請しました" +
                    (money != ""
                        ? $"　来月から月{money}円分を積み立て、{payoutPeriod}にまとめて支給"
                        : $"　来月から積み立て、{payoutPeriod}にまとめて支給"));
            }
            else
            {
                Show($"{s.title}を申請しました" + (money != "" ? $"　来月から毎月 {money}" : "　来月から支給されます"));
            }
        }
    }

    private void HandleExpired(SupportScheme s)
    {
        if (s == null) return;
        Show($"{s.title}の申請期限が過ぎました");   // 1回に絞るのは SupportManager 側
    }

    // ------------------------------------------------------------
    // 表示
    // ------------------------------------------------------------

    private IEnumerator Run()
    {
        while (queue.Count > 0)
        {
            if (label != null) label.text = queue.Dequeue();
            else queue.Dequeue();

            yield return Fade(0f, 1f);
            yield return new WaitForSecondsRealtime(showSeconds);
            yield return Fade(1f, 0f);
        }
        runner = null;
    }

    private IEnumerator Fade(float from, float to)
    {
        if (fadeSeconds <= 0f) { SetAlpha(to); yield break; }
        for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
        {
            SetAlpha(Mathf.Lerp(from, to, t / fadeSeconds));
            yield return null;
        }
        SetAlpha(to);
    }

    private void SetAlpha(float a)
    {
        if (group != null) group.alpha = a;
    }

    // ------------------------------------------------------------
    // 文字列づくり（金額は SupportManager が持つ値を読むだけ。お金は動かさない）
    // ------------------------------------------------------------

    private static int MoneyOfEffects(SupportScheme s)
    {
        float sum = 0f;
        if (s.effects != null)
        {
            foreach (var e in s.effects)
            {
                if (e != null && e.kind == EffectKind.Money) sum += e.value;
            }
        }
        return Mathf.RoundToInt(sum);
    }

    /// <summary>毎月の額。今の段があればそれ、無ければ支給表の最初の段。</summary>
    private static int MonthlyMoney(SupportManager mgr, SupportScheme s)
    {
        var tier = mgr.CurrentTier(s);
        if (tier == null && s.monthlyTiers != null)
        {
            foreach (var t in s.monthlyTiers)
            {
                if (t != null) { tier = t; break; }
            }
        }
        return tier != null ? tier.MoneyAmount() : 0;
    }

    private static string PayoutMonths(SupportScheme s)
    {
        var months = new List<string>();
        foreach (int month in s.payoutMonths)
        {
            string value = $"{month}";
            if (month >= 1 && month <= 12 && !months.Contains(value))
                months.Add(value);
        }
        return string.Join("・", months);
    }

    private static string FormatMoney(int money) => money == 0 ? "" : $"{money:+#,0;-#,0}";
}
