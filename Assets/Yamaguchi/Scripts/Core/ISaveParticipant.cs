/// <summary>
/// セーブに参加する部品。自分の分を文字列（JSON）にして渡し、ロードのときに受け取って戻す。
/// OnEnable で SaveSystem.Register(this)、OnDisable で SaveSystem.Unregister(this) を呼ぶ。
/// </summary>
public interface ISaveParticipant
{
    /// <summary>例 "family" "support"。他と重ねない・後から変えない。</summary>
    string SaveKey { get; }

    /// <summary>小さい順に Restore される（家族 10 → 年収 20 → 制度 30 → 家出 40 → 他の人 100〜）。</summary>
    int RestoreOrder { get; }

    /// <summary>今の状態を JSON 文字列にして返す。</summary>
    string Capture();

    /// <summary>JSON から戻す（セーブにそのキーが無ければ呼ばれない）。</summary>
    void Restore(string json);
}
