---
title: カレンダー機能
updated: 2026-10-11
status: 実装中（画面・時間停止・制度カード連携・ホバー詳細ポップアップ実装済み）
related: [docs/design/support-systems.md, docs/design/support-apply.md, docs/design/events.md, docs/code/architecture.md, docs/code/ui-main-test-hud.md]
---

# カレンダー機能

> **主要な仕様は 2026-09-23 に決定済み**（[../decisions/0010-calendar-follows-reality.md](../decisions/0010-calendar-follows-reality.md) ほか）。2026-10-11 に制度カード連携・簡易表示・ホバー詳細ポップアップを実装（[../decisions/0018-calendar-plan-display-and-hover-popup.md](../decisions/0018-calendar-plan-display-and-hover-popup.md)）。担当は草尾。
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

`Main_test` 下部の「カレンダー」ボタンから開く（`MainTestHUD.OnCalendar` → `CalendarUI.ShowCalendar()`。実装は `Assets/Kusao/CalendarUI.cs`）。

> **実装済みの画面**（2026-09-23、[../decisions/0008-calendar-ui-implementation.md](../decisions/0008-calendar-ui-implementation.md)）：タイトル「〇歳 〇月」＋前月／翌月ボタン＋右上の✕、42マスの日付グリッド、今日のマスを強調、押した日を選択色にする。**曜日は表示しない**（1日は常に左上のマスから始まる）。2026-09-25、この形で確定。

```
カレンダー画面
├ ヘッダー        … 「3歳 5月」＋ 前月／翌月ボタン
├ 月グリッド      … 日付セル 7×6＝42マス固定（作り方は 6章）。セルの中に予定チップ（最大2〜3件＋「+2」）
│   └ 今日のセルを強調。期限が近い日は色を変える
├ 選択日の詳細    … その日の予定リスト（下部 or 右側）
│   └ 各予定：アイコン／タイトル／状態バッジ／[申請する] ボタン
└ メモ追加ボタン  … プレイヤーが自由に書き込む
```

- ゲーム内の暦は `GameDate` が持っている（0歳1月1日起点、1年365日、月の日数は実際の暦）。**自前で日付計算をしない**こと。→ [../code/architecture.md](../code/architecture.md) 2章
- **カレンダーを開いている間はゲーム内時間を止める**（実装済み）。`CalendarUI` の `OnEnable` / `OnDisable` で `GameClock.PushPause()` / `PopPause()` を呼んでいる（8.2）。→ [../decisions/0004-pause-during-decisions.md](../decisions/0004-pause-during-decisions.md)

### 2.1 日付を押してメモを付ける

**カレンダーの日付マスを押すと、その日にメモを付けられる**（2026-09-25 決定）。

- 当月の日付マスを押すと、その日のメモ入力欄が開く。`CalendarUI` にある `OnDaySelected(通算日数)` イベントを使う（押した日は選択色になるところまで実装済み）。
- 入力して確定すると、その日の通算日数と一緒に保存する。**保存と同時にオートセーブ**（8.3）。
- 0歳1月〜19歳12月のどの日にも付けられる（過去の日・先の日も可）。前月・翌月のグレーのマスは押せない（実装どおり）。
- メモがある日は、マスに印を付けて分かるようにする。【たたき台】印の形は画面で見て決める。
- 1日に付けられるメモは1つ。中身を空にして確定したら削除。【たたき台】
- メモは効果を持たない（3章）。
- メモを書いている間もカレンダーは開いたままなので、時間は止まったまま。


### 2.2 予定の書き込み（制度を選んで登録）【必須・2026-10-05 追加】

Google カレンダーのように、**プレイヤーが「どの制度を、いつまでに申請するか」を予定として書き込む**。2.1 のメモ（自由な文字）とは別に、制度と結びついた予定を作れる。

書き込みの流れ：

1. 日付マスを押す → その日の詳細に［＋予定を追加］
2. 種類を選ぶ：**申請**／**手続き**（毎年の更新など）／**メモ**（2.1 と同じ自由記入）
3. 申請・手続きのときは、**制度を一覧から選ぶ**（政府／地方自治体／会社のタブ。一覧は `SupportManager.I.Schemes` を読むだけ）
4. 通知のタイミングと「止めて知らせる」を選ぶ（2.4）
5. 確定 → マスに予定チップが出る。**オートセーブ**（8.3、メモと同じ）

- タイトルは制度名から自動で「児童手当を申請する」。プレイヤーが書き換えてもよい。
- 予定は**制度の id を持つ**（文字だけにしない）。これで 2.5 の完了チェックやサポート画面への移動ができる。
- 1日に複数の予定を置ける（メモは2.1のとおり1日1つ）。マスには最大2件＋「+2」。
- 予定の編集・削除：予定を押す → ［編集］［削除］。日付を変えるときは編集画面で日付を選び直す。
- 予定は効果を持たない（申請は `SupportManager.Apply` でしか起きない）。**予定を書いても申請したことにはならない**のがポイント。

データの形（たたき台）：

```csharp
public enum PlanKind { Apply, Renew, Memo }
public enum ReminderTiming { None, OneWeekBefore, TwoWeeksBefore, OneMonthBefore }

[System.Serializable]
public class CalendarPlan          // そのまま SaveData に入れる
{
    public string uid;             // 予定ごとの一意な id（編集・削除用。Guid 文字列）
    public int    ageDays;         // どの日の予定か（通算日数）
    public PlanKind kind;
    public string schemeId;        // Apply / Renew のとき：SupportScheme.id（Memo は空）
    public string title;           // 表示する文字（制度名から自動。書き換え可）
    public ReminderTiming reminder = ReminderTiming.OneWeekBefore;
    public bool   pauseOnRemind = true;   // 止めて知らせる（2.4）
    public bool   reminded;        // 通知を出したか（1回だけ出すため）
}

// SaveData に追加
public List<CalendarPlan> calendarPlans = new List<CalendarPlan>();
```

### 2.3 制度カードの「カレンダーに追加」ボタン【必須】

サポート画面の制度カードに［📅 カレンダーに追加］を付ける。押すと、**その制度の締め切り日が入った状態で 2.2 の書き込み画面が開く**（種類＝申請、制度＝そのカード）。

- 締め切りがない制度は、今日の日付が入る。
- 毎年の手続きがある制度は、今年の期限日が入り、種類＝手続き。
- 役割分担：ボタンはサポート画面（山口）に置き、押されたら `CalendarUI` 側の公開メソッドを呼ぶ。

  ```csharp
  // CalendarUI（草尾）に用意してもらう窓口
  public static void OpenAddPlan(string schemeId, int ageDays, PlanKind kind);
  ```

- 自分で調べて書く遊びは残しつつ、締め切り日を写す手間だけ省く。

### 2.4 通知（リマインダー）【必須・2026-10-05 改訂】

**1日＝1秒で進むので、当日や数日前の通知では間に合わない**（読んでいる間に過ぎる）。そこで、

1. 通知は**早め**（既定 1週間前）にする
2. 通知が来たら**時間を止めて知らせる**（ポップアップ）

の2つで「通知を見たのに間に合わない」を防ぐ。

| 予定ごとに選ぶ項目 | 選択肢 | 既定 |
|---|---|---|
| いつ知らせるか | なし／1週間前／2週間前／1か月前 | 1週間前 |
| 止めて知らせる | ON／OFF | 申請・手続きは ON、メモは OFF |

**止めて知らせる＝ON のとき**（通知の日になったら）：

```
┌ 📅 予定のお知らせ ─────────────────┐
│ 1週間後：児童手当を申請する          │
│ （期限 0歳 4月30日）                 │
│ [サポートで申請] [カレンダーを開く] [閉じる] │
└────────────────────────────┘
```

- 出ている間は時間が止まる（`OnEnable` で `GameClock.I.PushPause()`、`OnDisable` で `PopPause()`。AGENTS.md ルール8）。
- ［サポートで申請］→ サポート画面のその制度のカードを開く（`SupportPanelController.Open(schemeId)`、山口）。メモの予定にはこのボタンを出さない。
- ［カレンダーを開く］→ カレンダーのその日を開く。
- ［閉じる］→ 時間が再開する。
- 同じ日に複数の通知が来たら、**1つのポップアップにまとめて並べる**（何回も止めない）。
- **他の判断画面（イベント・食事選択・サポート画面など）が開いている間は出さず、閉じたら出す**（順番待ち）。判断画面が開いているかは `GameClock` の停止カウント（`pauseRequests`）で分かる。今は private なので、`GameClock` に `public bool IsPauseRequested => pauseRequests > 0;` を足す（Core・山口）。0 になったら出す。

**止めて知らせる＝OFF のとき**：時間は止めず、画面上部に短い通知（3秒）。押すとカレンダーのその日を開く。

共通：

- 判定は `DayTicker.OnDayPassed` で「今日 ≥ 予定の日 − 何日前」かつ `reminded == false` の予定を探す。出したら `reminded = true`。
- 予定を書いた時点ですでに通知の日を過ぎていたら（例：3日後の予定に1週間前通知）、**次の日にすぐ出す**。
- カレンダーを閉じていても通知は出るので、判定は常にシーンにある部品（`CalendarReminder`）で行う。
- 予定の日を過ぎた予定は消さない（2.5 を作ったら「未申請」表示）。

### 2.5 完了チェック・サポート画面への移動【時間があれば】

- **完了チェック**：申請・手続きの予定は `SupportManager.I.GetState(制度)` を読み、申請済み（受け取り済み／受給中）なら予定に ✓ を付ける。予定の日を過ぎても未申請なら赤で「未申請」。`SupportManager.OnStateChanged` で描き直す。
- **サポート画面へ移動**：予定の［申請する］を押すと、サポート画面のその制度のカードを開く（申請はサポート画面で行う）。サポート画面側に `SupportPanelController.Open(schemeId)` を用意する（山口）。

---

## 3. カレンダーに載るもの

| 種類 | 誰が入れる | 例 | 期限を過ぎると |
|---|---|---|---|
| 申請期限 | **プレイヤーが書く**（2.2／カードから 2.3）。自動では載らない（2026-10-06 決定） | 児童手当の申請、幼保無償化の手続き | その制度を受けられない／受給開始が遅れる |
| 定期手続き | **プレイヤーが書く**（種類＝手続き。繰り返しは将来） | 就学援助の申請し直し（毎年4月） | 受給が停止・失効する |
| 予防接種 | 自動（最初から載っている） | 定期接種のスケジュール | 体調イベントの発生率が下がらないまま |
| 行事・イベント | 自動 | 運動会、学校行事 | 子の趣味・ストレスに影響 |
| プレイヤーのメモ | 手動 | 「来月は高級食にする」「保育園の見学」 | 何も起きない（自分用の備忘） |
| プレイヤーの予定（申請・手続き） | 手動（2.2）／制度カードから（2.3） | 「児童手当を申請する」 | 何も起きない。申請しないまま過ぎると 2.5 で「未申請」 |

- 申請できる期間は、**制度を1つ追加するときに Inspector で設定する**（決定済み → [support-systems.md](support-systems.md) 3.5）。
  - 開始日：歳・月・日（例：0歳1月1日）
  - 期限：なし／1回だけ（歳・月・日）／毎年（○月○日）
  - ~~カレンダーは制度データを読んでその日のマスに予定を出す~~ → **廃止（2026-10-06）**。期限は制度カード（サポート画面）に表示し、カレンダーには 2.3 のボタンで締め切り日を写す。
- **制度の締め切りはカレンダーに自動では載せない**（2026-10-06 決定 → [../decisions/0016-calendar-shows-only-player-plans.md](../decisions/0016-calendar-shows-only-player-plans.md)）。現実でも期限は自分で調べて手帳やカレンダーに書く。調べる場所＝制度カード、書く場所＝カレンダー、という役割分担にする。
- 予防接種・行事の扱い（自動で載せるか）は、制度とは別に今後決める。
- **突然のイベント（[events.md](events.md)）はカレンダーに載せない。** ゲームの驚きはイベントで出す。
- **手動メモは効果を持たない。** 効果を持たせると「メモしないと損」になり、作業感が出る。

---

## 4. 状態と締め切りの扱い

[support-systems.md](support-systems.md) 3章の状態表示と揃える。

| 状態 | 色（たたき台） | 意味 |
|---|---|---|
| 条件未達 | グレー | まだ対象外（例：子が3歳になるまで） |
| 申請できる | オレンジ | 条件を満たした。**締め切りあり** |
| 受給中 | 緑 | 申請済みで効果が出ている |
| 支給停止 | 赤 | 毎年の手続きを逃して止まっている。手続きすれば再開 |
| 期限切れ | 赤 | 逃した。損をした |

- **締め切りの10日前から**セルの色を変える／バッジを出す（決定済み）。10 は Inspector で変えられるようにする（`[SerializeField] int warnDaysBefore = 10`）。
- 期限日の**その日のうちは申請できる**。翌日になったら期限切れ（`DayTicker.OnDayPassed` で判定）。
- 期限切れになったら、逃したことがプレイヤーに分かるように一度だけ通知する。黙って消さない。

### 期限切れの扱い（現実の制度と同じ・決定済み）

| 期限の種類 | 過ぎるとどうなるか | 現実の例 |
|---|---|---|
| 1回だけ | その制度はもう申請できない | 出産育児一時金（出産の翌日から2年で時効） |
| 受給中は毎月もらう制度 | 受給は**申請した月の翌月分から**。申請が遅れた月の分はさかのぼってもらえない（期限前でも遅いほど損） | 児童手当 |
| 毎年 | 支給が止まる（支給停止）。後から手続きすれば再開し、止まっていた分も受け取れる。**期限から2年**手続きしないと受給資格がなくなる | 児童扶養手当の現況届 |

- 制度ごとの違いは Inspector で設定する（効果のタイミング、資格を失うまでの年数 → [support-systems.md](support-systems.md) 3.5）。

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

- **プレイヤーのメモはセーブする**（決定済み）。メモを書いたときに自動でセーブする（8.3）。`SaveData` に次を足す（[../code/architecture.md](../code/architecture.md) 5章）。

  ```csharp
  [System.Serializable]
  public class MemoSave
  {
      public int    ageDays;   // どの日のメモか（通算日数）
      public string text;
  }

  // SaveData に追加
  public List<MemoSave> calendarMemos = new List<MemoSave>();
  ```

  `JsonUtility` は `List<中身が [Serializable] のクラス>` をそのまま保存できる。
- 制度の状態（申請済み・受給中・支給停止など）も、セーブしないとロードで消える。`SupportScheme.id` をキーにして同じく `SaveData` に持たせる（制度も草尾の担当なので、メモと一緒に作る → 8.3）。
- 時間との接続：`MonthTicker.OnMonthChanged` で月表示を更新、`DayTicker.OnDayPassed` で締め切りの判定。**自分で時間を数えない。** → [../code/architecture.md](../code/architecture.md) 1章
- 作業場所は `Assets/Kusao/`。自分の UI はプレハブ化して、他のメンバーとシーンが衝突しないようにする。
- 予定リストの表示は、既存の `StatusListUI` / `StatusRowUI` と同じ「データ → 行を生成」の形にすると揃う。スクロールの組み方は [../code/ui-scroll-patterns.md](../code/ui-scroll-patterns.md)。

---

## 6. 画面の作り方（Qiita 記事の方式で作る）

> ⚠ **実装はこの章の手順とは一部違う形で完成している**（`Assets/Kusao/CalendarUI.cs`）。UI はスクリプトが自動で組み立てる形、**曜日は使わない**（1日は常に左上から）、時間停止は `PushPause` / `PopPause`。この章は実装前に書いた手順書として残す。**曜日まわり（`BirthDayOfWeek` / `DayOfWeek`・土日の色分け・1日の曜日ぶんずらす処理）は採用しなかった。**

> **月グリッドは、Qiita の記事「[Unity でカレンダーを作る（hire1105）](https://qiita.com/hire1105/items/b23e905bbcc062f70376)」の方式で作ることに決定。**
> ただし記事は `System.DateTime`（現実の暦）で日付を出しているので、このゲームでは日付計算を **`GameDate` に置き換える**。下の手順とコードはその置き換え済みのもの。

### 6.1 記事の方式の要点

1. 日付ボタン（`Button` ＋ 子に文字）を**プレハブ**にする。
2. `GridLayoutGroup` を付けた親オブジェクトの下に、そのボタンを **42個（7列 × 6行）** 生成する。並べるのは `GridLayoutGroup` に任せる。
3. 表示する月の **1日の曜日** を求め、その分だけ先頭のマスを空ける（＝前月の日付をグレーで入れる）。
4. 1日から月末まで日付を書き込み、余ったマスには翌月の日付をグレーで入れる。
5. 日曜は赤、土曜は青、それ以外は黒。
6. 当月の日付ボタンにだけ `onClick` を登録する（`RemoveAllListeners()` してから `AddListener`）。前月・翌月のマスは押しても何も起きない。

42マスで固定なのは、どの月でも「1日が土曜 ＋ 31日」まで収まる最大サイズだから。月ごとに行数を変えなくてよいので、作りが単純になる。

### 6.2 記事から変えるところ（なぜ変えるか）

| 記事 | このプロジェクト | 理由 |
|---|---|---|
| `DateTime.Now` / `DateTime.DaysInMonth` / `DayOfWeek` | `GameDate` を使う | ゲーム内の暦は「0歳1月1日起点・1年365日・うるう年なし」で現実の日付と無関係。自前計算も禁止（2章）。 |
| `UnityEngine.UI.Text` | `TextMeshProUGUI` | プロジェクトの他の UI（`DayUI` など）が TMP。 |
| 毎回 `GameObject.Find("GameObject")` で探す | 生成したボタンを `List` に持っておく | 名前に依存すると、他のメンバーのオブジェクト名と衝突して壊れる。毎回探すのも遅い。 |
| 曜日を `switch` で 0〜6 に変換 | マスの列番号 `i % 7` がそのまま曜日 | 1日の曜日ぶんずらして並べているので、列 0 = 日曜、列 6 = 土曜になる。 |
| 前月／翌月ボタンなし | ヘッダーに前月／翌月ボタンを付ける | 2章の画面構成に必要。記事には無いので自分で足す。 |
| `set_Date` で `Debug.Log` | 選択した日の**通算日数**をイベントで通知 | 5章のデータ（`CalendarEntry.ageDays`）と同じ単位にそろえる。 |

### 6.3 GameDate に足す関数（決定済み）

今の `GameDate` には「歳・月・日 → 通算日数」と「その月の日数」と「曜日」を出す関数が無い。カレンダー側で計算すると「自前で日付計算をしない」に反するので、**`GameDate` に下の3つを追加する**（2026-09-23 決定）。

- **`GameState` ではなく `GameDate` に置く理由**：どれも「日数 ↔ 日付」の変換だけで、ゲームの状態を持たない。`GameDate` は static なので、`GameState.I` がまだ無いタイミング（シーン読み込み直後・エディタ上のテスト）でも呼べる。暦の決まり（月の日数の表）も `GameDate` にしかないので、ここに置けば暦の計算が1か所にまとまる。
- 1ヶ月は実際の暦の日数（[../decisions/0009-real-calendar-months.md](../decisions/0009-real-calendar-months.md)）。

```csharp
// Assets/Yamaguchi/Scripts/Core/GameDate.cs に追加

// 生まれた日（0歳1月1日）の曜日。0=日 … 6=土。月曜日に決定（2026-09-23）
public const int BirthDayOfWeek = 1;

// その月の日数（うるう年なし）
public static int DaysInMonth(int month)
{
    return MonthDays[month - 1];
}

// 歳・月・日 → 通算日数（FromAgeDays の逆）
public static int ToAgeDays(int year, int month, int day)
{
    int total = year * DaysPerYear;
    for (int i = 0; i < month - 1; i++) total += MonthDays[i];
    return total + (day - 1);
}

// 通算日数 → 曜日（0=日 … 6=土）
public static int DayOfWeek(int ageDays)
{
    return (ageDays + BirthDayOfWeek) % 7;
}
```

- ゲーム内の暦には本来「曜日」が無い。記事どおり土日を色分けするために、**生まれた日（0歳1月1日）を月曜日**として 7 日周期で回す（決定済み → [../decisions/0012-birth-day-is-monday.md](../decisions/0012-birth-day-is-monday.md)）。
  - 例：0歳1月1日＝月曜なので、カレンダーの 0歳1月は日曜の列を 1 マス空けて、月曜の列から 1日が始まる。
  - 1年が 365日（52週＋1日）なので、翌年の1月1日は1つずつ曜日がずれる（1歳1月1日＝火曜）。

### 6.4 見られる範囲

前月／翌月ボタンで **0歳1月〜19歳12月まで全部見られる**（2026-09-23 決定）。20歳ちょうど（7300日）でゲームが終わるので、それより先の月は無い。先の月の予定も最初から全部見える（3章・決定済み）。

### 6.5 Unity での組み立て手順

作業場所は `Assets/Kusao/`。シーンに直接置かず、最後にプレハブ化する（5章）。

1. **日付ボタンのプレハブを作る**
   - Hierarchy で右クリック → UI → Button - TextMeshPro。名前を `DayButton` にする。
   - `Image` の Source Image は**白無地の画像**に差し替える（記事の注意点：既定の丸角画像が 42 個並ぶと見づらい）。
   - 子の `Text (TMP)` は中央寄せ、フォントサイズは適当に（セルに収まれば良い）。
   - `Assets/Kusao/Prefabs/DayButton.prefab` にドラッグして保存し、シーンからは消す。
2. **カレンダーのパネルを作る**（Canvas の下）
   ```
   CalendarPanel            … CalendarView.cs を付ける
   ├ Header                 … HorizontalLayoutGroup
   │  ├ PrevButton          … 「＜」
   │  ├ MonthLabel (TMP)    … 「3歳 5月」
   │  └ NextButton          … 「＞」
   ├ WeekdayRow             … GridLayoutGroup（7列）。日〜土の TMP を 7 個手で置く
   ├ DayGrid                … GridLayoutGroup。中身は空（スクリプトが 42 個生成する）
   └ CloseButton
   ```
3. **`DayGrid` の `GridLayoutGroup` を設定する**（数値は【たたき台】）
   - Constraint：**Fixed Column Count = 7**（これで必ず 7 列になる）
   - Cell Size：例 `100 × 80`、Spacing：例 `4 × 4`
   - Start Corner：Upper Left、Start Axis：Horizontal、Child Alignment：Upper Center
   - `WeekdayRow` も同じ Cell Size の幅・Spacing にしておくと、曜日と日付の列がそろう。
4. **`CalendarView.cs` を `CalendarPanel` に付けて、Inspector で参照を入れる**
   - Grid ← `DayGrid` / Day Button Prefab ← `DayButton` プレハブ / Month Label / Prev・Next Button
5. **開閉をつなぐ**：`Main_test` 下部の「カレンダー」ボタン（今は `Debug.Log` の仮ハンドラ）で `CalendarPanel.SetActive(true)`、`CloseButton` で `SetActive(false)`。
6. `CalendarPanel` を `Assets/Kusao/Prefabs/` にプレハブ化する。

### 6.6 スクリプト（たたき台）

```csharp
// Assets/Kusao/CalendarView.cs
// 方式：Qiita「Unity でカレンダーを作る」(hire1105) の 42 マス方式。
// 日付計算は System.DateTime ではなく GameDate を使う。
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CalendarView : MonoBehaviour
{
    private const int CellCount = 42;   // 7列 × 6行

    [Header("参照")]
    [SerializeField] private Transform grid;          // GridLayoutGroup を付けた DayGrid
    [SerializeField] private Button dayButtonPrefab;  // DayButton プレハブ
    [SerializeField] private TMP_Text monthLabel;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;

    [Header("色")]
    [SerializeField] private Color sundayColor     = Color.red;
    [SerializeField] private Color saturdayColor   = Color.blue;
    [SerializeField] private Color weekdayColor    = Color.black;
    [SerializeField] private Color otherMonthColor = Color.gray;
    [SerializeField] private Color cellColor       = Color.white;
    [SerializeField] private Color todayCellColor  = new Color(1f, 0.95f, 0.6f);

    /// <summary>日付マスが押されたとき。引数はその日の通算日数（GameState.AgeDays と同じ単位）。</summary>
    public event Action<int> OnDaySelected;

    private readonly List<Button>   cells  = new List<Button>();
    private readonly List<TMP_Text> labels = new List<TMP_Text>();

    private int viewYear;   // 表示中の歳
    private int viewMonth;  // 表示中の月（1〜12）

    private void Awake()
    {
        // 記事の Start() と同じ：ボタンを 42 個生成。ただし参照をリストに持っておく
        for (int i = 0; i < CellCount; i++)
        {
            Button cell = Instantiate(dayButtonPrefab, grid);
            cells.Add(cell);
            labels.Add(cell.GetComponentInChildren<TMP_Text>());
        }

        prevButton.onClick.AddListener(() => ShiftMonth(-1));
        nextButton.onClick.AddListener(() => ShiftMonth(+1));
    }

    private void OnEnable()
    {
        if (GameState.I == null) return;

        // 開いたら「今日」の月を表示する
        int day;
        GameDate.FromAgeDays(GameState.I.AgeDays, out viewYear, out viewMonth, out day);
        Refresh();

        // 時間を止める：実装では OnEnable で GameClock.PushPause()、OnDisable で PopPause() を呼ぶ（8.2）
    }

    private void ShiftMonth(int delta)
    {
        viewMonth += delta;
        if (viewMonth < 1)  { viewMonth = 12; viewYear--; }
        if (viewMonth > 12) { viewMonth = 1;  viewYear++; }
        if (viewYear < 0)   { viewYear = 0;   viewMonth = 1; }    // 生まれる前には戻れない
        if (viewYear > 19)  { viewYear = 19;  viewMonth = 12; }   // 20歳（=ゲーム終了）より先は無い
        Refresh();
    }

    // 記事の CalendarController() に相当
    private void Refresh()
    {
        monthLabel.text = viewYear + "歳 " + viewMonth + "月";

        int firstAgeDays = GameDate.ToAgeDays(viewYear, viewMonth, 1);
        int startday     = GameDate.DayOfWeek(firstAgeDays);          // 1日の曜日 = 先頭の空きマス数
        int monthEnd     = GameDate.DaysInMonth(viewMonth);
        int prevMonthEnd = GameDate.DaysInMonth(viewMonth == 1 ? 12 : viewMonth - 1);
        bool isFirstMonth = (viewYear == 0 && viewMonth == 1);         // 前月＝生まれる前
        int today        = Mathf.FloorToInt(GameState.I.AgeDays);

        for (int i = 0; i < CellCount; i++)
        {
            Button   cell  = cells[i];
            TMP_Text label = labels[i];
            cell.onClick.RemoveAllListeners();
            cell.image.color = cellColor;

            int dayNum = i - startday + 1;   // このマスが当月の何日か（範囲外なら前月・翌月）

            if (dayNum < 1)
            {
                // 前月の日付（グレー・押せない）
                label.text  = isFirstMonth ? "" : (prevMonthEnd + dayNum).ToString();
                label.color = otherMonthColor;
                cell.interactable = false;
            }
            else if (dayNum > monthEnd)
            {
                // 翌月の日付（グレー・押せない）
                label.text  = (dayNum - monthEnd).ToString();
                label.color = otherMonthColor;
                cell.interactable = false;
            }
            else
            {
                // 当月の日付
                int ageDays = firstAgeDays + (dayNum - 1);   // ラムダに渡すのでループ内のローカル変数にする
                int column  = i % 7;                          // 0=日 … 6=土

                label.text  = dayNum.ToString();
                label.color = column == 0 ? sundayColor
                            : column == 6 ? saturdayColor
                            : weekdayColor;
                if (ageDays == today) cell.image.color = todayCellColor;

                cell.interactable = true;
                cell.onClick.AddListener(() => OnDaySelected?.Invoke(ageDays));
            }
        }
    }
}
```

- `ageDays` をループ内のローカル変数にしているのは、ラムダがループ変数をそのまま掴むと全部のボタンが同じ日を返してしまうため（記事でも `tmp` に一度コピーしている）。
- 開いている間は時間が止まる前提（2章）なので、表示中に日付が進んだときの再描画は入れていない。止める仕組みが入る前に試すと「今日」の強調がずれるだけで、壊れはしない。
- 次の段階：`Refresh()` の当月マスで、その日の `CalendarEntry`（5章）を探して予定チップを出す。`OnDaySelected` を「選択日の詳細」パネルが購読して予定リストを出す。

---

## 7. 決めないといけないこと

- 状態の色（4章）とグリッドのサイズ（6.5）はたたき台。実際に画面で見て決める。
- それ以外の仕様は決定済み（2026-09-23）。実装の前に作るものは 8章。
- ~~申請期限を自動でカレンダーに出し続けるか~~ → **書いたものだけ載せる（案B）に決定（2026-10-06）**。3章を修正済み。
- 予防接種・行事を自動で載せるか。

---

## 8. 残りの作業（担当：草尾）

1・2 は実装済み。残りは 3〜8（5〜7 は 2026-10-05 追加・必須、8 は時間があれば）。

| # | 作るもの | 状態 | 詳しくは |
|---|---|---|---|
| 1 | `GameDate` に日付計算の関数を足す（`DaysInMonth` / `ToAgeDays`） | **実装済み** | 8.1 |
| 2 | 時間を止める仕組み（`PushPause` / `PopPause`） | **実装済み** | 8.2 |
| 3 | メモと予定・制度の状態のセーブ（`ISaveParticipant`） | **予定・制度は実装済み**（メモは画面と一緒） | 5章、8.3 |
| 4 | 日付を押してメモを付ける画面 | 未着手（日付を押すと選択されるところまでは実装済み） | 2.1 |
| 5 | 予定の管理・カレンダー簡易表示・ホバー詳細ポップアップ | **実装済み** | 2.2、0018 |
| 6 | 制度カードから追加する窓口 `CalendarUI.OpenAddPlan` / `AddPlanFromScheme` | **実装済み** | 2.3 |
| 7 | 通知（リマインダー） | 未着手・**必須** | 2.4 |
| 8 | 完了チェック・サポート画面への移動 | 未着手・時間があれば | 2.5 |

### 8.1 GameDate の関数（実装済み）

- `DaysInMonth(月)` … その月の日数
- `ToAgeDays(歳, 月, 日)` … 歳・月・日 → 通算日数（`FromAgeDays` の逆）
- 曜日を出す `DayOfWeek` は、曜日を使わないことになったので作らない。

### 8.2 時間を止める仕組み（実装済み）

`GameClock` に `PushPause()` / `PopPause()` が入っていて、`CalendarUI` の `OnEnable` で `PushPause()`、`OnDisable` で `PopPause()` を呼んでいる（2026-09-23 実装、2026-09-25 この形で確定 → [../decisions/0004-pause-during-decisions.md](../decisions/0004-pause-during-decisions.md)）。

- 表示・非表示のタイミングで呼んでいるので、×ボタンなどどの閉じ方でも必ず解除される。
- 他の判断画面（制度一覧・食事選択・イベントの行動選択）も、同じように `OnEnable` / `OnDisable` で呼ぶ。
- 画面を開閉するのに `GameClock.SetRunning` を直接呼ばない（ゲームオーバー専用）。

### 8.3 メモと制度の状態のセーブ

**セーブ機能の本体（ステータス・日時などを保存して、ゲームを続きから遊べるようにする）は山口が作る**（2026-09-23 決定）。草尾が作るのは、カレンダーと制度に関わる部分だけ。

| 担当 | 作るもの |
|---|---|
| 山口 | `SaveSystem` の本体。お金・日時・ステータスなどの保存と、「続きから」の読み込み |
| 草尾 | メモと制度の状態（申請済み・受給中・支給停止など）を保存用の形に変える部分。`SaveData` にその項目を足す |

- **メモはオートセーブ**（2026-09-23 決定）。メモを書いた・直した・消したときに、その場で自動的にセーブする。プレイヤーがセーブボタンを押さなくても消えない。
- オートセーブでは `SaveSystem.Save()` を呼んで**ゲーム全体を1つのセーブにまとめて保存**する。メモだけ別の場所に保存すると、ロードしたときに「メモは新しいのに日付は古い」というずれが起きるため。
- 制度の状態は `SupportScheme.id` をキーにして保存する（アセットの中には書かない → [support-systems.md](support-systems.md) 3.5）。
- 保存する形（5章の `MemoSave` など）を `SaveData` に足すときは、山口さんと項目名を合わせる。
