using System.Collections.Generic;
using UnityEngine;

/// 家族の状態から「誰が部屋のどこで何をするか」を決める。
/// - 親：ワーク寄りほど外出（仕事）が多い／ストレスが高いとソファで休む
/// - 子：勉強寄りほど机・外出（勉強）、遊び寄りほどラグやテレビ／状態（風邪など）が付いていたらベッド
/// - 月が変わったら全員で食卓に集まる
/// 歩く・アニメーションは FamilyMemberActor の担当。ここは行き先を決めるだけ。
public class FamilyActivityDirector : MonoBehaviour
{
    [Header("キャラ")]
    [SerializeField] private FamilyMemberActor mother;
    [SerializeField] private FamilyMemberActor father;
    [SerializeField] private FamilyMemberActor child;

    [Header("居場所（居間）")]
    [Tooltip("仕事に行くときに向かう玄関")]
    [SerializeField] private RoomSpot entrance;
    [SerializeField] private RoomSpot sofa;
    [SerializeField] private RoomSpot tv;
    [SerializeField] private RoomSpot kitchen;
    [Tooltip("月が変わったときの食卓の席")]
    [SerializeField] private RoomSpot eatMother;
    [SerializeField] private RoomSpot eatFather;
    [SerializeField] private RoomSpot eatChild;

    [Header("居場所（子ども部屋）")]
    [SerializeField] private RoomSpot desk;
    [SerializeField] private RoomSpot playRug;
    [Tooltip("子どもが遊んでいるとき、親が一緒に遊ぶ場所")]
    [SerializeField] private RoomSpot playWithChild;
    [SerializeField] private RoomSpot bed;

    [Header("タイミング（実時間の秒）")]
    [Tooltip("次の行動を決めるまでの時間（最小〜最大）")]
    [SerializeField] private Vector2 decideInterval = new Vector2(5f, 9f);
    [Tooltip("月が変わったときに食卓にいる時間")]
    [SerializeField] private float mealSeconds = 6f;

    [Header("子どもの外出（少年期以降）")]
    [Tooltip("この段階から子どもは玄関から外に出る")]
    [SerializeField] private ChildStage outingFromStage = ChildStage.Kid;
    [Tooltip("次の行動で外に出る基本の確率（勉強寄りほど増える）")]
    [Range(0f, 1f)] [SerializeField] private float outingChance = 0.35f;

    [Header("しきい値")]
    [Tooltip("このストレス以上の親は、家にいるときソファで休む")]
    [SerializeField] private int restStress = 70;

    private readonly Dictionary<FamilyMemberActor, float> timers = new Dictionary<FamilyMemberActor, float>();
    private readonly List<RoomSpot> options = new List<RoomSpot>();
    private float mealTimer;
    private bool monthSubscribed;
    private ChildStageSwitcher childStage;

    private IEnumerable<FamilyMemberActor> Actors
    {
        get
        {
            if (mother != null) yield return mother;
            if (father != null) yield return father;
            if (child != null) yield return child;
        }
    }

    private void Start()
    {
        if (mother != null) mother.Warp(kitchen);
        if (father != null) father.Warp(sofa);
        if (child != null) child.Warp(playRug);
        foreach (var a in Actors) timers[a] = Random.Range(1f, decideInterval.y);
        if (child != null) childStage = child.GetComponent<ChildStageSwitcher>();

        // MonthTicker.I は Awake で入るので、購読は Start で行う
        if (MonthTicker.I != null)
        {
            MonthTicker.I.OnMonthChanged += HandleMonthChanged;
            monthSubscribed = true;
        }
    }

    private void OnDestroy()
    {
        if (monthSubscribed && MonthTicker.I != null) MonthTicker.I.OnMonthChanged -= HandleMonthChanged;
    }

    private void HandleMonthChanged(int age, int month)
    {
        mealTimer = mealSeconds;
        if (mother != null) mother.GoTo(eatMother);
        if (father != null) father.GoTo(eatFather);
        if (child != null) child.GoTo(eatChild);
    }

    private void Update()
    {
        if (mealTimer > 0f)
        {
            mealTimer -= Time.deltaTime;
            if (mealTimer <= 0f)
            {
                // 食事が終わったら、それぞれすぐに次の行動を決める（同時に動かないよう少しずらす）
                foreach (var a in Actors) timers[a] = Random.Range(0f, 1.5f);
            }
            return;
        }

        foreach (var a in Actors)
        {
            timers[a] -= Time.deltaTime;
            if (timers[a] > 0f || a.IsWalking) continue;
            Decide(a);
            timers[a] = Random.Range(decideInterval.x, decideInterval.y);
        }
    }

    private void Decide(FamilyMemberActor a)
    {
        var next = a.Role == FamilyRole.Child ? ChooseForChild() : ChooseForParent(a);
        if (next != null) a.GoTo(next);
    }

    private RoomSpot ChooseForChild()
    {
        var s = Household.I != null ? Household.I.Child : null;
        if (s == null) return playRug;

        // 状態（風邪気味など）が1つでも付いていたら寝ている
        if (s.Conditions != null)
            foreach (var _ in s.Conditions.All) return bed;

        // 少年期以降は、玄関から外へ出かける（勉強寄りほど多い／勉強のため）
        if (entrance != null && childStage != null && childStage.Current.HasValue
            && childStage.Current.Value >= outingFromStage)
        {
            float chance = outingChance * (0.5f + 0.5f * s.StudyPlayBalance);
            if (Random.value < chance) return entrance;
        }

        // StudyPlayBalance：1=勉強 / 0=遊び
        if (Random.value < s.StudyPlayBalance) return desk;
        return Random.value < 0.7f ? playRug : tv;
    }

    private RoomSpot ChooseForParent(FamilyMemberActor a)
    {
        var hh = Household.I;
        var s = hh != null ? hh.Get(a.Role == FamilyRole.Mother ? ParentRole.Mother : ParentRole.Father) : null;
        if (s == null) return a.Target;

        // WorkLifeBalance：0=ライフ / 1=ワーク。ワーク寄りほど仕事に出ている
        if (Random.value < s.WorkLifeBalance) return entrance;

        if (s.Stress >= restStress && IsFree(sofa, a)) return sofa;

        options.Clear();
        options.Add(a.Role == FamilyRole.Mother ? kitchen : tv);
        if (IsFree(sofa, a)) options.Add(sofa);
        if (child != null && child.Target == playRug && IsFree(playWithChild, a)) options.Add(playWithChild);
        return options[Random.Range(0, options.Count)];
    }

    /// 自分以外の誰も、その場所にいない・向かっていない
    private bool IsFree(RoomSpot spot, FamilyMemberActor self)
    {
        if (spot == null) return false;
        foreach (var a in Actors)
            if (a != self && a.Target == spot) return false;
        return true;
    }
}
