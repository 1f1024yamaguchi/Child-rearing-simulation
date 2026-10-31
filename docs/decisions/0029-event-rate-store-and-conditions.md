---
title: 発生率の補正を EventRateModifiers に預け、発生条件を部品にする
updated: 2026-10-31
status: 採用
---

# 発生率の補正を EventRateModifiers に預け、発生条件を部品にする

## 背景

食事プランは `BadEventRate` / `GoodEventRate` の効果を持っていたが、`EffectApplier` はログを出すだけで、発生率には何も効いていなかった。また `EventConditionSO` は抽象クラスだけで、年齢やストレスでイベントを絞る部品がなかった。ストレスや食事でいい・悪いイベントの出やすさを変えるには、この2つが先に必要になる。

## 決定

- 発生率の効果は `EventRateModifiers`（`Assets/Maruyama/Scripts/Core/`）に登録する。`EffectApplier` は登録するだけで、抽選はしない。
- 登録するときの期間は `Effect.duration` に従う。`Instant` は無視する。
- 抽選側が読む合計は `TotalPercent(kind)`。Effect 由来に加えて、家族全員の特性・状態の加算補正を足す（traits.md 3章の記述どおり）。
- 発生条件は `EventConditionSO` を継承した部品にする。まず `AgeRangeCondition`（年齢帯）と `StressLevelCondition`（ストレス段階）を作る。
- 補正はセーブに含めない。

## 検討した他の案と、採らなかった理由

- `EventManager` が食事グレードを見て直接確率を決める … 食事以外（選択肢の効果・特性）から補正を足せなくなる。AGENTS.md ルール3（効果はデータで持つ）にも反する。
- 補正を `GameState` に持たせる … イベント専用の値で、共通の土台に置くほどのものではない。
- 補正をセーブに含める … 今回の範囲を超える。`ThisMonth` の補正が消える影響は小さいので後回し。

## 結果・影響

- `EffectApplier` の発生率の扱いを変更。`EventRateModifiers` / `AgeRangeCondition` / `StressLevelCondition` を追加。
- `EventManager` はまだ補正も条件も使っていない。次に、悪い・良いを別々に月ごとに抽選する作りへ直す。
- [events.md](../design/events.md) 5章に追記。
