---
title: 他のメンバーの合意が必要な項目（支援制度・カレンダー・支出）
updated: 2026-10-13
status: 合意待ち
related: [docs/design/support-apply.md, docs/design/calendar-system.md, docs/design/expenses.md]
---

# 他のメンバーの合意が必要な項目

> 山口が支援制度・支出・家計簿の設計を進める中で、**他のメンバーの担当部分に関わるもの**の一覧。
> 合意が取れたら「合意日」に日付を書き、関係する文書を直す。断られた・変更になった場合は理由を書く。

---

## 草尾さん（カレンダー）

| # | お願い・確認したいこと | 草尾さんへの影響 | 詳しくは | 合意日 |
|---|---|---|---|---|
| K1 | 制度の状態（申請済み・受給中など）は山口の `SupportManager` が持ち、カレンダーは読むだけにする | 制度の状態を自分で持たなくてよくなる | support-apply.md 0章 | |
| K2 | calendar-system.md 8.3 の「制度の状態のセーブ」を山口に移す。草尾さんのセーブはメモと予定（`CalendarPlan`）だけ | 作業が減る | support-apply.md 0.3 | |
| K3 | **制度の締め切りをカレンダーに自動で載せない**（書いた予定だけ載せる）。9/23 の決定 0010 を変える | 自動表示を作らなくてよくなる | 決定 0016 | |
| K4 | 予定の書き込み（2.2）・リマインダー（2.4）を必須の作業として追加。完了チェック・サポート画面への移動（2.5）は時間があれば | **作業が増える** | calendar-system.md 2.2〜2.5、8章 | |
| K5 | 制度カードの［カレンダーに追加］から呼ぶ `CalendarUI.OpenAddPlan(schemeId, ageDays, kind)` を用意してもらう | 公開メソッドを1つ作る | calendar-system.md 2.3 | |
| K7 | メモ・予定のセーブは `ISaveParticipant` を実装して自分で保存・復元してもらう（save-system.md 2.1）。SaveSystem は山口が作る | 自分の分の Capture/Restore を書く | save-system.md 1.2・2.1 | |
| K6 | リマインダーのポップアップから山口の `SupportPanelController.Open(schemeId)` を呼ぶ。判断画面が開いているかは `GameClock.IsPauseRequested`（山口が追加）で判定する | 山口側の窓口を使う | calendar-system.md 2.4 | |

---

## 丸山さん（イベント）

| # | お願い・確認したいこと | 丸山さんへの影響 | 詳しくは | 合意日 |
|---|---|---|---|---|
| M1 | `EffectApplier` に `Apply(effects, LedgerSource source)` を追加し、お金が動いたら `Ledger.Record` を呼ぶ。今の `Apply(effects)` は残す | `EffectApplier.cs` を少し変更 | expenses.md 5.6 || 2026-10-28（山口が実施。E1b・2026-10-22 動作確認） |
| M2 | `EventChoice` に家計簿の種類（`ledgerCategory`）を足し、`EventChoiceButtonUI` 56行目でイベント名・選択肢名と一緒に渡す（`EventCardUI` → `ChoiceBlockUI` → ボタンへイベント名を渡す） | 4ファイル変更。お金が動く選択肢を作るときに種類を入れる | antigravity-expenses.md 2.6・依頼E1b || 2026-10-28（山口が実施。E1b・2026-10-22 動作確認） |
| M3 | 支援制度・支出の条件は `RequirementSO`（山口）として作り、`EventConditionSO` とはまとめない | 影響なし（共有の連絡のみ） | support-apply.md 10章 | |

**順番**：山口が `Ledger` と `LedgerSource` を先に Core に作る → M1・M2 も **山口が行う**（2026-10-21 変更。[antigravity-expenses.md](antigravity-expenses.md) 依頼E1b）。作業前に丸山さんへ一言伝える。

---

## 有坂さん（食事）

| # | お願い・確認したいこと | 有坂さんへの影響 | 詳しくは | 合意日 |
|---|---|---|---|---|
| A1 | 食費でお金を減らすとき（`MealSystemManager` 201行目）は `EffectApplier.Apply(effects, new LedgerSource("食費（普通）", ExpenseCategory.食費))` の形で呼ぶ（家計簿に「食費」として載るように）。**山口が行う**（2026-10-21 変更。依頼E1b） | 1行変更 | expenses.md 5.6 || 2026-10-28（山口が実施。E1b・2026-10-22 動作確認） |
| A3 | 食事の状態（ランク・最後に食費を引いた日・今月の額など）を `ISaveParticipant` で保存・復元してもらう | 自分の分の Capture/Restore を書く | save-system.md 1.2・[request-arisaka-meal-save.md](request-arisaka-meal-save.md) | |
| A2 | **お金がマイナスになるようになる**（0円止めを外す）。`MoneyUI` でマイナスを表示できるようにする（赤字表示など） | `MoneyUI.cs` の確認 | expenses.md 5.7 | |

---

## 全員に共有すること（合意というより連絡）

| # | 内容 | 詳しくは | 共有日 |
|---|---|---|---|
| ALL1 | 金額は実際の円。給料は手取り（手取り率 0.8）で入る。ボーナスはなし | 決定 0015、expenses.md 5.5 | |
| ALL2 | お金がマイナスになりうる。**赤字が2か月続いたら家計破綻でゲームオーバー** | expenses.md 5.7 | |
| ALL3 | `GameState.OnGameOver` は今「20歳到達（クリア）」で呼ばれている。終わり方の理由つきの `OnGameEnded(GameEndReason)` に移行する。`OnGameOver` を使っているコードがあれば教えてほしい | expenses.md 5.7 | |
| ALL4 | 毎月の家計簿（Ledger）ができる。お金を動かすときは `EffectApplier` 経由で「何のお金か」を渡す | expenses.md 5.6 | |
