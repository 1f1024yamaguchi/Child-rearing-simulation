using UnityEngine;

/// 子どもの成長段階。
public enum ChildStage
{
    Baby,     // 0〜1歳（ハイハイ）
    Toddler,  // 2〜5歳（幼少期）
    Kid,      // 6〜12歳（少年期）
    Teen,     // 13〜18歳（青年期）
    Adult,    // 19歳〜（大人のベース。職業が決まったら服を差し替える）
}

/// 子どもの年齢に合わせて、部屋のキャラの見た目（Animator）を成長段階ごとに差し替える。
/// 誕生日で段階が変わったら、すぐに差し替えていまの姿勢を続ける。
[RequireComponent(typeof(FamilyMemberActor), typeof(Animator))]
public class ChildStageSwitcher : MonoBehaviour
{
    [Header("段階ごとの見た目（Animator）")]
    [SerializeField] private RuntimeAnimatorController baby;
    [SerializeField] private RuntimeAnimatorController toddler;
    [SerializeField] private RuntimeAnimatorController kid;
    [SerializeField] private RuntimeAnimatorController teen;
    [SerializeField] private RuntimeAnimatorController adult;

    [Header("年齢の区切り（その歳から次の段階）")]
    [SerializeField] private int toddlerFrom = 2;
    [SerializeField] private int kidFrom = 6;
    [SerializeField] private int teenFrom = 13;
    [SerializeField] private int adultFrom = 19;

    [Tooltip("赤ちゃんのハイハイの速さ（歩く速さに対する倍率）")]
    [SerializeField] private float crawlSpeedScale = 0.4f;

    private FamilyMemberActor actor;
    private Animator anim;
    private ChildStage? current;
    private bool subscribed;
    private bool initialized;

    public ChildStage? Current => current;

    private void Awake()
    {
        actor = GetComponent<FamilyMemberActor>();
        anim = GetComponent<Animator>();
    }

    // GameState.I は Awake で入るので Start で購読する
    private void Start()
    {
        if (GameState.I != null)
        {
            GameState.I.OnAgeChanged += HandleAgeChanged;
            subscribed = true;
        }
    }

    private void OnDestroy()
    {
        if (subscribed && GameState.I != null) GameState.I.OnAgeChanged -= HandleAgeChanged;
    }

    // 最初の見た目は、FamilyActivityDirector が Start で居場所に置いたあとに合わせる
    private void Update()
    {
        if (initialized) return;
        initialized = true;
        if (GameState.I != null) HandleAgeChanged(GameState.I.AgeDays);
    }

    private void HandleAgeChanged(float ageDays)
    {
        GameDate.FromAgeDays(ageDays, out int year, out _, out _);
        Apply(StageFor(year));
    }

    public ChildStage StageFor(int age)
    {
        if (age >= adultFrom) return ChildStage.Adult;
        if (age >= teenFrom) return ChildStage.Teen;
        if (age >= kidFrom) return ChildStage.Kid;
        if (age >= toddlerFrom) return ChildStage.Toddler;
        return ChildStage.Baby;
    }

    /// 段階の見た目に差し替える（同じ段階なら何もしない）
    public void Apply(ChildStage stage)
    {
        if (current == stage) return;
        current = stage;

        RuntimeAnimatorController c = stage switch
        {
            ChildStage.Baby => baby,
            ChildStage.Toddler => toddler,
            ChildStage.Teen => teen,
            ChildStage.Adult => adult,
            _ => kid,
        };
        actor.SpeedScale = stage == ChildStage.Baby ? crawlSpeedScale : 1f;
        if (c == null || c == anim.runtimeAnimatorController) return;
        anim.runtimeAnimatorController = c;

        FamilyPose pose;
        if (actor.IsWalking) pose = FamilyPose.Walk;
        else if (actor.Current == null || actor.Current.Pose == FamilyPose.Away) pose = FamilyPose.Idle;
        else pose = actor.Current.Pose;
        anim.Play(pose.ToString(), 0, 0f);
    }

    [ContextMenu("テスト：次の段階にする")]
    private void NextForTest()
    {
        var next = current.HasValue ? (ChildStage)(((int)current.Value + 1) % 5) : ChildStage.Baby;
        Apply(next);
    }
}
