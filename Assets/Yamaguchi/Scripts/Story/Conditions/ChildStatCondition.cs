using Maruyama.Events;
using UnityEngine;

/// <summary>子どものステータス判定対象。</summary>
public enum ChildStatKind
{
    成績,
    体調,
    趣味レベル,
    趣味の種類,
}

/// <summary>
/// 子どものステータス（成績・体調・趣味Lv・趣味の種類）が指定した条件を満たすときだけ発生を許す条件。
/// </summary>
public class ChildStatCondition : EventConditionSO, IDescribable
{
    [Tooltip("判定するステータスの種類")]
    public ChildStatKind statKind = ChildStatKind.成績;

    [Tooltip("最小値（成績・体調・趣味Lv で使用）")]
    public int minValue = 0;

    [Tooltip("最大値（成績・体調・趣味Lv で使用）")]
    public int maxValue = 100;

    [Tooltip("指定する趣味の種類（趣味の種類 で使用）")]
    public HobbyKind hobbyKind = HobbyKind.スポーツ;

    public override bool IsSatisfied()
    {
        if (Household.I == null || Household.I.Child == null) return true;

        var child = Household.I.Child;
        int lo = Mathf.Min(minValue, maxValue);
        int hi = Mathf.Max(minValue, maxValue);

        switch (statKind)
        {
            case ChildStatKind.成績:
            {
                int val = child.Grade;
                return val >= lo && val <= hi;
            }
            case ChildStatKind.体調:
            {
                int val = child.Health;
                return val >= lo && val <= hi;
            }
            case ChildStatKind.趣味レベル:
            {
                int val = child.HobbyLevel;
                return val >= lo && val <= hi;
            }
            case ChildStatKind.趣味の種類:
            {
                return child.Hobby == hobbyKind;
            }
            default:
                return true;
        }
    }

    public string Describe()
    {
        int lo = Mathf.Min(minValue, maxValue);
        int hi = Mathf.Max(minValue, maxValue);
        string range = (lo == hi) ? lo.ToString() : $"{lo}〜{hi}";

        switch (statKind)
        {
            case ChildStatKind.成績: return $"成績 {range}";
            case ChildStatKind.体調: return $"体調 {range}";
            case ChildStatKind.趣味レベル: return $"趣味Lv {range}";
            case ChildStatKind.趣味の種類: return $"趣味「{hobbyKind}」";
            default: return statKind.ToString();
        }
    }
}

