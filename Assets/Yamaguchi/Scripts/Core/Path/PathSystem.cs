using System;
using System.Collections;
using System.Collections.Generic;
using Maruyama.Events;
using UnityEngine;

/// <summary>
/// 進路（ending.md 2章〜4章）。高校受験・進路決定・推薦・一般入試・不合格の選び直し・留年・ゴール（巣立ちの日）の更新。
/// セーブキー "path"。本編シーンでは SchoolBootstrap が自動で作る。進路のアセットは Resources/Paths から読む。
///
/// 判断の画面は、専用の画面ができるまでイベントのダイアログ（EventManager）を使う（PathDialogs）。
/// 選ばれた選択肢は EventManager.OnEventResolved で受け取る。時間はダイアログが止める。
/// 合否の乱数はセーブに入れない（ロードで引き直せてよい。ending.md 0章）。
/// </summary>
public class PathSystem : MonoBehaviour, ISaveParticipant
{
    public const string PathsResourcePath = "Paths";
    public static PathSystem I { get; private set; }

    [Header("子ストレス（たたき台）")]
    [Tooltip("希望と違う進路を選んだとき（3.1）")]
    [SerializeField] private int wishMismatchStress = 30;
    [Tooltip("公立高校に落ちたとき（2.5）")]
    [SerializeField] private int highSchoolFailStress = 10;
    [Tooltip("浪人を選んだとき（3.5）")]
    [SerializeField] private int roninStress = 5;
    [Tooltip("留年したとき（4章）")]
    [SerializeField] private int repeatStress = 10;

    [Header("時期（月）")]
    [SerializeField] private int decideMonth = 7;      // 高3の7月：進路を決める
    [SerializeField] private int recommendMonth = 11;  // 高3の11月：推薦の合否
    [SerializeField] private int examMonth = 2;        // 2月：高校受験（中3）・一般入試（高3・浪人中）

    private ChildPath state = new ChildPath();
    private readonly List<PathDefinition> paths = new List<PathDefinition>();
    private bool loaded, subscribed, testSubscribed;

    public event Action OnPathDecisionNeeded;
    public event Action<PathDefinition> OnPathChanged;
    public event Action<ExamRecord> OnExamResult;
    public event Action OnRetryChoiceNeeded;
    public event Action OnRepeatYear;

    public ChildPath State => state;
    public bool IsEnrolled => state.enrolled;
    public PathDefinition CurrentPath => Find(state.currentPathId);
    public PathDefinition TargetPath => Find(state.targetPathId);
    /// <summary>巣立ちのときの職業判定に使う進路。決めていなければ就職。</summary>
    public PathDefinition CareerPath => CurrentPath ?? Find(PathKind.就職);
    public IReadOnlyList<PathDefinition> AllPaths { get { Load(); return paths; } }

    public string SaveKey => "path";
    public int RestoreOrder => 60;

    // ─── ライフサイクル ─────────────────────────────────
    private void Awake()
    {
        if (I != null && I != this) { Destroy(this); return; }
        I = this;
    }

    private void OnEnable()
    {
        SaveSystem.Register(this);
        EventManager.OnEventResolved += HandleEventResolved;
        Subscribe();
    }

    private void Start() => Subscribe();

    private void OnDisable()
    {
        SaveSystem.Unregister(this);
        EventManager.OnEventResolved -= HandleEventResolved;
        if (subscribed && MonthTicker.I != null) MonthTicker.I.OnMonthChanged -= HandleMonthChanged;
        if (testSubscribed && TermTestSystem.I != null) TermTestSystem.I.OnTermTest -= HandleTermTest;
        subscribed = testSubscribed = false;
    }

    private void OnDestroy() { if (I == this) I = null; }

    private void Subscribe()
    {
        if (!subscribed && MonthTicker.I != null) { MonthTicker.I.OnMonthChanged += HandleMonthChanged; subscribed = true; }
        if (!testSubscribed && TermTestSystem.I != null) { TermTestSystem.I.OnTermTest += HandleTermTest; testSubscribed = true; }
    }

    private void Load()
    {
        if (loaded) return;
        loaded = true;
        paths.Clear();
        paths.AddRange(Resources.LoadAll<PathDefinition>(PathsResourcePath));
        paths.Sort((a, b) => a.kind != b.kind ? a.kind.CompareTo(b.kind) : string.CompareOrdinal(a.name, b.name));
        if (paths.Count == 0) Debug.LogWarning($"[PathSystem] 進路のアセットが Resources/{PathsResourcePath} にありません。");
    }

    public PathDefinition Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        Load();
        return paths.Find(p => p != null && p.id == id);
    }

    public PathDefinition Find(PathKind kind)
    {
        Load();
        return paths.Find(p => p != null && p.kind == kind);
    }

    private static ChildStatus Child => Household.I != null ? Household.I.Child : null;
    private static AcademicRecord Academic => TermTestSystem.I != null ? TermTestSystem.I.Record : null;
    private static int Today => GameState.I != null ? Mathf.FloorToInt(GameState.I.AgeDays) : 0;

    // ─── 毎月 ─────────────────────────────────
    private void HandleMonthChanged(int age, int month)
    {
        if (GameState.I == null || GameState.I.IsGameOver) return;
        int sy = GameDate.SchoolYear(Today);

        if (sy == 14 && month == examMonth && !state.highSchoolExamDone) HighSchoolExam();
        if (sy == 17 && month == decideMonth && TargetPath == null && !state.enrolled) RequestDecision();
        if (sy == 17 && month == recommendMonth && TargetPath != null && TargetPath.kind == PathKind.推薦 && !state.recommendationDone) Recommendation();
        if (month == examMonth && !state.enrolled && sy == 17 + state.roninCount)
        {
            var t = TargetPath;
            if (t != null && (t.kind == PathKind.大学 || t.kind == PathKind.専門学校)) GeneralExam(t, sy);
        }
    }

    // ─── 子どもの希望 ─────────────────────────────────
    /// <summary>
    /// 子どもの希望の進路。夢があれば、夢の職業が候補に入っている進路（推薦→大学→専門→就職の順。推薦は出願できるときだけ）。
    /// 夢が無ければ：推薦に出願できる → 推薦／成績が大学のライン以上 → 大学／趣味がある → 専門／それ以外 → 就職（たたき台）。
    /// </summary>
    public PathDefinition Wish()
    {
        Load();
        var child = Child;
        var order = new[] { PathKind.推薦, PathKind.大学, PathKind.専門学校, PathKind.就職 };
        var dream = DreamSystem.I != null ? DreamSystem.I.Dream : null;
        if (dream != null)
        {
            foreach (var k in order)
            {
                var p = Find(k);
                if (p == null || p.careers == null || !p.careers.Contains(dream)) continue;
                if (k == PathKind.推薦 && !AdmissionCalc.CanApplyRecommendation(child, Academic, p, out _)) continue;
                return p;
            }
        }
        var rec = Find(PathKind.推薦);
        if (rec != null && AdmissionCalc.CanApplyRecommendation(child, Academic, rec, out _)) return rec;
        var univ = Find(PathKind.大学);
        if (univ != null && child != null && child.Grade >= univ.passLine) return univ;
        if (child != null && child.Hobby != HobbyKind.なし && Find(PathKind.専門学校) != null) return Find(PathKind.専門学校);
        return Find(PathKind.就職);
    }

    // ─── 進路を決める（高3の7月）─────────────────────────────────
    public void RequestDecision()
    {
        state.pendingDialog = "decide";
        OnPathDecisionNeeded?.Invoke();
        PathDialogs.ShowDecision(this);
    }

    /// <summary>進路を決める。希望と違えば子ストレス＋。就職ならその場で巣立ちの日が決まる。</summary>
    public void Decide(PathDefinition p)
    {
        if (p == null) return;
        var wish = Wish();
        state.wishPathId = wish != null ? wish.id : "";
        state.targetPathId = p.id;
        state.pendingDialog = "";
        state.decidedByParent = wish != null && wish != p;
        if (state.decidedByParent && Child != null) Child.AddStress(wishMismatchStress);

        if (p.kind == PathKind.就職)
        {
            state.currentPathId = p.id;
            GameState.I?.SetGoal(GameDate.SchoolYearStart(GameDate.SchoolYear(Today) + 1), "巣立ち");
        }
        SchoolNotice.Toast($"進路を「{p.DisplayNameOrName}」に決めました" + (state.decidedByParent ? "（本人の希望とは違う）" : ""));
        OnPathChanged?.Invoke(p);
    }

    // ─── 高校受験（中3の2月）─────────────────────────────────
    public void HighSchoolExam()
    {
        state.highSchoolExamDone = true;
        var p = Find(PathKind.公立高校);
        var child = Child;
        if (p == null || child == null) return;
        float rate = AdmissionCalc.PassRate(child.Grade, p);
        bool passed = UnityEngine.Random.value < rate;
        var rec = AddExam(ExamKind.高校受験, p, rate, passed, true);
        if (!passed)
        {
            state.isPrivateHighSchool = true;
            child.AddStress(highSchoolFailStress);
        }
        PathDialogs.ShowNotice(passed ? "公立高校に合格" : "公立高校は不合格",
            passed ? "{name}は第一志望の公立高校に合格した。" : "{name}は公立高校に届かず、私立高校に通うことになった。入学金や制服代がかかる。",
            passed ? EventCategory.Merit : EventCategory.Demerit);
        OnExamResult?.Invoke(rec);
    }

    // ─── 推薦（高3の11月）─────────────────────────────────
    public void Recommendation()
    {
        state.recommendationDone = true;
        var p = TargetPath;
        var child = Child;
        var univ = Find(PathKind.大学);
        if (p == null || child == null) return;
        if (!AdmissionCalc.CanApplyRecommendation(child, Academic, p, out string reason))
        {
            if (univ != null) state.targetPathId = univ.id;
            PathDialogs.ShowNotice("推薦に出願できなかった", $"推薦の条件を満たしていない（{reason}）。2月の一般入試で大学を受ける。", EventCategory.Demerit);
            return;
        }
        int sy = GameDate.SchoolYear(Today);
        float rate = AdmissionCalc.PassRate(child.Grade, p);
        bool passed = UnityEngine.Random.value < rate;
        var rec = AddExam(ExamKind.推薦, p, rate, passed, state.wishPathId == p.id);
        if (passed)
        {
            Admit(p, sy + 1);
            PathDialogs.ShowNotice("推薦で合格", "{name}は推薦で合格した！ 春から" + p.DisplayNameOrName + "の道に進む。", EventCategory.Merit);
        }
        else
        {
            if (univ != null) state.targetPathId = univ.id;
            PathDialogs.ShowNotice("推薦は不合格", "推薦は届かなかった。2月の一般入試で大学を受ける。", EventCategory.Demerit);
        }
        OnExamResult?.Invoke(rec);
    }

    // ─── 一般入試（高3・浪人中の2月）─────────────────────────────────
    public void GeneralExam(PathDefinition p, int sy)
    {
        var child = Child;
        if (p == null || child == null) return;
        float rate = AdmissionCalc.PassRate(child.Grade, p);
        bool passed = UnityEngine.Random.value < rate;
        var rec = AddExam(ExamKind.一般, p, rate, passed, state.wishPathId == p.id);
        if (passed)
        {
            Admit(p, sy + 1);
            PathDialogs.ShowNotice("合格", "{name}は" + p.DisplayNameOrName + "に合格した！" + (state.roninCount > 0 ? $"（浪人{state.roninCount}年）" : ""), EventCategory.Merit);
        }
        else RequestRetry(p);
        OnExamResult?.Invoke(rec);
    }

    private void RequestRetry(PathDefinition failed)
    {
        state.pendingDialog = "retry";
        OnRetryChoiceNeeded?.Invoke();
        PathDialogs.ShowRetry(this, failed);
    }

    /// <summary>一般入試に落ちたあとの選び直し（3.5）。</summary>
    public void Retry(RetryChoice choice)
    {
        state.pendingDialog = "";
        int sy = GameDate.SchoolYear(Today);
        var wish = Find(state.wishPathId);
        bool wishedUniv = wish != null && (wish.kind == PathKind.大学 || wish.kind == PathKind.推薦);
        var child = Child;

        switch (choice)
        {
            case RetryChoice.浪人:
                state.roninCount++;
                if (child != null) child.AddStress(roninStress);
                GameState.I?.SetGoal(GameDate.SchoolYearStart(18 + state.roninCount), "巣立ち");
                SchoolNotice.Toast($"浪人して、来年もう一度受ける（浪人{state.roninCount}年目）");
                break;

            case RetryChoice.就職:
                if (wishedUniv && child != null) child.AddStress(wishMismatchStress);
                var job = Find(PathKind.就職);
                if (job != null) { state.currentPathId = job.id; state.targetPathId = job.id; }
                GameState.I?.SetGoal(GameDate.SchoolYearStart(sy + 1), "巣立ち");
                SchoolNotice.Toast("就職することにした");
                OnPathChanged?.Invoke(job);
                break;

            case RetryChoice.専門:
                var senmon = Find(PathKind.専門学校);
                if (senmon == null || child == null) return;
                if (wishedUniv) child.AddStress(wishMismatchStress);
                state.targetPathId = senmon.id;
                float rate = AdmissionCalc.PassRate(child.Grade, senmon);
                bool passed = UnityEngine.Random.value < rate;
                var rec = AddExam(ExamKind.一般, senmon, rate, passed, state.wishPathId == senmon.id);
                if (passed)
                {
                    Admit(senmon, sy + 1);
                    PathDialogs.ShowNotice("専門学校に合格", "{name}は" + senmon.DisplayNameOrName + "に合格した。", EventCategory.Merit);
                }
                else RequestRetry(senmon);
                OnExamResult?.Invoke(rec);
                break;
        }
    }

    // ─── 合格・在学・留年 ─────────────────────────────────
    private void Admit(PathDefinition p, int startSchoolYear)
    {
        state.currentPathId = p.id;
        state.targetPathId = p.id;
        state.enrolled = true;
        state.enrolledSchoolYear = startSchoolYear;
        state.yearInSchool = 1;
        UpdateGoalForEnrolled();
        OnPathChanged?.Invoke(p);
    }

    /// <summary>巣立ちの日 = 入学年度 + 在学年数 + 留年 の年の4月1日。</summary>
    private void UpdateGoalForEnrolled()
    {
        var p = CurrentPath;
        if (p == null || !state.enrolled) return;
        GameState.I?.SetGoal(GameDate.SchoolYearStart(state.enrolledSchoolYear + p.years + state.repeatCount), "巣立ち");
    }

    /// <summary>在学中の3月のテストのあと、その学年の平均で留年を判定する（4章）。</summary>
    private void HandleTermTest(TermTestRecord r)
    {
        if (r == null || r.stage != SchoolStage.高校卒業後 || r.month != 3 || !state.enrolled) return;
        YearEndCheck(r.schoolYear);
    }

    public void YearEndCheck(int schoolYear)
    {
        var p = CurrentPath;
        if (p == null || Academic == null) return;
        float avg = Academic.AverageOfYear(schoolYear);
        if (avg >= 0 && avg < p.inSchoolPassLine)
        {
            state.repeatCount++;
            Child?.AddStress(repeatStress);
            UpdateGoalForEnrolled();
            SchoolNotice.Toast($"{p.DisplayNameOrName}{state.yearInSchool}年の成績が足りず、留年した（巣立ちが1年のびた）");
            OnRepeatYear?.Invoke();
        }
        else state.yearInSchool++;
    }

    private ExamRecord AddExam(ExamKind kind, PathDefinition p, float rate, bool passed, bool wished)
    {
        var rec = new ExamRecord
        {
            kind = kind,
            schoolYear = GameDate.SchoolYear(Today),
            pathId = p.id,
            wasWished = wished,
            mockRank = AdmissionCalc.MockRank(rate),
            passed = passed,
        };
        state.exams.Add(rec);
        Debug.Log($"[PathSystem] {kind}：{p.DisplayNameOrName} 合格率{rate:P0}（{rec.mockRank}判定）→ {(passed ? "合格" : "不合格")}");
        return rec;
    }

    // ─── ダイアログの選択 ─────────────────────────────────
    private void HandleEventResolved(EventResolvedInfo info)
    {
        if (info.eventId == PathDialogs.DecideId) Decide(Find(info.choiceId));
        else if (info.eventId == PathDialogs.RetryId && Enum.TryParse(info.choiceId, out RetryChoice c)) Retry(c);
    }

    // ─── セーブ ─────────────────────────────────
    public string Capture() => JsonUtility.ToJson(state);

    public void Restore(string json)
    {
        state = string.IsNullOrEmpty(json) ? new ChildPath() : (JsonUtility.FromJson<ChildPath>(json) ?? new ChildPath());
        // 選んでもらう途中でセーブしていたら、ロードが全部終わった次のフレームで出し直す
        // （EventManager の復元（110）が出番待ちを捨てるので、ここでは出さない）
        if (!string.IsNullOrEmpty(state.pendingDialog) && isActiveAndEnabled) StartCoroutine(ReshowNextFrame());
    }

    private IEnumerator ReshowNextFrame()
    {
        yield return null;
        if (state.pendingDialog == "decide") RequestDecision();
        else if (state.pendingDialog == "retry") RequestRetry(TargetPath);
    }

    // ─── 確認用 ─────────────────────────────────
    [ContextMenu("Debug/今の学年・進路をログに出す")]
    private void DebugLog()
    {
        float d = Today;
        var child = Child;
        var sb = new System.Text.StringBuilder($"[PathSystem] {GameDate.Format(d)}　{GameDate.Stage(d)} {GameDate.GradeInStage(d)}年（年度{GameDate.SchoolYear(d)}）　ゴール {GameDate.Format(GameState.I != null ? GameState.I.GoalAgeDays : 0)}「{GameState.I?.GoalLabel}」\n");
        sb.AppendLine($"  希望：{Wish()?.DisplayNameOrName}　決めた進路：{TargetPath?.DisplayNameOrName}　在学：{(state.enrolled ? CurrentPath?.DisplayNameOrName + " " + state.yearInSchool + "年" : "なし")}　浪人{state.roninCount} 留年{state.repeatCount}　私立高校{state.isPrivateHighSchool}");
        foreach (var p in AllPaths)
        {
            if (child == null) break;
            float rate = AdmissionCalc.PassRate(child.Grade, p);
            string extra = p.kind == PathKind.推薦 ? (AdmissionCalc.CanApplyRecommendation(child, Academic, p, out string why) ? " 出願できる" : " 出願できない：" + why) : "";
            sb.AppendLine($"  {p.DisplayNameOrName}：ライン{p.passLine} 合格率{rate:P0}（{AdmissionCalc.MockRank(rate)}）{extra}");
        }
        foreach (var e in state.exams) sb.AppendLine($"  受験：年度{e.schoolYear} {e.kind} {Find(e.pathId)?.DisplayNameOrName} {e.mockRank} {(e.passed ? "合格" : "不合格")}");
        Debug.Log(sb.ToString());
    }

    [ContextMenu("Debug/進路を決める画面を出す")] private void DebugDecideDialog() => RequestDecision();
    [ContextMenu("Debug/就職に決める")] private void DebugJob() => Decide(Find(PathKind.就職));
    [ContextMenu("Debug/専門に決める")] private void DebugSenmon() => Decide(Find(PathKind.専門学校));
    [ContextMenu("Debug/大学に決める")] private void DebugUniv() => Decide(Find(PathKind.大学));
    [ContextMenu("Debug/推薦に決める")] private void DebugRec() => Decide(Find(PathKind.推薦));
    [ContextMenu("Debug/今すぐ高校受験")] private void DebugHs() => HighSchoolExam();
    [ContextMenu("Debug/今すぐ推薦の合否")] private void DebugRecExam() => Recommendation();
    [ContextMenu("Debug/今すぐ一般入試")] private void DebugExam() { var t = TargetPath; if (t != null) GeneralExam(t, GameDate.SchoolYear(Today)); }
    [ContextMenu("Debug/浪人")] private void DebugRonin() => Retry(RetryChoice.浪人);
    [ContextMenu("Debug/就職（不合格のあと）")] private void DebugRetryJob() => Retry(RetryChoice.就職);
    [ContextMenu("Debug/専門（不合格のあと）")] private void DebugRetrySenmon() => Retry(RetryChoice.専門);
    [ContextMenu("Debug/今すぐ学年末判定")] private void DebugYearEnd() => YearEndCheck(GameDate.SchoolYear(Today));
}
