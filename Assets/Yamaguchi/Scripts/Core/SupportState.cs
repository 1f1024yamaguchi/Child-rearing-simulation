/// <summary>制度の状態（support-apply.md 2章）。</summary>
public enum SupportState
{
    Locked,      // 申請できない：開始日より前、または申請条件を満たしていない（グレー）
    Available,   // 申請できる（オレンジ＋申請ボタン）
    Received,    // 受け取り済み：その場で1回もらう制度を申請した（緑）
    Active,      // 受給中：毎月もらう制度を申請した（緑）
    Suspended,   // 支給停止：毎年の手続きを過ぎた（赤みのオレンジ＋手続きボタン）
    Expired,     // 期限切れ：申請しないまま期限を過ぎた（赤）
    Lost,        // 受給資格なし：支給停止のまま lapseYears 経過（赤）
    Ended,       // 受給終了：支給表の最後の年齢を過ぎた（グレー）
}
