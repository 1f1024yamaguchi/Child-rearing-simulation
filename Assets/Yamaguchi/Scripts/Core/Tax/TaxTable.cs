using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>給与所得控除の1段。収入が upTo 以下なら「収入 × rate ＋ add」。</summary>
[Serializable]
public class SalaryDeductionRow
{
    [Tooltip("この額以下の収入に使う（円）。最後の段は大きな数にする")]
    public int upTo;
    [Range(0f, 1f)] public float rate;
    public int add;
}

/// <summary>基礎控除の1段。合計所得が incomeUpTo 以下なら amount。</summary>
[Serializable]
public class BasicDeductionRow
{
    public int incomeUpTo;
    public int amount;
}

/// <summary>所得税の速算表の1段。課税所得が upTo 以下なら「課税所得 × rate − subtract」。</summary>
[Serializable]
public class IncomeTaxRow
{
    public int upTo;
    [Range(0f, 1f)] public float rate;
    public int subtract;
}

/// <summary>
/// 税金の数値の表（taxes.md 3.1）。数値はすべてたたき台。税制が変わったらこのアセットだけ直す。
/// 計算は TaxCalculator が行い、ここは数値を持つだけ。
/// </summary>
[CreateAssetMenu(menuName = "子育て/税金/税金の表")]
public class TaxTable : ScriptableObject
{
    [Header("社会保険料")]
    [Tooltip("毎月の額面に掛ける率。健康保険・厚生年金・雇用保険のおおよその合計")]
    [Range(0f, 0.5f)] public float socialInsuranceRate = 0.15f;

    [Header("給与所得控除（収入の少ない順）")]
    public int salaryDeductionMin = 740000;
    public List<SalaryDeductionRow> salaryDeduction = new List<SalaryDeductionRow>
    {
        new SalaryDeductionRow { upTo = 2200000,  rate = 0f,   add = 740000 },
        new SalaryDeductionRow { upTo = 3600000,  rate = 0.3f, add = 80000 },
        new SalaryDeductionRow { upTo = 6600000,  rate = 0.2f, add = 440000 },
        new SalaryDeductionRow { upTo = 8500000,  rate = 0.1f, add = 1100000 },
        new SalaryDeductionRow { upTo = int.MaxValue, rate = 0f, add = 1950000 },
    };

    [Header("基礎控除・所得税（所得の少ない順）")]
    public List<BasicDeductionRow> basicDeduction = new List<BasicDeductionRow>
    {
        new BasicDeductionRow { incomeUpTo = 1320000,  amount = 950000 },
        new BasicDeductionRow { incomeUpTo = 3360000,  amount = 880000 },
        new BasicDeductionRow { incomeUpTo = 4890000,  amount = 680000 },
        new BasicDeductionRow { incomeUpTo = 6550000,  amount = 630000 },
        new BasicDeductionRow { incomeUpTo = 23500000, amount = 580000 },
        new BasicDeductionRow { incomeUpTo = int.MaxValue, amount = 0 },
    };

    [Header("所得税の速算表（課税所得の少ない順）")]
    public List<IncomeTaxRow> incomeTax = new List<IncomeTaxRow>
    {
        new IncomeTaxRow { upTo = 1950000,  rate = 0.05f, subtract = 0 },
        new IncomeTaxRow { upTo = 3300000,  rate = 0.10f, subtract = 97500 },
        new IncomeTaxRow { upTo = 6950000,  rate = 0.20f, subtract = 427500 },
        new IncomeTaxRow { upTo = 9000000,  rate = 0.23f, subtract = 636000 },
        new IncomeTaxRow { upTo = 18000000, rate = 0.33f, subtract = 1536000 },
        new IncomeTaxRow { upTo = 40000000, rate = 0.40f, subtract = 2796000 },
        new IncomeTaxRow { upTo = int.MaxValue, rate = 0.45f, subtract = 4796000 },
    };
    [Tooltip("復興特別所得税（所得税に掛ける率）")]
    [Range(0f, 0.1f)] public float reconstructionRate = 0.021f;

    [Header("住民税")]
    [Range(0f, 0.3f)] public float residentRate = 0.10f;
    public int residentBasicDeduction = 430000;
    [Tooltip("均等割（森林環境税を含む）")]
    public int residentPerCapita = 5000;
    [Tooltip("合計所得がこの額以下なら住民税は 0 円（非課税）")]
    public int residentNonTaxableIncome = 450000;
}
