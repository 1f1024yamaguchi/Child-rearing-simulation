using System;

/// <summary>「何のお金か」（名前＋種類）。Ledger.Record や EffectApplier に渡す。</summary>
[Serializable]
public class LedgerSource
{
    public string name;
    public ExpenseCategory category;

    public LedgerSource(string name, ExpenseCategory category)
    {
        this.name = name;
        this.category = category;
    }

    /// <summary>何のお金か分からないとき用。</summary>
    public static readonly LedgerSource Unknown = new LedgerSource("その他", ExpenseCategory.その他);
}
