using System;
using System.Collections.Generic;

/// <summary>
/// リザルトに出す値の入れ物（result-scene.md 3章）。終わった瞬間の値を写したもの。
/// Main シーンの部品は end シーンに移ると消えるので、ここに写して ResultCarrier で渡す。育成記録にもそのまま保存する。
/// JsonUtility で保存できる型だけを使う（Dictionary・プロパティは保存されない）。
/// 振り返り用の月の記録（review-records.md 6.1）は LifeRecorder ができてから足す。
/// </summary>
[Serializable]
public class EndingResult
{
    public GameEndReason reason;
    public string childName = "";
    public int endAgeDays;
    public string finishedAt = "";      // yyyy/MM/dd HH:mm（育成記録の並び順用）

    public string careerId = "";        // なった職業（家出・破綻は空）
    public string careerName = "";
    public string dreamId = "";         // 将来の夢（決まる前に終わったら空）
    public string dreamName = "";
    public string epilogue = "";

    public long totalChildCost;
    public long totalSupport;
    public long totalIncome;
    public long finalMoney;             // マイナスもありうる
    public long totalScholarship;       // 奨学金（未実装なので 0）

    public int grade;
    public int health;
    public int childStress;
    public string hobby = "";
    public int hobbyLevel;
    public List<string> traits = new List<string>();

    public ParentResult mother = new ParentResult();
    public ParentResult father = new ParentResult();

    public int causeDays;               // 家出：限界が続いた日数／破綻：赤字が続いた月数
    public long causeValue;             // 家出：最後のストレス／破綻：最後の不足額（正の数）

    [Serializable]
    public class ParentResult
    {
        public bool exists;
        public string rank = "";
        public int salary;
        public int stress;
        public string workStyle = "";   // ライフ寄り／半々／ワーク寄り
    }

    /// <summary>「○歳○か月」。</summary>
    public string GetAgeText()
    {
        GameDate.FromAgeDays(endAgeDays, out int y, out int m, out _);
        int months = m - 1;
        return months > 0 ? $"{y}歳{months}か月" : $"{y}歳";
    }
}
