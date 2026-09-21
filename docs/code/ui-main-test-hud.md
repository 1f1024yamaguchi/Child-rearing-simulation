---
title: Main_test シーンの HUD 実装
updated: 2026-09-19
status: 実装済み
related: [docs/code/architecture.md, docs/code/ui-scroll-patterns.md]
---

# Main_test シーンの HUD 実装

> シーン `Assets/Yamaguchi/scenes/Main_test.unity` に Desktop-3 のデザインを実装した記録。
> テキスト・ボタンはすべて TextMeshPro。

## 1. 構成

- スクリプト：`Assets/Yamaguchi/Scripts/UI/MainTestHUD.cs`
- シーン内オブジェクト（**実体として常設＝Play 不要で表示・Inspector で調整可**）：
  - `GameManager`（`GameState` + `GameClock`）
  - `HUD`（`MainTestHUD`）
  - `HUD_Canvas`（UI 本体。子に MoneyBox / AgeBox / StatusToggle / StatusPanel / GrowthTrack(GrowthFill) / StageLabel / GoalLabel / Btn_サポート・カレンダー・セーブ・設定 など）
  - `EventSystem`（`InputSystemUIInputModule`）

## 2. 動作方式（重要）

- **一度シーンに構築**（`MainTestHUD.EditorRebuild()` を実行して生成 → シーン保存）。以後はエディタに実体が見える。
- **実行時**は `Start` で既存の `HUD_Canvas` を名前で探して各 UI に接続（`BindExisting`）。無ければ実行時に構築（フォールバック）。
- ボタンの `onClick` は実行時に配線する（コードの `AddListener` は非シリアライズのため）。
- → **作り直すと Inspector で調整した座標がリセットされる。** レイアウト調整は各 UI の RectTransform、または `MainTestHUD` のシリアライズ値 / `BuildUI` の座標で行う。

## 3. 実装済みの UI

- 左上「お金：{Money:N0}」（`OnMoneyChanged` 連動。編集中は既定表示、Play で実値）
- 上部「{歳} {月}月{日}日」＝生まれてからの年齢（誕生日 12/25 を 0歳0日として月日計算。`OnAgeChanged` 連動）
- 成長ゲージ（`AgeDays / GoalAgeDays` 横塗り、右端「20歳」、中央に段階：あかちゃん／幼児／小学生／中学生／高校生／成人）
- 右のステータス開閉ボタン（`<` ／ `>`）→ ステータスパネル（枠のみ。将来ここに母親／父親／子どもカード）
- 下部4ボタン：サポート／カレンダー／セーブ／設定（現状は `Debug.Log` の仮ハンドラ）

## 4. 日本語フォント（重要）

- 日本語 TMP フォントが無かったため、Windows の `meiryo.ttc` から生成：`Assets/Yamaguchi/Resources/JPFont.asset`（Dynamic）。TMP 既定フォントにも設定済み。HUD は `Resources.Load<TMP_FontAsset>("JPFont")` で読む。
- ※ OS の動的フォント（`Font.CreateDynamicFromOSFont`）は TMP では日本語を描画できない（フォントファイルが必要）ため、この方式にしている。

## 5. 未実装・今後

- 4ボタンの遷移先（サポート＝制度一覧／カレンダー／セーブ処理／設定）は仮。
- ステータスパネルの中身（母親・父親・子どもカード）。→ 母親カードは Main_test2 側で先行実装。[ui-scroll-patterns.md](ui-scroll-patterns.md)
- 角丸スプライト未割当（ビルトインは実行時に読めない）。必要なら 9slice スプライトを差し込む。
