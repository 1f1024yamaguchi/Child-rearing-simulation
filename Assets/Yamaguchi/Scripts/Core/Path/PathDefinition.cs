using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 進路1つ（ending.md 2章・3章・8章）。在学年数・合格ライン・合格率の式の値・推薦の条件・職業の候補。
/// 置き場所：Assets/Yamaguchi/Resources/Paths/（PathSystem が Resources から読む）。数値はすべてたたき台。
/// </summary>
[CreateAssetMenu(fileName = "Path_", menuName = "子育て/進路")]
public class PathDefinition : ScriptableObject
{
    [Header("基本")]
    [Tooltip("ID（英数字と _。セーブのキーなので、ゲームに出したあとは変えない）")]
    public string id = "";
    public string displayName = "";
    public PathKind kind = PathKind.未定;
    [Tooltip("在学年数（就職は 0）")]
    [Min(0)] public int years = 0;

    [Header("合否（3.4・3.5）")]
    [Tooltip("合格ライン（成績）。就職は使わない")]
    public int passLine = 50;
    [Tooltip("ラインちょうどのときの合格率")]
    [Range(0f, 1f)] public float baseRate = 0.5f;
    [Tooltip("ラインから1点離れるごとに変わる合格率")]
    public float slope = 0.04f;
    [Range(0f, 1f)] public float minRate = 0.05f;
    [Range(0f, 1f)] public float maxRate = 0.95f;

    [Header("推薦の出願条件（推薦のときだけ）")]
    [Tooltip("必要な趣味の種類（なし = 種類は問わないが、趣味があること）")]
    public HobbyKind requiredHobby = HobbyKind.なし;
    [Min(0)] public int requiredHobbyLevel = 10;
    [Tooltip("必要な評定（1〜5）")]
    [Range(0, 5)] public int requiredHyotei = 3;

    [Header("在学中（4章・留年）")]
    [Tooltip("在学中の学期末テストの合格ライン。学年の平均がこれを下回ると留年")]
    public int inSchoolPassLine = 40;

    [Header("入学条件（お金など。依頼8で使う）")]
    public List<RequirementSO> requirements = new List<RequirementSO>();

    [Header("職業（5.1）")]
    [Tooltip("この進路で巣立ったときの職業の候補")]
    public List<CareerDefinition> careers = new List<CareerDefinition>();
    [Tooltip("条件を満たす候補が無いときの職業（空なら会社員＝既定の職業）")]
    public CareerDefinition fallbackCareer;

    public string DisplayNameOrName => string.IsNullOrEmpty(displayName) ? name : displayName;

    /// <summary>入学してから巣立つまでに学校に通う進路か（専門・大学・推薦）。</summary>
    public bool IsSchool => kind == PathKind.専門学校 || kind == PathKind.大学 || kind == PathKind.推薦;
}
