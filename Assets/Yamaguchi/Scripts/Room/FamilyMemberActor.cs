using System;
using UnityEngine;

/// 部屋の中の1人ぶんの見た目を動かす。
/// 言われた居場所まで床の上を歩き、着いたらその場所の姿勢のアニメーションを再生するだけ。
/// どこへ行くかは決めない（FamilyActivityDirector の担当）。
[RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
public class FamilyMemberActor : MonoBehaviour
{
    [Tooltip("母親・父親・子どものどれか")]
    [SerializeField] private FamilyRole role;

    [Tooltip("歩く速さ（ユニット/秒）。PPU20 なので 1.2 で 1秒に24ドット")]
    [SerializeField] private float walkSpeed = 1.2f;

    [Tooltip("歩くときに通る床の高さ（ワールドY）。ベッドの上などから降りるときもこの高さに戻る")]
    [SerializeField] private float floorY;

    private SpriteRenderer sr;
    private Animator anim;
    private RoomSpot target;
    private Vector3 walkGoal;
    private bool walking;

    public FamilyRole Role => role;
    /// いま居る場所（歩いている間は null）
    public RoomSpot Current { get; private set; }
    /// 向かっている、または居る場所
    public RoomSpot Target => target;
    public bool IsWalking => walking;
    /// 歩く速さの倍率（赤ちゃんのハイハイはゆっくり、など）
    public float SpeedScale { get; set; } = 1f;


    /// 居場所に着いたとき
    public event Action<FamilyMemberActor, RoomSpot> OnArrived;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
    }

    /// 居場所まで歩いていく
    public void GoTo(RoomSpot spot)
    {
        if (spot == null || spot == target) return;
        target = spot;
        Current = null;

        sr.enabled = true;   // 外出中なら玄関から現れる
        var p = transform.position;
        p.y = floorY;        // ベッドの上などにいたら床に下りる
        transform.position = p;
        walkGoal = new Vector3(spot.Position.x, floorY, p.z);
        walking = true;
        Play(FamilyPose.Walk);
    }

    /// 歩かずにその場所へ移す（開始時など）
    public void Warp(RoomSpot spot)
    {
        if (spot == null) return;
        target = spot;
        walking = false;
        Arrive();
    }

    private void Update()
    {
        if (!walking) return;

        var p = transform.position;
        float dx = walkGoal.x - p.x;
        if (Mathf.Abs(dx) > 0.001f) sr.flipX = dx < 0f;   // 絵は右向きなので、左へ歩くときだけ反転

        p = Vector3.MoveTowards(p, walkGoal, walkSpeed * SpeedScale * Time.deltaTime);
        transform.position = p;

        if ((p - walkGoal).sqrMagnitude < 0.000001f)
        {
            walking = false;
            Arrive();
        }
    }

    private void Arrive()
    {
        Current = target;
        var pos = target.Position;
        pos.z = transform.position.z;
        transform.position = pos;
        sr.flipX = target.FaceLeft;

        if (target.Pose == FamilyPose.Away)
        {
            sr.enabled = false;   // 玄関から外に出た
            Play(FamilyPose.Idle);
        }
        else
        {
            sr.enabled = true;
            Play(target.Pose);
        }
        OnArrived?.Invoke(this, target);
    }

    private void Play(FamilyPose pose)
    {
        if (anim.runtimeAnimatorController == null) return;
        anim.Play(pose.ToString(), 0, 0f);
    }
}
