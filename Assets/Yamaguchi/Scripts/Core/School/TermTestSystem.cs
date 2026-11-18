using System;
using UnityEngine;

/// <summary>
/// 7・12・3月に通知表／学期末テスト／在学中のテストを行う（ending.md 2.5章・4章）。セーブキー "academic"。
/// - 小学校：通知表（成績を見せるだけ。記録しない）
/// - 中学・高校：学期末テスト。合格ライン（ChildStatus.passLine）で判定して記録。不合格なら子ストレス＋
/// - 高校卒業後：大学・専門に在学中だけテストを記録する（留年の判定は PathSystem が OnTermTest で行う）
/// 時間は止めない（通知だけ）。本編シーンでは SchoolBootstrap が自動で作る。
/// </summary>
public class TermTestSystem : MonoBehaviour, ISaveParticipant
{
    public static TermTestSystem I { get; private set; }

    [Header("たたき台")]
    [Tooltip("学期末テストで不合格だったときの子ストレス")]
    [SerializeField] private int failStress = 5;

    private AcademicRecord record = new AcademicRecord();
    private bool subscribed;

    public AcademicRecord Record => record;

    /// <summary>テストを記録したとき（通知表では出ない）。</summary>
    public event Action<TermTestRecord> OnTermTest;

    public string SaveKey => "academic";
    public int RestoreOrder => 50;

    private void Awake()
    {
        if (I != null && I != this) { Destroy(this); return; }
        I = this;
    }

    private void OnEnable() { SaveSystem.Register(this); Subscribe(); }
    private void Start() => Subscribe();
    private void OnDisable() { SaveSystem.Unregister(this); Unsubscribe(); }
    private void OnDestroy() { Unsubscribe(); if (I == this) I = null; }

    private void Subscribe()
    {
        if (subscribed || MonthTicker.I == null) return;
        MonthTicker.I.OnMonthChanged += HandleMonthChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (MonthTicker.I != null) MonthTicker.I.OnMonthChanged -= HandleMonthChanged;
        subscribed = false;
    }

    private void HandleMonthChanged(int age, int month)
    {
        if (!GameDate.IsTermEndMonth(month) || GameState.I == null || GameState.I.IsGameOver) return;
        RunTest(month);
    }

    /// <summary>今の日付でテスト（または通知表）を1回行う。</summary>
    public void RunTest(int month)
    {
        var child = Household.I != null ? Household.I.Child : null;
        if (child == null || GameState.I == null) return;
        float day = GameState.I.AgeDays;
        var stage = GameDate.Stage(day);

        switch (stage)
        {
            case SchoolStage.小学校:
                SchoolNotice.Toast($"通知表：成績 {child.Grade}");
                return;
            case SchoolStage.中学校:
            case SchoolStage.高校:
                break;
            case SchoolStage.高校卒業後:
                if (PathSystem.I == null || !PathSystem.I.IsEnrolled) return; // 就職・浪人中はテストなし
                break;
            default:
                return;
        }

        var r = new TermTestRecord
        {
            schoolYear = GameDate.SchoolYear(day),
            month = month,
            stage = stage,
            grade = child.Grade,
            passed = child.IsAbovePassLine,   // 丸めた整数で比べる（child-status.md 5章）
        };
        if (stage == SchoolStage.高校卒業後 && PathSystem.I.CurrentPath != null)
            r.passed = child.Grade >= PathSystem.I.CurrentPath.inSchoolPassLine;
        record.records.Add(r);

        if (stage != SchoolStage.高校卒業後)
        {
            if (r.passed) SchoolNotice.Toast($"学期末テスト：成績 {r.grade}（合格）");
            else
            {
                child.AddStress(failStress);
                SchoolNotice.Toast($"学期末テスト：成績 {r.grade}。合格ラインに届かなかった");
            }
        }
        OnTermTest?.Invoke(r);
    }

    // ─── セーブ ─────────────────────────────────
    public string Capture() => JsonUtility.ToJson(record);

    public void Restore(string json)
    {
        record = string.IsNullOrEmpty(json) ? new AcademicRecord() : (JsonUtility.FromJson<AcademicRecord>(json) ?? new AcademicRecord());
    }

    // ─── 確認用 ─────────────────────────────────
    [ContextMenu("Debug/記録をログに出す（評定・不合格回数）")]
    private void DebugLog()
    {
        var sb = new System.Text.StringBuilder($"[TermTestSystem] 記録 {record.records.Count} 回　不合格 {record.FailCount}　高校の平均 {record.HighSchoolAverage:0.0}　評定 {record.Hyotei}\n");
        foreach (var r in record.records) sb.AppendLine($"  年度{r.schoolYear} {r.month}月 {r.stage} 成績{r.grade} {(r.passed ? "合格" : "不合格")}");
        Debug.Log(sb.ToString());
    }

    [ContextMenu("Debug/学期末テストを今すぐ行う")]
    private void DebugRun()
    {
        if (GameState.I == null) return;
        GameDate.FromAgeDays(GameState.I.AgeDays, out _, out int m, out _);
        RunTest(GameDate.IsTermEndMonth(m) ? m : 7);
    }
}
