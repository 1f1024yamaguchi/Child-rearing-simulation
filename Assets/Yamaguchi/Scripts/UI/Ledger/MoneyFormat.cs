using System;
using UnityEngine;

/// <summary>
/// お金の書式をまとめるクラス（expenses.md 5章・依頼E5）。
/// 家計簿・リザルト画面など全体で同じ表記を使う。
/// </summary>
public static class MoneyFormat
{
    /// <summary>「12,345円」の形式（符号なし・絶対値）。</summary>
    public static string Yen(int amount)
    {
        return $"{amount:N0}円";
    }

    /// <summary>「+12,345」「−12,345」「0」の形式（符号付き）。</summary>
    public static string Signed(int amount)
    {
        if (amount > 0) return $"+{amount:N0}";
        if (amount < 0) return $"−{-amount:N0}";
        return "0";
    }

    /// <summary>「+12,345円」「−12,345円」「0円」の形式。</summary>
    public static string SignedYen(int amount)
    {
        if (amount > 0) return $"+{amount:N0}円";
        if (amount < 0) return $"−{-amount:N0}円";
        return "0円";
    }

    /// <summary>「12.3万円」の形式（1万円単位・小数第1位まで）。</summary>
    public static string Man(int amount)
    {
        float man = amount / 10000f;
        return $"{man:0.#}万円";
    }
}
