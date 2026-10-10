---
title: 通知（SupportToast）の動作
updated: 2026-10-09
status: 実装済み
related: [docs/design/support-apply.md, docs/code/ui-support-card.md]
---

# 通知（SupportToast）の動作

> 画面上部に出る短い通知。**何が起きたら何が出るか**、仕様と実装上の判断をまとめる。仕様は [support-apply.md](../design/support-apply.md) 7.4・7.5・10章が正。ファイル：`Assets/Yamaguchi/Scripts/UI/SupportToast.cs`。

## 1. 何をするもの

プレイヤーに、制度まわりの「今起きたこと」を2秒ほど知らせる。サポート画面を閉じていても見えるよう、`Canvas` の直下に置く。

| きっかけ | 出る文 | 例 |
|---|---|---|
| 申請した（`OnApply` の制度） | 「○○を受け取りました　お金 +金額」 | 出産育児一時金を受け取りました　お金 +500,000 |
| 申請した（`Monthly` の制度） | 「○○を申請しました　来月から毎月 +金額」 | 児童手当を申請しました　来月から毎月 +15,000 |
| 期限が過ぎた（`SupportManager.OnExpired`） | 「○○の申請期限が過ぎました」 | 妊婦のための支援給付の申請期限が過ぎました |

- **毎月の支給（`OnPaid`）では出さない。** 通知が毎月何件も出るのを避けるため（2026-10-07 決定）。支給や金額の変化は制度カードと家計簿で確認する。
- 金額が 0 のときは金額の部分を省く（`Monthly` は「来月から支給されます」）。

## 2. 表示のしかた

- フェードイン → 約2秒表示 → フェードアウト。秒数は Inspector（`showSeconds` / `fadeSeconds`）で変えられる。
- **重なったら順番に出す。** 出ている最中に次の通知が来たら、キューに入れて前の通知が消えてから出す。
- 待ち時間は実時間（`WaitForSecondsRealtime` と `unscaledDeltaTime`）。サポート画面を開いて時間が止まっていても、通知は消える。
- `Start` で `SetAsLastSibling()` を呼び、他の画面より手前に出す。

## 3. 仕組み（なぜそうしたか）

- **「申請した」を状態の変化から見分けている。** `SupportManager` には「申請した」イベントが無く、`SupportManager` は変更しない方針だったため、`OnStateChanged` で「`Available` から `Received` / `Active` に変わった」を申請とみなす。`Available` から他の状態へ変わるのは `Apply` のときだけ。
- **セーブ読み込みで誤って出さない。** `Restore` は全制度の `OnStateChanged` を飛ばすので、読み込みで状態が変わって見えることがある。そのため申請日（`appliedAgeDays`）が今日のときだけ通知する。
- **期限切れの1回だけは `SupportManager` 側で保証済み。** `expiredNotified` で絞ってあるので、トーストは `OnExpired` が来たら出すだけ。
- **お金は動かさない。** 金額は制度アセットの値を読んで文にするだけ。お金の増減は `SupportManager.Apply` → `EffectApplier` が行う。

## 4. 外から使う

```csharp
toast.Show("好きな文");   // 他の機能から通知を出したいとき（キューに入る）
```

## 5. 気をつけること

- `SupportToast` が有効になる前に起きた申請は通知されない（キューに入れるのは有効な間だけ）。
- 見た目（色・位置・大きさ）は、メニュー「子育て/サポートUI/2. SupportToast を Canvas の直下に作る」で作った `Panel` を Inspector で直す。
- 文字は `JPFont`。絵文字は使わない。
