---
title: セーブ機能（続きから遊べるようにする）
updated: 2026-10-31
status: 設計中
related: [docs/team/antigravity-save-system.md, docs/design/title-screen.md, docs/decisions/0019-two-save-slots.md, docs/code/architecture.md, docs/design/support-apply.md, docs/design/calendar-system.md, docs/design/food-system.md, docs/team/agreements-needed.md]
---

# セーブ機能（続きから遊べるようにする）

> ゲームの状態を保存し、次に起動したときに「続きから」遊べるようにする。担当は山口（AGENTS.md）。
> 今の `SaveSystem` は お金・世帯ストレス・年齢・バランスの4つしか保存していない。**家族全員の状態・支援制度・年収の記録まで全部保存する**（2026-10-09 決定）。

---

## 1. 保存するもの

### 1.1 山口の担当分（2026-10-09 決定：すべて保存する）

| まとまり | 保存する値 | 今どこにあるか |
|---|---|---|
| ゲーム全体 | お金、通算日数（年齢）、ゲームオーバー済みか | `GameState` |
| 母・父（それぞれ） | ストレス、役職、ワークライフバランス、積み立て中の給料、特性、状態（残り日数つき） | `ParentStatus`（`PersonStatus`） |
| 子ども | ストレス、成績、体調、勉強⇔遊びのバランス、趣味と趣味の経験値、特性、状態（残り日数つき） | `ChildStatus`（`PersonStatus`） |
| 支援制度 | 制度ごとの状態・申請日・最後に払った月・未支給の積立額・手続き済みの歳・支給停止の情報 | `SupportManager.ToSave()`（作成済み） |
| 年収の記録 | 直近12か月の給料（額面） | `IncomeHistory.ToSave()`（作成済み） |
| 家出の数え | 子どもが限界ストレスのまま何日たったか | `RunawayWatcher.dangerDayCount` |
| （将来）働き方 | 産休・育休・時短の状態、休む前のバランス | `ParentStatus`（company-support.md） |
| （将来）家計簿・赤字の月数 | 直近の家計簿、赤字が続いた月数 | expenses.md |

### 1.2 他のメンバーの担当分（各自に作ってもらう・要相談）

| 担当 | 保存したいもの（今のコードから見た候補） |
|---|---|
| 有坂（食事） | 今選んでいる食事のランク、最後に食費を引いた日、今月引いた額、最初の食事選択を出したか（`MealSystemManager`） |
| 草尾（カレンダー） | メモ、予定（`CalendarPlan`）、リマインダーを出したか（calendar-system.md 8.3） |
| 丸山（イベント） | **実装済み**（`EventManager`・キー `"event"`）。起きた固定イベントの記録、今月の抽選済みの印と予約、直前に出したイベント、発生率の補正（[events.md](events.md) 7章） |

→ 他の人のコードを山口が直接いじらずに済むよう、**それぞれが自分の分を保存・復元できる仕組み**（2章）にする。

### 1.3 保存しないもの

- 制度アセット・特性／状態の定義・イベントの中身など **アセット（ScriptableObject）の中身**。保存するのは「どれか」を表す **id だけ**。
- 画面の開き具合（サポート画面のタブ、スクロール位置など）。

---

## 2. 仕組み

### 2.1 担当ごとに「セーブの参加者」になる

```csharp
/// セーブに参加する部品。自分の分を文字列（JSON）にして渡し、ロードのときに受け取って戻す。
public interface ISaveParticipant
{
    string SaveKey { get; }              // 例 "support", "meal", "calendar"（他と重ねない・後から変えない）
    string Capture();                    // 今の状態を JSON にして返す
    void   Restore(string json);         // JSON から状態を戻す（キーが無かったら呼ばれない）
}
```

- 参加者は `OnEnable` で `SaveSystem.Register(this)`、`OnDisable` で `Unregister(this)` する。
- `SaveSystem` は登録された参加者を全部回して保存する。**`SaveSystem` は他の人の型を知らない**ので、メンバーが自分の分を足すときに `SaveSystem` を直す必要がない。

### 2.2 セーブデータの形

```csharp
[System.Serializable]
public class SaveData
{
    public int    version = 1;           // データの形を変えたら上げる（古いセーブを読むとき用）
    public string savedAt;               // 保存した現実の日時（表示用）
    public float  ageDays;               // 先に戻す必要がある（2.4）
    public List<SaveSection> sections = new List<SaveSection>();
}

[System.Serializable]
public class SaveSection { public string key; public string json; }
```

- 1枠につき1つのファイル（`Application.persistentDataPath` の下）に保存する。今の PlayerPrefs より中身を開いて確かめやすい。
- セーブ枠は **オート（`save_auto.json`）と手動（`save.json`）の2つ**（2026-10-14 変更。[ADR 0019](../decisions/0019-two-save-slots.md)）。

### 2.3 特性・状態は id で保存する

特性・状態はアセット（`TraitDefinition` / `ConditionDefinition`）を指しているので、そのままでは保存できない。

- 保存：アセットの `id` を保存する（状態は id と残り日数）。
- 復元：id からアセットを探す **一覧（カタログ）** が要る。`DefinitionCatalog`（ScriptableObject）に全部の特性・状態を登録し、`Resources` に置く。
- 新しい特性・状態を作ったら、カタログにも登録する（エディタのメニューで自動で集める道具を作る）。

### 2.4 ロードの順番（大事）

`DayTicker` は「前の日から今日までを1日ずつ通知する」ので、ロードで年齢を一気に変えると、**何百日分の給料・制度の判定・イベントが一度に起きてしまう**。そうならないように：

1. 時間を止める（`GameClock.PushPause`）。
2. `DayTicker` と `MonthTicker` に「今日はこの日」と教える（`Resync(ageDays)` を足す）。**通知は出さない**。
3. `GameState` の年齢・お金を戻す。
4. 各参加者の `Restore` を呼ぶ（家族 → 年収の記録 → 支援制度 → 食事 → カレンダー の順）。
5. 画面を描き直す（各部品は値が変わったイベントで自分を更新する）。
6. 時間を戻す（`PopPause`）。

---

## 3. いつ保存するか・いつ読むか（2026-10-09 決定）

| 場面 | 決定 |
|---|---|
| 自動セーブ | **月が変わったとき**（給料・支給・食事が全部終わったあと）。メモを書いたときも保存（calendar-system.md 8.3）。どちらも **オート枠** |
| 手動セーブ | 画面下の［セーブ］ボタンで保存し、「セーブしました」の通知を出す。**手動枠** |
| 続きから／はじめから | タイトル画面（[title-screen.md](title-screen.md)）。続きからでオート／手動のどちらから再開するか選ぶ。はじめからではセーブを消さない（2026-10-14） |
| セーブ枠 | ~~1つ~~ → **オートと手動の2つ**（2026-10-14 変更。[ADR 0019](../decisions/0019-two-save-slots.md)） |
| ゲームオーバー・クリア | セーブを消す（**2枠とも**。はじめからになる）。終わった育成は育成記録に残す（title-screen.md 6章・本編後） |
| ロードしたときの食事画面 | **今月まだ選んでいないときだけ出し直す**（2026-10-13 決定。理由と作り方は [../team/request-arisaka-meal-save.md](../team/request-arisaka-meal-save.md) 3章） |

---

## 4. 作る順番

> 実装法と Antigravity への依頼文（3回分）は [../team/antigravity-save-system.md](../team/antigravity-save-system.md)。


1. 仕組み：`ISaveParticipant`・`SaveData`・`SaveSystem`（ファイル保存）・`DayTicker`/`MonthTicker` の `Resync`。
2. 山口の担当分の参加者：`GameState`、家族（`Household` が母・父・子をまとめて）、`IncomeHistory`、`SupportManager`、`RunawayWatcher`。
3. 特性・状態のカタログ。
4. ［セーブ］ボタンと月替わりの自動セーブ、確認用のロードの呼び方（「続きから」はタイトル画面で）。
5. 他のメンバーに参加者の作り方を伝える（2.1 の形と例）。

### 確認

1. 0歳3月まで進め、児童手当を申請、母のスライダーをワーク寄り、子どもに状態「風邪気味」を付ける → セーブ。
2. Play を止めて、もう一度 Play → 確認用のロードを呼ぶ（タイトル画面ができたら「続きから」）。
3. 日付・お金・両親の役職とバランスとストレス・積み立て中の給料・子どもの成績と体調・風邪気味の残り日数・児童手当の「受給中」と次回支給日が、全部セーブしたときと同じ。
4. ロードした直後に、給料やイベントがまとめて起きていない。

---

## 5. 決めること

- ~~自動セーブ・続きからの画面・セーブ枠~~ → 3章で決定（2026-10-09）。
- ~~タイトル画面の作り~~ → [title-screen.md](title-screen.md)（2026-10-14）。
