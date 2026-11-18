---
title: 家計簿の収支・支出を月末締めで当月に記録する設計への変更
updated: 2026-11-18
status: 採用
---

# 家計簿の収支・支出を月末締めで当月に記録する設計への変更

## 背景
これまでゲーム内の月次処理は、月が変わった瞬間（翌月1日の月初）に `MonthTicker.OnMonthChanged` で発火していた。
そのため、当月1日〜末日に働いた給与の回収（`HouseholdIncome`）や、当月に生活した固定費の引き落とし（`ExpenseManager`）、支援金の支給（`SupportManager`）がすべて「翌月1日」に実行されていた。

このとき家計簿（`Ledger`）は現在の日付（翌月1日）を参照して行を記録するため、**当月に発生した収支・支出がすべて「次の月」の家計簿に記載されてしまう**現象が発生していた（例: 4月に働いた給料や4月の家賃が、5月の家計簿に記載される）。
これにより、4月の家計簿には食費しか載っておらず大赤字に見え、5月の家計簿に4月分の給料・家賃が載るという、プレイヤーにとって直感に反する表示となっていた。

## 決定
月次処理を**「月末の締め（当月の収支精算）」**と**「月初の開始（新しい月のスタート）」**に明確に分離した。

1. **`DayTicker.OnDayCompleted` の追加**:
   - その日の日次処理（日給の積立、食費の引き落とし等）が完了した直後に発火するイベントを新設。
2. **`MonthTicker.OnMonthEnding` の新設（月末の締め）**:
   - `MonthTicker` がその月の最終日（`d == GameDate.DaysInMonth(m)`）を検知。
   - その日の日給積立や食費引き落としが完了した直後（月末の最後）に `OnMonthEnding` を発火。
   - `HouseholdIncome`（給料支給）、`ExpenseManager`（固定費引落）、`SupportManager`（支援金支給）は `OnMonthEnding` を購読し、**当月末の日付（4月30日など）で精算・家計簿記録**を行う。
3. **`MonthTicker.OnMonthSettled` の発火タイミングの適正化**:
   - `OnMonthEnding` の収支精算が完了した直後（月末の最終処理）に発火。
   - これにより、`BankruptcyWatcher`（赤字判定）は月末の収支確定後の残高で判定され、`TaxManager` は12月末（12月31日）に1年分の所得税の年末調整を行う。
4. **`MonthTicker.OnMonthChanged` の純化（月初の開始）**:
   - 翌月1日になった瞬間は、純粋に新しい月のイベント（`MealSystemManager` の食事プラン選択、`EventManager` の月初イベント抽選など）のみを発火する。

## 検討した他の案と、採らなかった理由
- **案A: 翌月1日に精算を実行するが、家計簿の記録先 monthIndex だけを前月にする案**:
  - 翌月1日にトースト「給料が入りました」が出て手元の所持金が増加するにもかかわらず、家計簿を開くと「今月」には給料が記載されておらず、前月を開かないと確認できないという表示の齟齬が生じる。
  - また、所持金残高の増減タイミングと家計簿の月次収支（手取り−支出）のタイミングがずれるため却下した。

## 結果・影響
- 初月（0歳4月）から、4月の給与、4月の固定費、4月の食費、4月の支援金がすべて「0歳4月」の家計簿に正しく記載されるようになった。
- 給与振込や家賃引き落としの通知が出てすぐに家計簿を開いた際も、その月の家計簿が開いて今動いたお金がそのまま表示されるようになり、プレイヤーの直感と完全に一致するようになった。
- 影響ファイル:
  - `Assets/Yamaguchi/Scripts/Core/DayTicker.cs`
  - `Assets/Yamaguchi/Scripts/Core/MonthTicker.cs`
  - `Assets/Yamaguchi/Scripts/Core/HouseholdIncome.cs`
  - `Assets/Yamaguchi/Scripts/Core/Expense/ExpenseManager.cs`
  - `Assets/Yamaguchi/Scripts/Core/SupportManager.cs`

