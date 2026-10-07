---
title: 制度の状態は SupportManager が一元で持ち、効果は EffectApplier で適用する
updated: 2026-10-05
status: 採用
---

# 制度の状態は SupportManager が一元で持ち、効果は EffectApplier で適用する

## 背景
サポート画面で申請しても効果が反映されず、申請前後で期限や受給状況も見えなかった。制度の状態（申請済み・受給中・支給停止など）はサポート画面とカレンダーの両方が使う。

## 決定
- 制度ごとの状態は Core の `SupportManager` が `id` をキーに持つ。画面（サポート・カレンダー）は読むだけ。
- 申請（OnApply）・毎月の支給（Monthly）・支給停止からの再開は、すべて `EffectApplier.Apply(scheme.effects)` で適用する。
- 状態の変化は `DayTicker.OnDayPassed` / `MonthTicker.OnMonthChanged` で判定する。
- 申請前は期限と効果、申請後は受給状況と次の手続きをカードに出す。
- 毎月の支給は年齢の段ごとの支給表（`PaymentTier`）で持つ。
- 申請条件は `SupportRequirementSO` を継承した部品をリストで持たせ、後から種類を足せるようにする。
- 詳細は [../design/support-apply.md](../design/support-apply.md)。

## 検討した他の案と、採らなかった理由
- カード（UI）が自分で状態を持つ … カレンダーと食い違う・セーブしにくい。
- ScriptableObject に状態を書く … Play 中の変更がエディタに残る（support-systems.md 3.5）。

## 結果・影響
- support-systems.md 3.5 に関連リンクを追加。`SupportScheme` に `category` / `applyRequirements` / `monthlyTiers` / `renewWindowDays` を足す。
- calendar-system.md 8.3 の「制度の状態のセーブ」は山口側に移す提案（草尾さんと合意後に書き換え）。
- `SaveData` に `supportSchemes` を追加する（草尾さんと項目名を合わせる）。
