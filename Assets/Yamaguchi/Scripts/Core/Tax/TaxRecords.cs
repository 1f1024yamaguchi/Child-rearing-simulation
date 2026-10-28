using System;
using System.Collections.Generic;

/// <summary>給料日1回ぶんの明細（taxes.md 4章）。</summary>
public struct PaySlip
{
    public int gross;             // 額面
    public int socialInsurance;   // 社会保険料
    public int incomeTax;         // 所得税（源泉・概算）
    public int residentTax;       // 住民税

    public int Deducted => socialInsurance + incomeTax + residentTax;
    public int Net => gross - Deducted;
}

/// <summary>親1人・1年ぶんの記録。year は税金の年（＝子の年齢。その年の1月〜12月の給料日）。</summary>
[Serializable]
public class TaxYearRecord
{
    public int year;
    public int role;               // ParentRole の数値
    public int gross;              // 額面の合計
    public int socialInsurance;    // 社会保険料の合計
    public int withheld;           // 源泉徴収した所得税の合計
    public int payCount;           // 給料日の回数
    public int settledTax = -1;    // 年末調整（または確定申告）で決まった年税額。-1 はまだ
}

/// <summary>申請した控除1件。</summary>
[Serializable]
public class TaxClaim
{
    public int year;
    public string deductionId;
    public int role;      // 誰の控除にしたか（ParentRole の数値）
    public int route;     // DeductionRoute の数値（どこで出したか）
}

/// <summary>年ごとの手続きの進み具合。</summary>
[Serializable]
public class TaxYearFlags
{
    public int year;
    public bool yearEndFormDone;    // 年末調整の書類を出した（または出さないと決めた）
    public bool yearEndSettled;     // 12月の給料日で精算した
    public bool taxReturnFiled;     // 確定申告をした
    public bool taxReturnClosed;    // 確定申告の期限が過ぎた
}

/// <summary>住民税の年税額（どの年の所得から出したか）。</summary>
[Serializable]
public class ResidentTaxEntry
{
    public int role;
    public int baseYear;   // 元になった所得の年。マイナスはゲーム開始前（見込み）
    public int annual;
}

/// <summary>画面に出す控除1件ぶんの情報。</summary>
public class DeductionOption
{
    public TaxDeduction deduction;
    public bool motherEligible;
    public bool fatherEligible;
    public int motherAmount;          // 所得から引く額（所得税用）
    public int fatherAmount;
    public ParentRole recommended;    // 税金が多く減るほう

    public bool IsEligible(ParentRole r) => r == ParentRole.Mother ? motherEligible : fatherEligible;
    public int AmountFor(ParentRole r) => r == ParentRole.Mother ? motherAmount : fatherAmount;
}

/// <summary>TaxManager のセーブ内容。</summary>
[Serializable]
public class TaxSaveData
{
    public List<TaxYearRecord> records = new List<TaxYearRecord>();
    public List<TaxClaim> claims = new List<TaxClaim>();
    public List<TaxYearFlags> flags = new List<TaxYearFlags>();
    public List<ResidentTaxEntry> resident = new List<ResidentTaxEntry>();
}
