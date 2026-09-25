---
title: Unity コーディング規約
updated: 2026-09-25
status: 運用中
---

# Unity コーディング規約

このプロジェクトのコードを書く／直すときに必ず守るルール。詳しい実装の背景は [docs/code/architecture.md](../../docs/code/architecture.md) を参照。

## ディレクトリ

| 置くもの | 場所 |
|---|---|
| 共通の土台（時間・お金・親ステータス） | `Assets/Yamaguchi/Scripts/Core/` |
| 各メンバーの機能 | `Assets/<担当者名>/`（Arisaka / Kusao / Maruyama） |
| UI スクリプト | `Assets/Yamaguchi/Scripts/UI/` |
| プレハブ | `Assets/Yamaguchi/Prefabs/UI/` |
| シーン | `Assets/Yamaguchi/scenes/` |
| Resources から読むアセット | `Assets/Yamaguchi/Resources/` |

## 状態管理

- ゲームの状態は **`GameState.I`（シングルトン）に集約**する。UI スクリプトが独自に値を保持しない。
- **日次・月次の処理を作るときは、自分で時間を数えない。** `DayTicker.OnDayPassed` / `MonthTicker.OnMonthChanged` を購読する。
- **プレイヤーが判断する画面を開くときは時間を止める。** 画面の `OnEnable` で `GameClock.I.PushPause()`、`OnDisable` で `PopPause()` を呼ぶ（`CalendarUI` と同じ形。どの閉じ方でも必ず解除される）。`GameClock.SetRunning` は画面の開閉に使わない（ゲームオーバー専用）。→ [../../docs/decisions/0004-pause-during-decisions.md](../../docs/decisions/0004-pause-during-decisions.md)
- `GameState` の値を直接書き換えず、必ず `AddMoney` / `AddStress` / `AddAgeDays` / `SetBalance` などの窓口メソッドを通す。
- UI は `OnMoneyChanged` などの **イベント購読で更新**する。`Update()` で毎フレーム読みに行かない。
- 購読したイベントは必ず `OnDisable` / `OnDestroy` で解除する。

## スクリプトの分け方

**1スクリプト1責務**で分ける。既存の例に倣うこと。

- データ … `SupportScheme`（制度1件のデータ）
- 1要素の見た目 … `StatusRowUI`（1行の表示だけ）、`SupportSchemeCard`（カード1枚の表示だけ）
- 一覧・画面の制御 … `StatusListUI`（行の追加・更新）、`SupportPanelController`（タブ切替とリスト生成）

## 数値と効果の扱い

- バランス調整用の数値を**コードにハードコードしない**。ScriptableObject かシリアライズフィールドに外出しして、Inspector で調整できるようにする。
- **ステータスを動かす処理は `Effect` のデータとして持つ。** 食事・イベント・制度の中で `GameState.I.AddStress(+5)` のような呼び出しを直接書かない。適用は共通の `EffectApplier` に任せる。→ [../../docs/design/effects.md](../../docs/design/effects.md)
- 効果の種類を足すときに触るのは `EffectKind` と `EffectApplier` の2箇所だけ。各機能のコードは触らない。
- 数値の出どころは `docs/design/` の表。コードを変えたらドキュメント側の数値も合わせる。

## UI（uGUI / TextMeshPro）

- テキスト・ボタンのラベルは **すべて TextMeshPro** を使う。レガシー `Text` は使わない。
- 日本語フォントは `Resources.Load<TMP_FontAsset>("JPFont")`。OS の動的フォント（`Font.CreateDynamicFromOSFont`）は TMP で日本語を描画できないので使わない。
- **`TextOverflowModes.Ellipsis` を使わない。** テキストが枠を超えたときに文字が1文字も描画されなくなる。既定の `Overflow` のままにする。
- 入力まわりは **新 Input System**。EventSystem には `InputSystemUIInputModule` を使う（`StandaloneInputModule` を足さない）。
- スクロールを作るときは [docs/code/ui-scroll-patterns.md](../../docs/code/ui-scroll-patterns.md) の3階層構成に従う。自己流で組むとまず動かない。

## シーンを壊さないために

- シーンに実体として置かれている UI を、コードから作り直さない。`MainTestHUD` は既存の `HUD_Canvas` を名前で探して接続する方式（`BindExisting`）。**再構築すると Inspector で調整した座標がリセットされる。**
- ボタンの `onClick` はコードの `AddListener` ではシリアライズされない。実行時に配線する前提で書く。
