/// 効果の種類。effects.md 2章の定義を一字一句そのまま写す。
/// 新しい値を足したり名前を変えたりしない（3機能が共有する名前のため）。
public enum EffectKind
{
    Money,          // お金の増減
    Stress,         // ストレスの増減
    Health,         // 体調
    Grades,         // 成績
    Hobby,          // 趣味
    GoodEventRate,  // Goodイベントの発生率補正（％）
    BadEventRate,   // Badイベントの発生率補正（％）
    AddTrait,       // 特性（永続）を付ける → traits.md
    AddCondition,   // 状態（一時）を付ける → traits.md
    // ここに足していく（例：Fatigue, Relationship, JobRankProgress …）
}
