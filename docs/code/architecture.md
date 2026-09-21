---
title: アーキテクチャ（時間・お金・親ステータス）
updated: 2026-09-21
status: 実装済み（一部未実装）
related: [docs/design/rules-and-stats.md, docs/code/ui-main-test-hud.md]
---

# アーキテクチャ（時間・お金・親ステータス）

> **実際のコードを読んで確認した内容。ゲームロジックに触る前に必ず読むこと。**
> 対象：`Assets/Yamaguchi/Scripts/Core/`
> 仕様側の意図は [../design/rules-and-stats.md](../design/rules-and-stats.md)。

---

## 1. 全体の流れ（これだけは覚える）

時間は1本の流れで下に伝わる。**日次・月次の処理を足したいときは、自分で時間を数えずに `DayTicker` / `MonthTicker` を購読する。**

```
GameClock （実時間 1秒 = ゲーム内 1日）
   └─ GameState.AddAgeDays(1)
         └─ OnAgeChanged
              ├─ DayTicker    → OnDayPassed(通算日数)
              │     ├─ WageAccrual        … 各親の日給を積み立て
              │     └─ ParentStressSystem … 各親のストレスを増減
              └─ MonthTicker  → OnMonthChanged(歳, 月)
                    └─ HouseholdIncome    … 給料日。積立分を GameState.AddMoney
```

- `DayTicker` / `MonthTicker` はどちらも取りこぼし対策済み（複数日まとまって進んでも1日ずつ処理する）。
- `GameClock.Tick()` は現在 **`AddAgeDays(1f)` を呼ぶだけ**。お金・ストレスの増減はここではなく、上記の各システムが担当している。

---

## 2. 時間まわり

| クラス | 役割 |
|---|---|
| `GameClock` | 実時間 `secondsPerDay`（既定 1秒）ごとに1日進める。`SetRunning(bool)` で一時停止 |
| `GameDate` | 経過日数 → 歳・月・日 に変換する static ユーティリティ。`FromAgeDays()` / `Format()` |
| `DayTicker` | `OnDayPassed(int 通算日数)` |
| `MonthTicker` | `OnMonthChanged(int 歳, int 月)` |

**`GameDate` の暦**：生まれた日を 0歳1月1日として数える。1年 = 365日で、月の日数は実際の暦（31/28/31/30…）。うるう年は無い。

> ⚠ **仕様書との差**：[../design/rules-and-stats.md](../design/rules-and-stats.md) は「30日 = 1ヶ月」と書いているが、実装は**実際の暦の月**（28〜31日）。日給の計算基準 `ParentStatus.WageDaysPerMonth` だけが 30 固定なので、2月は 28日分しか積み立たず月給より少なくなる。仕様書側を実装に合わせるか、実装を30日固定にするかは**未決定**。

---

## 3. GameState（お金・年齢・全体の状態）

`Core/GameState.cs`。シングルトン `GameState.I`（`DontDestroyOnLoad`）。

- 読み取り：`Money`(int) / `Stress`(int 0-100) / `AgeDays`(float) / `AgeYears`(float = ageDays/365) / `WorkLifeBalance`(float 0-1) / `GoalAgeDays`(int = 20*365 = 7300) / `IsGameOver`(bool)
- 書き込み窓口：`AddMoney(int)` / `AddStress(int)` / `AddAgeDays(float)` / `SetBalance(float)` / `LoadValues(...)`
- イベント：`OnMoneyChanged` / `OnStressChanged` / `OnAgeChanged` / `OnBalanceChanged` / `OnGameOver`
- 初期値：money=300000、stress=30、ageDays=0、workLifeBalance=0.5、goalAgeYears=20、daysPerYear=365
- **クランプは窓口側でやってくれる**：お金は0未満にならず、ストレスは 0〜100 に収まる。呼ぶ側で気にしなくてよい。
- `AddAgeDays` で `AgeDays >= GoalAgeDays`（20歳）になると `IsGameOver = true` ＋ `OnGameOver` 発火。

> ⚠ **ストレスは人ごとに持つ（決定済み）**：母親・父親・子どもがそれぞれ 0〜100 のストレスを持つ（[../decisions/0005-per-person-stress.md](../decisions/0005-per-person-stress.md)）。**`ParentStatus.stress` の形が正**で、日次で動かしているのも `ParentStressSystem` → `ParentStatus`。
> 一方 `GameState.Stress`（世帯にひとつ）が旧仕様のまま残っている。チーム向けハンドアウトもこちらを使う前提で書かれているので、**どう整理するかは未決定**（子ども用の入れ物を作って `GameState.Stress` を廃止する、など）。子どものストレスを持つ入れ物はまだ無い。

---

## 4. 親のステータスと収入

| クラス | 役割 |
|---|---|
| `Household` | 母親・父親の `ParentStatus` を持つ。`Household.I.Get(ParentRole.Mother)` / `TotalMonthlySalary` |
| `ParentStatus` | 親1人ぶんの 役職・月給・日給・ワークライフバランス・ストレス |
| `WageAccrual` | 毎日、各親の `AccrueDailyWage()` を呼ぶ |
| `HouseholdIncome` | 給料日（月替わり）に各親の `CollectWage()` を回収して `GameState.AddMoney` |
| `ParentStressSystem` | 毎日、バランスに応じて各親のストレスを増減 |

### 給与の計算（実装済み）

```
月給   = 基本給(既定 200,000) × 役職倍率
日給   = 月給 ÷ 30 × バランス倍率
積立   = 毎日 日給を pendingWage に加算（WageAccrual）
給料日 = 月が変わったら pendingWage をまとめて世帯のお金へ（HouseholdIncome）
```

- 役職 `JobRank`：`平社員(×1.0) / 主任(×1.2) / 係長(×1.45) / 課長(×1.75) / 部長(×2.1)`
- バランス倍率 `ParentStatus.BalanceMultiplier()`：0–20→×0.8、21–40→×0.9、41–60→×1.0、61–80→×1.2、81–100→×1.5
  （[../design/rules-and-stats.md](../design/rules-and-stats.md) 3章の表をそのまま実装したもの）
- **月給は役職だけで決まり、バランスでは変わらない**（バランスが効くのは日給）。
- `ParentStatus` のイベント：`OnSalaryChanged` / `OnDailyWageChanged` / `OnPendingWageChanged` / `OnBalanceChanged` / `OnStressChanged`

### 親ストレス（実装済み）

`ParentStressSystem` が毎日、完全ライフ寄り `-5` 〜 完全ワーク寄り `+5` を線形補間して各親の `ParentStatus.stress` に加算（Inspector で調整可）。0〜100 にクランプされる。

---

## 5. セーブ

`Core/SaveSystem.cs`。`PlayerPrefs` + `JsonUtility`。`Save()` / `Load()` / `HasSave()` / `Delete()`。

保存しているのは `SaveData` の4項目だけ：`money` / `stress` / `ageDays` / `workLifeBalance`。
**役職・積立給与・親ごとのステータスは保存されない**ので、`ParentStatus` を増やすときは `SaveData` も一緒に拡張すること。

---

## 6. 実装済みのシーン

| シーン | 内容 | ドキュメント |
|---|---|---|
| `Assets/Scenes/test.unity` | 各メンバーが機能を組み込む共通の検証シーン | [../team/handout.md](../team/handout.md) |
| `Assets/Yamaguchi/scenes/Main_test.unity` | HUD（お金・年齢・成長ゲージ・ステータス開閉・下部4ボタン） | [ui-main-test-hud.md](ui-main-test-hud.md) |
| `Assets/Yamaguchi/scenes/Main_test2.unity` | 制度一覧のスクロール、母親カードのステータス欄 | [ui-scroll-patterns.md](ui-scroll-patterns.md) |

---

## 7. まだ無いもの（仕様書にあるが未実装）

- **子どものステータス全般**：子のストレス／成績／趣味／体調を持つ入れ物がまだ無い。仕様上もっとも重要なのは子のストレス（家出の引き金）。
- **家出によるゲームオーバー**：`IsGameOver` は20歳到達でしか立たない。家出カウンター（子ストレス限界の継続日数）は未実装。
- **エンディングの分岐**：20歳で `OnGameOver` が飛ぶだけ。成績／趣味／体調による進路分岐は未実装。
- **月次の食事選択・イベント抽選**：`MonthTicker.OnMonthChanged` は用意できているので、購読する側を作れば繋がる。
- **昇進の条件**：`SetRank()` はあるが、誰がいつ呼ぶか（ワーク寄りを続けたら昇進、など）は未実装。
- **一時停止の窓口（`PushPause` / `PopPause`）**：判断画面を開いている間は時間を止める方針だが、停止要求をカウントする仕組みがまだ無い。各画面が `SetRunning` を直接呼ぶと、画面が重なったときに破綻する。→ [../decisions/0004-pause-during-decisions.md](../decisions/0004-pause-during-decisions.md)
- **効果（Effect）の共通モデル**：食事・イベント・制度が共通で使う `Effect` / `EffectApplier` がまだ無い。3機能の土台なので、各自が `GameState` を直接叩き始める前に用意したい。→ [../design/effects.md](../design/effects.md)
- **ストレスの持ち方の整理**：`ParentStatus.stress`（正）と `GameState.Stress`（旧仕様）が並存している。子ども用の入れ物も要る。→ 3章

### 数値の扱い

[../design/rules-and-stats.md](../design/rules-and-stats.md) / [../design/food-system.md](../design/food-system.md) の数値はすべて調整前のたたき台。
`ParentStatus` のように `[SerializeField]` で Inspector に出すか、ScriptableObject に外出しすること。**コードにハードコードしない。**
