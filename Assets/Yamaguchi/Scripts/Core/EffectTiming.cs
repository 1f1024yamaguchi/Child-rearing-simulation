/// <summary>制度の効果が起きるタイミング（support-apply.md 3.1）。</summary>
public enum EffectTiming
{
    OnApply,   // 申請したとき1回（例：出産育児一時金）
    Monthly,   // 受給中は毎月。申請した月の翌月分から（例：児童手当）
}
