---
title: カレンダー機能
updated: 2026-09-21
status: 設計中（たたき台・要合意）
related: [docs/design/support-systems.md, docs/design/events.md, docs/code/architecture.md]
---

# カレンダー機能

> **このファイルは合意前のたたき台です。** 他の design ドキュメントと違い、話し合って決まった内容ではなく、担当（草尾）が決めるための出発点として書かれています。確定した項目から順に「たたき台」の断りを外していってください。
> 位置づけ：[support-systems.md](support-systems.md) 2.5章で整理した現実のデメリット（**①申請の手間 / ⑦手続きの継続**）を、プレイヤーが操作できる形にしたもの。

---

## 1. この機能が何のためにあるか

このゲームのテーマは「**知っていれば得をする**」。支援制度は申請しなければ1円ももらえず、現況届を出し忘れれば止まる（[support-systems.md](support-systems.md)）。

カレンダーは、その「知っている／忘れない」というプレイヤーの能力を**操作に落とし込む装置**です。

- 制度側から見ると：申請期限という締め切りが可視化され、「申請できる」状態を逃すと損をする。
- プレイヤーから見ると：実際のカレンダーアプリのように**自分でメモを書いて管理できる**。
- ゲーム体験として：時間はリアルタイムで流れ続ける（[../decisions/0001-realtime-time-model.md](../decisions/0001-realtime-time-model.md)）ので、「あの申請いつまでだっけ」と確認しに来る動機が自然に生まれる。

**やらないこと**：全部を自動で申請してくれる便利機能にはしない。それをやると「知っていれば得をする」というテーマが消える。

---

## 2. 画面の構成（たたき台）

`Main_test` 下部の「カレンダー」ボタンから開く（現在は `Debug.Log` の仮ハンドラ。→ [../code/ui-main-test-hud.md](../code/ui-main-test-hud.md)）。

```
カレンダー画面
├ ヘッダー        … 「3歳 5月」＋ 前月／翌月ボタン
├ 月グリッド      … 日付セル 7×5〜6。セルの中に予定チップ（最大2〜3件＋「+2」）
│   └ 今日のセルを強調。期限が近い日は色を変える
├ 選択日の詳細    … その日の予定リスト（下部 or 右側）
│   └ 各予定：アイコン／タイトル／状態バッジ／[申請する] ボタン
└ メモ追加ボタン  … プレイヤーが自由に書き込む
```

- ゲーム内の暦は `GameDate` が持っている（0歳1月1日起点、1年365日、月の日数は実際の暦）。**自前で日付計算をしない**こと。→ [../code/architecture.md](../code/architecture.md) 2章
- **カレンダーを開いている間はゲーム内時間を止める**（決定済み）。ただし `SetRunning` を直接呼ばず、停止要求をカウントする共通の窓口（`PushPause` / `PopPause`）を使うこと。→ [../decisions/0004-pause-during-decisions.md](../decisions/0004-pause-during-decisions.md)

---

## 3. カレンダーに載るもの（たたき台）

| 種類 | 誰が入れる | 例 | 期限を過ぎると |
|---|---|---|---|
| 申請期限 | 自動（条件を満たすと出現） | 児童手当の申請、幼保無償化の手続き | その制度を受けられない／受給開始が遅れる |
| 定期手続き | 自動（受給を始めると毎年出現） | 現況届の提出 | 受給が停止・失効する |
| 予防接種 | 自動（子の年齢で出現） | 定期接種のスケジュール | 体調イベントの発生率が下がらないまま |
| 行事・イベント | 自動 | 運動会、学校行事 | 子の趣味・ストレスに影響 |
| プレイヤーのメモ | 手動 | 「来月は高級食にする」「保育園の見学」 | 何も起きない（自分用の備忘） |

- 自動で入る予定は、制度データ（[support-systems.md](support-systems.md)）側に「いつから／いつまで」を持たせて、カレンダーがそれを読む形にする。
- **手動メモは効果を持たない。** 効果を持たせると「メモしないと損」になり、作業感が出る。

---

## 4. 状態と締め切りの扱い（たたき台）

[support-systems.md](support-systems.md) 3章の状態表示と揃える。

| 状態 | 色 | 意味 |
|---|---|---|
| 条件未達 | グレー | まだ対象外（例：子が3歳になるまで） |
| 申請できる | オレンジ | 条件を満たした。**締め切りあり** |
| 受給中 | 緑 | 申請済みで効果が出ている |
| 期限切れ | 赤 | 逃した。損をした |

- 締め切りの数日前からセルの色を変える／バッジを出す（何日前かは**未決定**）。
- 期限切れになったら、逃したことがプレイヤーに分かるように一度だけ通知する。黙って消さない。

---

## 5. データの持ち方（実装の入口・たたき台）

```csharp
public enum CalendarEntryKind { Application, Renewal, Vaccination, Event, Memo }
public enum CalendarEntryState { Locked, Available, Active, Expired }

public class CalendarEntry
{
    public int  ageDays;        // GameDate に合わせて通算日数で持つ
    public CalendarEntryKind  kind;
    public CalendarEntryState state;
    public string title;
    public string note;         // プレイヤーのメモ
}
```

- 時間との接続：`MonthTicker.OnMonthChanged` で月表示を更新、`DayTicker.OnDayPassed` で締め切りの判定。**自分で時間を数えない。** → [../code/architecture.md](../code/architecture.md) 1章
- 作業場所は `Assets/Kusao/`。自分の UI はプレハブ化して、他のメンバーとシーンが衝突しないようにする。
- 予定リストの表示は、既存の `StatusListUI` / `StatusRowUI` と同じ「データ → 行を生成」の形にすると揃う。スクロールの組み方は [../code/ui-scroll-patterns.md](../code/ui-scroll-patterns.md)。

---

## 6. 決めないといけないこと

- 締め切りの何日前から警告を出すか。
- 期限切れをどこまで厳しくするか（完全に受けられない／遅れて受給開始 など）。
- 自動で入る予定をどこまで最初から見せるか。全部見せると「知っていれば得をする」の驚きが減り、見せなさすぎると理不尽になる。
- プレイヤーのメモをセーブ対象にするか（現在の `SaveSystem` は4項目しか保存していない。→ [../code/architecture.md](../code/architecture.md) 5章）。
