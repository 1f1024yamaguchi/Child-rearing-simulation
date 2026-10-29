using UnityEngine;

/// 部屋の中の「居場所」。キャラはここまで歩いてきて、決められた姿勢をとる。
/// 位置はこの GameObject の Transform（＝キャラの足元）。
public class RoomSpot : MonoBehaviour
{
    [Tooltip("着いたらとる姿勢。Away は玄関から外出（姿を消す）")]
    [SerializeField] private FamilyPose pose = FamilyPose.Idle;

    [Tooltip("着いたら左を向く（絵はもともと右向き）")]
    [SerializeField] private bool faceLeft;

    public FamilyPose Pose => pose;
    public bool FaceLeft => faceLeft;
    public Vector3 Position => transform.position;

    private void OnDrawGizmos()
    {
        Gizmos.color = pose == FamilyPose.Away ? Color.gray : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.08f);
    }
}
