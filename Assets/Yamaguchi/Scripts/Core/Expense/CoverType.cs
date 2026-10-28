/// <summary>
/// 支援制度による支出の肩代わりの種類（expenses.md 4章）。
/// 値の順番は後から変えない（アセットに数字で残るため）。
/// </summary>
public enum CoverType
{
    Full,   // 全額
    Rate,   // 割合（value は 0〜1）
    UpTo,   // ○円まで（value は円）
}
