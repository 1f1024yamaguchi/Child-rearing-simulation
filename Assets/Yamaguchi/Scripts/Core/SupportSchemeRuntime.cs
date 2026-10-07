using System.Collections.Generic;

/// <summary>
/// 1制度ぶんの実行中の状態（support-apply.md 5.2）。そのまま SaveData に入れられる形。
/// SupportScheme（アセット）には状態を書かず、id をキーにこちらで持つ。
/// </summary>
[System.Serializable]
public class SupportSchemeRuntime
{
    public string       id;
    public SupportState state;
    public int  appliedAgeDays = -1;     // 申請した日（未申請は -1）
    public int  appliedMonthIndex = -1;  // 申請した月（「翌月から」用）
    public int  lastPaidMonthIndex = -1; // 最後に払った月（同じ月に2回払わないため）
    public int  lastPaidAmount = 0;      // 最後に払ったお金（カード表示用）
    public int  renewedYear = -1;        // 毎年の手続きを済ませた歳（Yearly）
    public int  suspendedAgeDays = -1;   // 支給停止になった日
    public List<int> owedTierIndexes = new List<int>(); // 支給停止中に払うはずだった段（再開時にまとめて払う）
    public bool expiredNotified = false; // 期限切れ通知を出したか
}
