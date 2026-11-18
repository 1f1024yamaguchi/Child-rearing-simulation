using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 職業1つのデータ（ending.md 5章・8章）。将来の夢（中1）と巣立ちの日の職業の判定で使う。
/// 職業を増やすときはこのアセットを1つ足すだけ。数値はすべてたたき台。
///
/// 置き場所：Assets/Yamaguchi/Resources/Careers/（DreamSystem が Resources から全部読む）。
/// 判定の計算は CareerJudge に1か所。このクラスは値を持つだけ。
/// </summary>
[CreateAssetMenu(fileName = "Career_", menuName = "子育て/職業")]
public class CareerDefinition : ScriptableObject
{
    [Serializable]
    public class TraitBonus
    {
        [Tooltip("この特性を持っていたら加点する")]
        public TraitDefinition trait;
        [Tooltip("加点（たたき台）")]
        public float bonus = 10f;
    }

    [Header("基本")]
    [Tooltip("職業のID（英数字と _ のみ。セーブのキーなので、ゲームに出したあとは変えない）")]
    public string id = "";

    [Tooltip("表示名（例：プロ野球選手）。リザルト・子カード・{dream} に出る")]
    public string displayName = "";

    [Tooltip("リザルトのイラスト（無くてよい）")]
    public Sprite illustration;

    [Tooltip("条件を満たす職業が1つも無いときの職業（会社員）。1つだけにする")]
    public bool isFallback = false;

    [Header("条件（満たさないと候補から外れる）")]
    [Tooltip("必要な成績（0 = 問わない）")]
    [Range(0, 100)] public int minGrade = 0;

    [Tooltip("必要な体調（0 = 問わない）")]
    [Range(0, 100)] public int minHealth = 0;

    [Tooltip("見る趣味の種類（なし = 趣味を見ない）。点数の「趣味Lv×重み」もこの種類のときだけ入る")]
    public HobbyKind hobby = HobbyKind.なし;

    [Tooltip("必要な趣味Lv（0 = 問わない）。1以上なら、趣味の種類が合っていてこのLv以上であること")]
    [Min(0)] public int minHobbyLevel = 0;

    [Tooltip("中学・高校の学期末テストで不合格だった回数の上限（-1 = 問わない）。研究者など成績重視の職業に使う")]
    public int maxFailCount = -1;

    [Tooltip("ほかの条件（フラグ・年収など。支援制度の条件の部品をそのまま使える）。全部満たすこと")]
    public List<RequirementSO> requirements = new List<RequirementSO>();

    [Header("点数の重み（たたき台）")]
    [Tooltip("成績（0〜100）に掛ける")]
    public float gradeWeight = 1f;

    [Tooltip("趣味Lv に掛ける（趣味の種類が合うときだけ）")]
    public float hobbyWeight = 0f;

    [Tooltip("体調（0〜100）に掛ける")]
    public float healthWeight = 0f;

    [Tooltip("特性の加点")]
    public List<TraitBonus> traitBonuses = new List<TraitBonus>();

    [Header("エピローグ")]
    [TextArea(1, 3)]
    [Tooltip("リザルトの最後の一言（例：5年後、博士に）。空でよい")]
    public string epilogue = "";

    /// <summary>表示名（空ならアセット名）。</summary>
    public string DisplayNameOrName => string.IsNullOrEmpty(displayName) ? name : displayName;
}
