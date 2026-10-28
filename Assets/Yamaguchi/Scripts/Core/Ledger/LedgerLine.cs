using System;

/// <summary>家計簿の1行（expenses.md 5章）。</summary>
[Serializable]
public class LedgerLine
{
    public int monthIndex;            // 歳*12 + (月-1)
    public string source;             // 例 "保育料" "給料（母）" "児童手当" "食費（普通）" "病院代"
    public ExpenseCategory category;
    public int amount;                // 入金 +／出金 −
    public int covered;               // 支援で肩代わりされた額（0 なら空欄）
}
