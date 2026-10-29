---
title: ゲーム終了理由の共通化（GameEndReason）と家計破綻・赤字警告の導入
updated: 2026-10-29
status: 採用
---

# ゲーム終了理由の共通化（GameEndReason）と家計破綻・赤字警告の導入

## 背景

- これまではゲーム終了がゴール到達（巣立ち・旧20歳到達）と家出（Runaway）で個別に扱われており、所持金も `Mathf.Max(0, ...)` で 0 円止めされていた。
- そのため、生活費や教育費・税金が所持金を超えるリアルな赤字状態を表現できず、所持金が足りないときのゲームオーバー（家計破綻）も存在しなかった。
- また、セーブデータの削除も `OnGameOver` と `OnRunaway` の個別の購読になっており、終了理由が増えるごとに処理が分散する懸念があった。

## 決定

1. **マイナス所持金の許可**:
   `GameState.AddMoney` および `LoadValues` での 0 円クランプ（`Mathf.Max(0, ...)`）を外し、赤字（借金・未払い状態）を許容する。
2. **ゲーム終了理由の統合（`GameEndReason`）**:
   `enum GameEndReason { GoalReached, Runaway, Bankrupt }` を定義し、`GameState.EndGame(GameEndReason reason)` メソッドと `event Action<GameEndReason> OnGameEnded` を新設。
   移行期間として、`GoalReached` の場合のみ既存の `OnGameOver` も同時に発火させる。
3. **家計破綻ウォッチャー（`BankruptcyWatcher`）**:
   月の精算後（`MonthTicker.OnMonthSettled`）に所持金を確認し、マイナスなら赤字月数を +1、プラスなら 0 にリセット。
   赤字月数が上限（既定2ヶ月連続）に達したときに `GameState.I.EndGame(Bankrupt)` を呼び出し、`OnBankrupt` を発火する。
   `ISaveParticipant`（SaveKey: "bankrupt"、RestoreOrder: 45）を実装し、赤字月数をセーブ・ロード可能にする。
4. **赤字警告画面（`DeficitWarningUI`）**:
   赤字月数が 1 ヶ月以上になった際に自動でポップアップ表示し、開いている間はゲーム時間を一時停止（`PushPause`/`PopPause`）する。
   不足額を表示し、［家計簿を見る］（今月の家計簿を開く）、［サポート画面へ］（支援制度一覧を開く）、［閉じる］を提供してプレイヤーのリカバリーを促す。
5. **セーブ削除の一本化**:
   `AutoSaver` は `OnGameOver` と `OnRunaway` の個別監視から、`OnGameEnded` の一元監視にまとめ、どの理由で終了してもセーブデータを削除する。

## 検討した他の案と、採らなかった理由

- **赤字になった瞬間に即座にゲームオーバーにする案**:
  現実でも急な出費や税金で一時的に口座残高がマイナスになることや、借入・支援申請で立て直す猶予がある。即死だと理不尽感が高くゲーム体験を損なうため、1ヶ月の猶予（赤字警告）を挟み、2ヶ月連続で破綻とする猶予制を採用した。
- **所持金 0 円止めを維持し、買えないものをスキップする案**:
  固定費や学費・税金は「払えないからスキップ」ができない必須の支出であるため、0 円止めでは家計の危機感が伝わらない。マイナス残高を明示することで、支援制度（児童手当や就学支援金）の重要性がプレイヤーに強く伝わるようにした。

## 結果・影響

- `GameState.cs`, `RunawayWatcher.cs`, `AutoSaver.cs`, `BankruptcyWatcher.cs`, `DeficitWarningUI.cs`, `LedgerUIBuilder.cs` に反映。
- 将来の進路・巣立ち後のエンディング/リザルト画面（`EndingFlow` / 依頼R1）において、`GameEndReason` を受け取ってエンディング種別を分岐させる土台が整った。
