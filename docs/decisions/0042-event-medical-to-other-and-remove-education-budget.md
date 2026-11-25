---
title: イベント医療費の「その他」分類と月別教育費の撤廃
updated: 2026-11-24
status: 採用
---

# イベント医療費の「その他」分類と月別教育費の撤廃

## 背景
1. **イベント医療費が「その他」に入らない問題**:
   - イベント「みらいが風邪をひいた（`demerit_child_sick`）」や「ケガをした（`demerit_injury`）」において、「病院に行く」の選択肢の家計簿カテゴリ（`ledgerCategory`）が `医療(7)` に設定されていた。
   - 0041の決定により「その他カテゴリにはイベントで発生した支出のみを入れる」方針としていたが、カテゴリが `医療` であったため、毎月の独立行として表示され「その他」に分類されない不具合が生じていた。
2. **月別生活費における教育費の撤廃**:
   - 総務省家計調査データ（budget01〜12）に基づき `MonthlyLivingExpenseManager` に導入されていた「教育」（毎月約2万〜5万円）について、既存の学校給食費・学用品費・授業料等との重複感および家計負担バランスの観点から撤廃する要望があった。

## 決定
1. **風邪・ケガ等のイベント選択肢の家計簿カテゴリを「その他」に変更**
   - `Event_Demerit_ChildSick.asset` および `Event_Demerit_Injury.asset` の選択肢 `ledgerCategory` を `その他(8)` に変更した。
   - `EventAssetBuilder.cs` の定義も `ExpenseCategory.その他` に更新。
   - なお、子ども医療費助成による軽減（`MedicalExpenseCoverage`）は `insuredMedicalExpense` フラグで判定されているため、カテゴリを「その他」に変更しても助成機能は完全に正常動作する。
2. **家計簿UI（月タブ・年タブ）におけるイベント出費判定の強化**
   - `LedgerMonthView.cs` および `LedgerYearView.cs` において、`l.category == ExpenseCategory.その他` に加え、イベント起因の支出（`l.source` に「：」を含む形式）を確実に「その他」カテゴリへ振り分ける安全策を追加した。
3. **月別生活費マネージャーからの「教育」項目の撤廃**
   - `MonthlyLivingExpenseManager.cs` から「教育」の月別定義を削除・撤廃した。
   - これにより、月別生活変動費は全8項目（光熱・水道、家具・家事用品、被服及び履物、保健医療、交通、通信、教養娯楽、諸雑費）となった。

## 検討した他の案と、採らなかった理由
- **医療費控除（確定申告）のためにイベント医療費のカテゴリを `医療` のまま維持する案**
  - 採らなかった理由：プレイヤーにとって「イベントで選んだ結果の出費はすべて『その他』に入る」という分類ルールの方が直感的であり、すでに毎月の生活費に「保健医療（年間約16万円）」があるため医療費控除の学習・適用体験は十分に満たされているため。

## 結果・影響
- `Assets/Maruyama/Data/Events/Event_Demerit_ChildSick.asset`: `ledgerCategory: 8` に更新。
- `Assets/Maruyama/Resources/Events/Event_Demerit_Injury.asset`: `ledgerCategory: 8` に更新。
- `Assets/Maruyama/Scripts/Editor/EventAssetBuilder.cs`: 病院に行く選択肢を `ExpenseCategory.その他` に更新。
- `Assets/Arisaka/MonthlyLivingExpenseManager.cs`: 「教育」項目を撤廃（8項目へ）。
- `Assets/Yamaguchi/Scripts/UI/Ledger/LedgerMonthView.cs` / `LedgerYearView.cs`: イベント出費を確実に「その他」に集約。
- みらいが風邪をひいて病院に行った費用が、家計簿の「その他」グループ内に正しく表示されるようになった。

