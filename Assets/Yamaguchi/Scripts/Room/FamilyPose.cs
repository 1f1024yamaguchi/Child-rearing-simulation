/// 部屋の中でキャラがとる姿勢。
/// Idle〜Sleep は Animator のステート名と同じ名前にしておくこと（文字列で再生するため）。
public enum FamilyPose
{
    Idle,   // 立っている
    Walk,   // 歩いている
    Sit,    // 座っている
    Eat,    // 食べている
    Sleep,  // 寝ている
    Away,   // 外出中（玄関まで行って姿を消す）
}

/// 部屋に出てくる家族の役。
public enum FamilyRole
{
    Mother,
    Father,
    Child,
}
