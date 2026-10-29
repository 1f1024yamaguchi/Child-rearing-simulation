using UnityEngine;

/// 親のライフ⇔ワークバランスに合わせて、部屋のキャラの服を着替えさせる。
/// 服＝Animator（絵だけ差し替えた Override Controller）。
/// スライダーを動かしても、その場では着替えない。次に歩き出したときに着替える
/// （玄関から帰ってきたらスーツ、のように自然に見せるため）。
[RequireComponent(typeof(FamilyMemberActor), typeof(Animator))]
public class ParentOutfitSwitcher : MonoBehaviour
{
    [Tooltip("どちらの親のバランスを見るか")]
    [SerializeField] private ParentRole role;

    [Header("服（Animator）")]
    [Tooltip("ライフ寄りのときの服")]
    [SerializeField] private RuntimeAnimatorController lifeOutfit;
    [Tooltip("中間のときの服")]
    [SerializeField] private RuntimeAnimatorController middleOutfit;
    [Tooltip("ワーク寄りのときの服")]
    [SerializeField] private RuntimeAnimatorController workOutfit;

    [Header("切り替えの境目（0=ライフ / 1=ワーク）")]
    [Tooltip("これ以下ならライフ寄りの服")]
    [Range(0f, 1f)][SerializeField] private float lifeMax = 0.33f;
    [Tooltip("これ以上ならワーク寄りの服")]
    [Range(0f, 1f)][SerializeField] private float workMin = 0.67f;
    [Header("着替えのタイミング")]
    [Tooltip("同じ場所に居続けて歩き出さないときも、この秒数たったらその場で着替える")]
    [SerializeField] private float maxWaitSeconds = 12f;


    private FamilyMemberActor actor;
    private Animator anim;
    private ParentStatus status;
    private RuntimeAnimatorController pending;
    private bool initialized;
    private bool wasWalking;
    private float pendingTime;


    private void Awake()
    {
        actor = GetComponent<FamilyMemberActor>();
        anim = GetComponent<Animator>();
    }

    // Household.I は Awake で入るので Start で取る
    private void Start()
    {
        if (Household.I != null) status = Household.I.Get(role);
        if (status != null) status.OnBalanceChanged += HandleBalanceChanged;
    }

    private void OnDestroy()
    {
        if (status != null) status.OnBalanceChanged -= HandleBalanceChanged;
    }

    private void HandleBalanceChanged(float balance)
    {
        var next = OutfitFor(balance);
        pending = next != anim.runtimeAnimatorController ? next : null;
        pendingTime = 0f;
    }

    private void Update()
    {
        // 最初のフレーム：今のバランスの服を着せ、いまの姿勢をやり直す
        // （FamilyActivityDirector が Start で居場所に置いたあとに行うため Update で）
        if (!initialized)
        {
            initialized = true;
            if (status != null) Wear(OutfitFor(status.WorkLifeBalance));
        }

        bool walking = actor.IsWalking;
        if (pending != null)
        {
            pendingTime += Time.deltaTime;
            // 歩き出した瞬間か、待ちすぎたら着替える
            if ((walking && !wasWalking) || pendingTime >= maxWaitSeconds)
            {
                Wear(pending);
                pending = null;
            }
        }
        wasWalking = walking;
    }

    private RuntimeAnimatorController OutfitFor(float balance)
    {
        RuntimeAnimatorController c;
        if (balance <= lifeMax) c = lifeOutfit;
        else if (balance >= workMin) c = workOutfit;
        else c = middleOutfit;
        return c != null ? c : anim.runtimeAnimatorController;
    }

    /// 服を着替えて、いまの姿勢のアニメーションを続ける
    private void Wear(RuntimeAnimatorController outfit)
    {
        if (outfit == null || outfit == anim.runtimeAnimatorController) return;
        anim.runtimeAnimatorController = outfit;

        FamilyPose pose;
        if (actor.IsWalking) pose = FamilyPose.Walk;
        else if (actor.Current == null || actor.Current.Pose == FamilyPose.Away) pose = FamilyPose.Idle;
        else pose = actor.Current.Pose;
        anim.Play(pose.ToString(), 0, 0f);
    }
}
