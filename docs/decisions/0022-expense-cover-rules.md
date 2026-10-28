---
title: 無償化（支出の肩代わり）の効き方と家計簿への載せ方
updated: 2026-10-28
status: 採用
---

# 無償化（支出の肩代わり）の効き方と家計簿への載せ方

## 背景
無償化は「お金を配るのではなく支出を減らす」と決まっていた（[../design/expenses.md](../design/expenses.md) 4章）。実装（依頼E3）で、次の細かいところを決める必要があった。

- 肩代わりはいつから効くか、支給停止中はどうするか。
- 支給表が空で covers だけの制度（幼保無償化など）の「毎月の支給」と「受給終了」をどう扱うか。
- 肩代わりされた額（covered）を家計簿にどう入れるか（`EffectApplier` はお金の額しか扱わない）。

## 決定
- **肩代わりが効くのは「受給中（Active）」かつ「申請した月より後の月」だけ**。毎月の支給と同じく翌月から。支給停止（Suspended）中は肩代わりしない（さかのぼっても戻さない）。窓口は `SupportManager.CoveringSchemes()`。受給中の一覧は `ActiveSchemes()`。
- covers の **期間（fromAge〜toAge）は ExpenseManager が支出ごとに判定**する。肩代わり額は `Full`＝全額／`Rate`＝額×割合／`UpTo`＝○円まで。複数の制度が同じ支出を肩代わりするときは合計し、支出の額を上限にする（0 未満にしない）。
- **支給表が空で covers だけの制度は、毎月の支給の処理をしない**（「今月は対象外」のログ・表示を出さない）。
- **受給終了の判定に covers の toAge も含める**（support-apply.md 3.2 の 4）。covers も支給表も空の制度は今までどおり終わらない。
- 家計簿：**支払った額は今までどおり `EffectApplier.Apply(effects, LedgerSource)` が amount として載せる。肩代わり額は `ExpenseManager` が `Ledger.I.Record(source, 0, covered)` で同じ行に足す。** `Ledger.Record` は同じ月・名前・種類の行をまとめ、amount が 0 でも covered があれば行を作るので、新しい窓口（`AddCovered`）は作らなかった。全額肩代わりでも「保育料 0円（支援 ○○円）」の行が残る。

## 他の案と、採らなかった理由
- `Ledger.AddCovered(source, covered)` を作る … `Record(source, 0, covered)` で同じことができ、窓口を増やすと記録の入口が2つになるため。
- covers を `EffectApplier` 経由にする … Effect は「所持金を動かす」ものなので、お金が動かない肩代わりを入れると意味がずれる。

## 結果・影響
- コード：`CoverType` / `ExpenseCover`（新規）、`SupportScheme.covers`、`SupportManager.ActiveSchemes` / `CoveringSchemes` / `HasEnded`、`ExpenseManager.CalcMonthlyCost(item, tier, ctx, out covered)`。
- アセット：`Support_PreschoolFree`（保育料を 3歳1月1日〜5歳12月31日 全額）、`Support_HSTuitionSupport`（高校授業料を 15歳4月〜18歳3月 月9,900円まで）。
- サポート画面のカードは支給表を前提にした表示なので、covers だけの制度が受給中のときは期限の行に「支出を肩代わり中（申請した翌月から）」とだけ出す（`SupportScheduleText.ActiveLine`）。何をいくら肩代わりするかをカードに出すのは後で行う。
