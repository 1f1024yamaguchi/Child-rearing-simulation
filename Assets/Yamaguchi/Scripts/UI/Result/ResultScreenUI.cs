using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// end シーンのリザルト画面の進行役（result-scene.md 4章）。計算はしない。EndingResult を受け取って表示するだけ。
/// 画面は end シーンに仮組みしてある ResultCanvas を名前で探してつなぐ（作り直さない。座標が消えるため）。
///
/// - ResultCarrier に結果があれば、それで中身を書き換える。無ければ（end シーンを単独で再生したとき）仮組みの見本のまま。
/// - 振り返り（グラフ・家計の時期ごと・親の20年・できごと）と家庭の一文は、月の記録（LifeRecorder）がまだ無いので隠す。
/// - 「将来の夢」「なった職業」を並べて出す。叶った／叶わなかったとは書かない（ending.md 5.3）。
/// - 上から順に出す。画面を押すと全部すぐ出す。
/// </summary>
public class ResultScreenUI : MonoBehaviour
{
    [Header("見出し（終わり方ごと）")]
    [SerializeField] private string headingGoal = "巣立ちの日";
    [SerializeField] private string headingRunaway = "家出";
    [SerializeField] private string headingBankrupt = "家計破綻";

    [Header("流し方")]
    [SerializeField] private float stepSeconds = 0.6f;
    [SerializeField] private string titleSceneName = "Start_scene";

    private const string ContentPath = "result_scroll/Viewport/Content";

    /// <summary>LifeRecorder ができるまで隠すもの（Content からの名前）。</summary>
    private static readonly string[] NotYet =
    {
        "review_divider", "sec_graph_child", "sec_graph_money", "sec_ledger", "sec_mother", "sec_father", "sec_lifelog",
        "sec_family/familyLine", "sec_family/parentingLine", "sec_money/row_deficit",
        "sec_money/btn_open_ledger_row", "sec_ledger/btn_open_ledger_2_row",
    };

    private Transform content;
    private readonly List<CanvasGroup> steps = new List<CanvasGroup>();
    private bool skipping;

    private void Start()
    {
        content = transform.Find(ContentPath);
        if (content == null)
        {
            Debug.LogWarning($"[ResultScreenUI] {ContentPath} が見つかりません。ResultCanvas に付けてください。");
            return;
        }
        BindTitleButton();

        if (!ResultCarrier.HasResult)
        {
            Debug.Log("[ResultScreenUI] 結果がありません（end シーンを単独で再生した）。仮組みの見本のまま表示します。");
            return;
        }

        Apply(ResultCarrier.Current);
        StartCoroutine(Reveal());
    }

    private void Update()
    {
        if (!skipping && UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame) skipping = true;
    }

    // ─── 中身を入れる ─────────────────────────────────
    private void Apply(EndingResult r)
    {
        foreach (var path in NotYet) SetActive(path, false);

        string age = r.GetAgeText();
        string heading, main, sub;
        switch (r.reason)
        {
            case GameEndReason.Runaway:
                heading = headingRunaway;
                main = $"{r.childName} は {age}で、家を出ていきました";
                sub = $"ストレスが限界のまま {r.causeDays}日 が過ぎました（最後のストレス {r.causeValue}）";
                break;
            case GameEndReason.Bankrupt:
                heading = headingBankrupt;
                main = $"{age}のとき、家計が立ち行かなくなりました";
                sub = $"赤字が {r.causeDays}か月 続きました（最後の不足額 {r.causeValue:N0}円）";
                break;
            default:
                heading = headingGoal;
                main = string.IsNullOrEmpty(r.careerName) ? $"{r.childName} は 巣立っていきました" : $"{r.childName} は {r.careerName} になりました";
                sub = $"{age}で巣立ちました";
                break;
        }
        SetText("hero/heading", heading);
        SetText("hero/mainLine", main);
        SetText("hero/subLine", sub);
        var corner = transform.Find("corner_top_right/text");
        if (corner != null) SetTMP(corner, r.reason == GameEndReason.GoalReached ? "巣立ちの日" : "終わった日");

        // 親の表
        SetParentRow("sec_family/parent_table/row_1", "母", r.mother);
        SetParentRow("sec_family/parent_table/row_2", "父", r.father);

        // お金
        SetRow("sec_money/row_childCost", Man(r.totalChildCost), r.totalChildCost != 0);
        SetRow("sec_money/row_support", Man(r.totalSupport), r.totalSupport != 0);
        SetRow("sec_money/row_income", Man(r.totalIncome), r.totalIncome != 0);
        SetRow("sec_money/row_finalMoney", Man(r.finalMoney), true);

        // 子ども
        SetRow("sec_child/row_dream", string.IsNullOrEmpty(r.dreamName) ? "まだ決まっていなかった" : r.dreamName, true);
        SetRow("sec_child/row_career", r.careerName, !string.IsNullOrEmpty(r.careerName));
        SetRow("sec_child/row_grade", r.grade.ToString(), true);
        SetRow("sec_child/row_hobby", string.IsNullOrEmpty(r.hobby) ? "なし" : $"{r.hobby} Lv{r.hobbyLevel}", true);
        SetRow("sec_child/row_health", r.health.ToString(), true);
        SetRow("sec_child/row_stress", r.childStress.ToString(), true);
        SetRow("sec_child/row_traits", r.traits.Count == 0 ? "なし" : string.Join("／", r.traits), true);

        if (!string.IsNullOrEmpty(r.epilogue)) SetText("hero/subLine", sub + "\n" + r.epilogue);
    }

    private void SetParentRow(string path, string who, EndingResult.ParentResult p)
    {
        var row = content.Find(path);
        if (row == null) return;
        row.gameObject.SetActive(p != null && p.exists);
        if (p == null || !p.exists || row.childCount < 5) return;
        SetTMP(row.GetChild(0), who);
        SetTMP(row.GetChild(1), p.rank);
        SetTMP(row.GetChild(2), Man(p.salary));
        SetTMP(row.GetChild(3), p.stress.ToString());
        SetTMP(row.GetChild(4), p.workStyle);
    }

    // ─── 上から順に出す ─────────────────────────────────
    private IEnumerator Reveal()
    {
        steps.Clear();
        foreach (Transform child in content)
        {
            if (!child.gameObject.activeSelf) continue;
            var cg = child.GetComponent<CanvasGroup>();
            if (cg == null) cg = child.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            steps.Add(cg);
        }
        foreach (var cg in steps)
        {
            float t = 0f;
            while (t < 1f && !skipping)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.05f, stepSeconds * 0.5f);
                cg.alpha = Mathf.Clamp01(t);
                yield return null;
            }
            cg.alpha = 1f;
            if (!skipping) yield return new WaitForSecondsRealtime(stepSeconds * 0.5f);
        }
    }

    // ─── タイトルへ ─────────────────────────────────
    private void BindTitleButton()
    {
        var t = content.Find("btn_title_row/btn_title");
        var btn = t != null ? t.GetComponent<Button>() : null;
        if (btn == null) return;
        btn.onClick.RemoveListener(GoTitle);
        btn.onClick.AddListener(GoTitle);
    }

    private void GoTitle()
    {
        // Ledger は static なので、消さないと［最初から］で前のプレイの家計簿が残る
        ResultCarrier.Clear();
        Ledger.I.Clear();
        SceneManager.LoadScene(titleSceneName);
    }

    // ─── 小物 ─────────────────────────────────
    /// <summary>「○○万円」。1万円未満は切り捨て。1億以上は「1億1,820万円」。マイナスは「−」。</summary>
    public static string Man(long yen)
    {
        bool neg = yen < 0;
        long man = System.Math.Abs(yen) / 10000;
        string s;
        if (man >= 10000)
        {
            long oku = man / 10000, rest = man % 10000;
            s = rest == 0 ? $"{oku}億円" : $"{oku}億{rest:N0}万円";
        }
        else s = $"{man:N0}万円";
        return neg ? "−" + s : s;
    }

    private void SetActive(string path, bool on)
    {
        var t = content.Find(path);
        if (t != null) t.gameObject.SetActive(on);
    }

    private void SetText(string path, string text)
    {
        var t = content.Find(path);
        if (t != null) SetTMP(t, text);
    }

    private void SetRow(string path, string value, bool show)
    {
        var row = content.Find(path);
        if (row == null) return;
        row.gameObject.SetActive(show);
        var v = row.Find("value");
        if (v != null) SetTMP(v, value);
    }

    private static void SetTMP(Transform t, string text)
    {
        var tmp = t.GetComponent<TMP_Text>();
        if (tmp != null) tmp.text = text;
    }
}
