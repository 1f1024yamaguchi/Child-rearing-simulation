---
title: budget01〜12（東京都区部）に基づく月別生活変動支出の導入
updated: 2026-11-22
status: 採用
---

# budget01〜12（東京都区部）に基づく月別生活変動支出の導入

## 背景
これまでの支出システムでは、電気代・ガス水道・通信費などが通年同一の固定値（たたき台）として設定されていた。
よりリアルな子育て家計の感覚（冬の光熱費増加、4月の新学期教育費、8月の夏休み教養娯楽、年末の諸雑費など）を体験できるよう、有坂の調査資料である `docs/research/raw/budget01.csv`〜`budget12.csv`（総務省家計調査・二人以上世帯・東京都区部）の月別実データに基づく支出モデルへの見直しを行った。

## 決定
1. **担当領域（Arisaka）内での完結**:
   - `Assets/Arisaka/MonthlyLivingExpenseManager.cs` を新設し、自動常駐（`RuntimeInitializeOnLoadMethod`）および `ISaveParticipant`（SaveKey: `"monthly_living_expenses"`）を実装。
   - 他メンバーの領域（Yamaguchiのスクリプトやシーンファイル）を変更せず、有坂フォルダ内で完結させた。
2. **反映させる9項目（budget01〜12 東京都区部実データ）**:
   - **光熱・水道**（`ExpenseCategory.光熱費`）: 17,951円〜28,971円
   - **家具・家事用品**（`ExpenseCategory.その他`）: 10,126円〜24,217円
   - **被服及び履物**（`ExpenseCategory.その他`）: 8,770円〜15,585円
   - **保健医療**（`ExpenseCategory.医療`）: 10,528円〜20,551円
   - **交通**（`ExpenseCategory.その他`）: 2,849円〜11,218円
   - **通信**（`ExpenseCategory.通信`）: 10,836円〜13,987円
   - **教育**（`ExpenseCategory.学校`）: 5,236円〜49,640円（※4月がピーク）
   - **教養娯楽**（`ExpenseCategory.その他`）: 28,139円〜42,371円（※8月がピーク）
   - **諸雑費**（`ExpenseCategory.その他`）: 23,477円〜35,687円
3. **既存項目の維持と重複排除**:
   - 既存の「居住（家賃: Expense_Rent）」「保育（保育料・副食費: Expense_NurseryFee/Meal）」「学校関連」は変更せず維持。
   - 既存の旧固定値（`electricity`, `gas_water`, `phone`）については、`MonthlyLivingExpenseManager` が起動時および実行時に `ExpenseManager.I.Items` から自動除外し、二重引き落としを完全に防止。
4. **月末締めでの引き落とし・家計簿記録**:
   - `MonthTicker.OnMonthEnding` を購読し、各月の最終日にその月番号（1〜12月）に応じた金額を `EffectApplier.Apply` 経由で世帯口座から引き落とし、家計簿（`Ledger`）に記録。

## 検討した他の案と、採らなかった理由
- **案A: Yamaguchi側の ExpenseItem / CostTier を拡張して12か月分のフィールドを持たせる案**:
  - `Assets/Yamaguchi/` のCoreスクリプトを変更する必要があり、「作業は Assets/<担当者名>/ の中で完結させる」「Arisakaフォルダ内でお願い」というルールおよびユーザー要求に反するため却下。
- **案B: 月ごとに12個×項目数のExpenseItemアセットを作成する案**:
  - 100個以上のアセットが必要となり、管理が煩雑でアセット破綻のリスクが高いため却下。

## 結果・影響
- 毎月1日〜12月のリアルな季節変動支出が家計簿に正確に反映され、家計管理のリアリティが大幅に向上した。
- 既存の居住費や保育料、支援制度による肩代わり（covers）の仕組みを損なうことなく安全に統合された。

