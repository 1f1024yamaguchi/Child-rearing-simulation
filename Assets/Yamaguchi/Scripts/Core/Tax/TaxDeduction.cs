using System.Collections.Generic;
using UnityEngine;

/// <summary>どこで申請できるか（taxes.md 5.1）。</summary>
public enum DeductionRoute
{
    YearEnd = 0,        // 年末調整で出せる（出し忘れたら確定申告でも出せる）
    TaxReturnOnly = 1,  // 確定申告だけ
}

/// <summary>控除の額の決まり方。</summary>
public enum DeductionKind
{
    Fixed = 0,     // 決まった額
    Spouse = 1,    // 配偶者（もう1人の親）の合計所得で判定。額は決まった額
    Medical = 2,   // その年の医療費から計算
}

/// <summary>
/// 控除1つのデータ（taxes.md 5.1）。1つ＝1アセット。
/// 申請したかどうか（今の状態）はここに書かず、TaxManager が id をキーに持つ。
/// </summary>
[CreateAssetMenu(menuName = "子育て/税金/控除")]
public class TaxDeduction : ScriptableObject
{
    [Header("表示")]
    public string id;                        // 例 "dependent_child"（セーブ用。後から変えない）
    public string title;                     // 例 "扶養控除（子）"
    [TextArea] public string description;    // 例 "16歳以上の子どもを養っているとき"

    [Header("申請")]
    public DeductionRoute route = DeductionRoute.YearEnd;
    public DeductionKind kind = DeductionKind.Fixed;

    [Header("額（所得から引く額）")]
    [Min(0)] public int incomeTaxAmount = 380000;
    [Min(0)] public int residentTaxAmount = 330000;

    [Header("条件")]
    [Tooltip("その年の12月31日の子の年齢がこの歳以上（-1 で条件なし）")]
    public int childAgeMin = -1;
    [Tooltip("その年の12月31日の子の年齢がこの歳以下（-1 で条件なし）")]
    public int childAgeMax = -1;
    [Tooltip("Spouse：もう1人の親の合計所得がこの額以下")]
    [Min(0)] public int spouseIncomeMax = 580000;
    [Tooltip("申請する人の合計所得がこの額以下（0 で条件なし）")]
    [Min(0)] public int claimantIncomeMax = 0;
    [Tooltip("追加の条件（全部満たしたら申請できる）")]
    public List<RequirementSO> extraRequirements = new List<RequirementSO>();

    [Header("医療費（Medical のとき）")]
    [Tooltip("医療費からこの額を引く（所得 × 率の方が少なければそちら）")]
    [Min(0)] public int medicalFloor = 100000;
    [Range(0f, 1f)] public float medicalIncomeRate = 0.05f;
    [Min(0)] public int medicalCap = 2000000;

    public bool ChildAgeOk(int childAgeAtYearEnd)
    {
        if (childAgeMin >= 0 && childAgeAtYearEnd < childAgeMin) return false;
        if (childAgeMax >= 0 && childAgeAtYearEnd > childAgeMax) return false;
        return true;
    }
}
