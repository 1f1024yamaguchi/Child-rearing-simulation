using System;

/// <summary>
/// 支出項目の実行中の状態（expenses.md 3.2）。セーブ対象。
/// </summary>
[Serializable]
public class ExpenseRuntime
{
    public string id;
    public bool enrolled;           // Optional：申し込み中か（塾に通っているか）
    public int enrolledAgeDays = -1;
}
