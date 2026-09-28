using System;

namespace Maruyama.Core
{
    public enum EffectDuration
    {
        Instant,        // その場で1回だけ
        ThisMonth,      // 今月のあいだ継続
        Days,           // days 日のあいだ継続
    }

    public enum EffectTarget
    {
        Household,      // 世帯（お金・イベント発生率など）
        Mother,
        Father,
        Child,
        Parents,        // 母親と父親の両方
        Everyone,       // 家族全員
    }

    [Serializable]
    public class Effect
    {
        public EffectKind kind;
        public EffectTarget target = EffectTarget.Household;
        public float value;      // 増減量
        public EffectDuration duration = EffectDuration.Instant;
        public int days;       // duration == Days のとき
    }
}
