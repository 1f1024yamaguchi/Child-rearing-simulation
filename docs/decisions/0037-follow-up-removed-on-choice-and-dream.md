---
title: 続きの予約は選ばれたときに消す／将来の夢を条件の部品で見る
updated: 2026-11-18
status: 採用
---

# 続きの予約は選ばれたときに消す／将来の夢を条件の部品で見る

## 背景

1. 「続き」のイベントは、予約日が来た時点で `StoryState` の予約から消してから EventManager の出番待ちに入れていた。出番待ちはセーブされず、ロードのときに捨てられる。自動セーブは月が変わった日の処理が全部終わってから走るため、**月の1日に予約された続き**は、セーブの時点で予約にも出番待ちにも無く、ロードすると二度と出なかった（月の初めは食事選択でイベントが待たされるので起きやすい）。
2. ストーリーを職業ごとに作ることにした（2026-11-18）。ただ、職業が決まるのは巣立ちの日なので、途中で分けるには中1の「将来の夢」を使うしかない。夢をストーリーの条件にする方法を決める必要があった。

## 決定

1. **続きの予約は、そのイベントで選択肢が選ばれたとき（`EventManager.OnEventResolved`）に消す。** 予約日が来たら `StoryState.GetDueScheduled` で見るだけにし、出番待ちに入れたものは `StoryEventScheduler` が目印（セーブしない）を付けて毎日入れ直さないようにする。条件を満たさずに捨てるときと、イベントが見つからないときは、その場で消す。
2. **将来の夢は `DreamSystem`（Core・セーブキー "dream"）が中1の4月1日（たたき台）に決め、ストーリーからは条件の部品 `DreamCondition` で見る。** 職業を空にすると「夢が決まっている」、職業を入れると「夢がその職業」。文章の `{dream}` が夢の職業名になる。夢は夢のイベントが出るまでプレイヤーに見せない。
3. 夢の決め方は ending.md 5.3 のとおり、全部の職業で `CareerJudge` を回して点数が一番高いもの。

## 検討した他の案と、採らなかった理由

- EventManager の出番待ちもセーブする … ストーリー以外のイベントも守れるが、丸山さんの担当ファイルを大きく変えることになる。続きはストーリー側だけで直せる。
- 続きを直さない … 続きのイベントがまれに消える。原因が分かりにくい。
- 夢を職業ごとのフラグ（`dream_baseball` など）で持つ … 職業を足すたびにフラグも作る必要があり、夢とフラグの二重管理になる。フラグは消すこともできるため、食い違いが起きる。

## 結果・影響

- コード：`StoryState`（`ConsumeScheduledForDay` を `GetDueScheduled` / `RemoveScheduled` に置き換え）、`StoryEventScheduler`、新規 `Core/Career/`（`CareerDefinition` `CareerJudge` `DreamSystem`）、`Story/Conditions/DreamCondition`、`StoryTextFormatter`（`{dream}`）、`StoryBootstrap`（DreamSystem も自動で作る）。
- 職業のアセットは `Assets/Yamaguchi/Resources/Careers/`（antigravity-ending.md 依頼5 の `Data/Careers` から変更。DreamSystem をシーンに置かずに自動で作るため、Resources から読む）。既定の職業「会社員」（`isFallback`）だけ作ってある。
- ストーリーウィンドウの流れ図・一覧は、夢の職業（DreamCondition）で絞り込めるようにする。
