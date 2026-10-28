using UnityEngine;

/// <summary>
/// 税金の計算だけをする（taxes.md 3章）。状態を持たないので、どこからでも呼べてテストしやすい。
/// 金額は円。途中の掛け算は long で行う。
/// </summary>
public static class TaxCalculator
{
    /// <summary>社会保険料（その月の額面 × 率）。</summary>
    public static int SocialInsurance(TaxTable t, int gross)
    {
        if (t == null || gross <= 0) return 0;
        return Mathf.RoundToInt(gross * t.socialInsuranceRate);
    }

    /// <summary>給与所得控除の額。</summary>
    public static int SalaryDeduction(TaxTable t, int gross)
    {
        if (t == null || gross <= 0) return 0;
        long ded = t.salaryDeductionMin;
        if (t.salaryDeduction != null)
        {
            foreach (var row in t.salaryDeduction)
            {
                if (row == null || gross > row.upTo) continue;
                ded = (long)(gross * (double)row.rate) + row.add;
                break;
            }
        }
        ded = System.Math.Max(ded, t.salaryDeductionMin);
        return (int)System.Math.Min(ded, gross);
    }

    /// <summary>給与所得（＝このゲームでは合計所得）。額面 − 給与所得控除。</summary>
    public static int SalaryIncome(TaxTable t, int gross)
    {
        return Mathf.Max(0, gross - SalaryDeduction(t, gross));
    }

    /// <summary>基礎控除（所得税）。合計所得で段が変わる。</summary>
    public static int BasicDeduction(TaxTable t, int totalIncome)
    {
        if (t == null || t.basicDeduction == null) return 0;
        foreach (var row in t.basicDeduction)
        {
            if (row != null && totalIncome <= row.incomeUpTo) return row.amount;
        }
        return 0;
    }

    /// <summary>
    /// 1年分の所得税（復興特別所得税を含む・100円未満切り捨て）。
    /// extraDeductions は申請した所得控除（所得税用）の合計。基礎控除と社会保険料控除はここで足す。
    /// </summary>
    public static int IncomeTax(TaxTable t, int annualGross, int annualSocialInsurance, int extraDeductions)
    {
        if (t == null || annualGross <= 0) return 0;
        int income = SalaryIncome(t, annualGross);
        long taxable = (long)income - annualSocialInsurance - BasicDeduction(t, income) - extraDeductions;
        if (taxable <= 0) return 0;
        taxable = taxable / 1000 * 1000;   // 1,000円未満切り捨て

        double tax = 0;
        if (t.incomeTax != null)
        {
            foreach (var row in t.incomeTax)
            {
                if (row == null || taxable > row.upTo) continue;
                tax = taxable * (double)row.rate - row.subtract;
                break;
            }
        }
        if (tax <= 0) return 0;
        tax *= 1.0 + t.reconstructionRate;
        long result = (long)tax / 100 * 100;   // 100円未満切り捨て
        return (int)System.Math.Min(result, int.MaxValue);
    }

    /// <summary>1年分の住民税（所得割＋均等割）。extraDeductions は申請した控除（住民税用）の合計。</summary>
    public static int ResidentTax(TaxTable t, int annualGross, int annualSocialInsurance, int extraDeductions)
    {
        if (t == null || annualGross <= 0) return 0;
        int income = SalaryIncome(t, annualGross);
        if (income <= t.residentNonTaxableIncome) return 0;

        long taxable = (long)income - annualSocialInsurance - t.residentBasicDeduction - extraDeductions;
        taxable = System.Math.Max(0L, taxable / 1000 * 1000);
        long tax = (long)(taxable * (double)t.residentRate) / 100 * 100 + t.residentPerCapita;
        return (int)System.Math.Min(tax, int.MaxValue);
    }

    /// <summary>医療費控除の額。医療費 −（floor か 所得 × 率 の少ない方）、上限あり。</summary>
    public static int MedicalDeduction(TaxDeduction d, int medicalPaid, int totalIncome)
    {
        if (d == null || medicalPaid <= 0) return 0;
        int floor = Mathf.Min(d.medicalFloor, Mathf.RoundToInt(totalIncome * d.medicalIncomeRate));
        return Mathf.Clamp(medicalPaid - floor, 0, d.medicalCap);
    }
}
