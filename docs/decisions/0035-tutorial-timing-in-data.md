---
title: チュートリアルを出すタイミングはデータに持たせ、共通のスケジューラで出す
updated: 2026-11-08
status: 採用
---

# チュートリアルを出すタイミングはデータに持たせ、共通のスケジューラで出す

## 背景
[0034](0034-tutorial-panel.md) では、家出のチュートリアルを出す専用のスクリプト（`RunawayTutorialTrigger`）を作った。今後チュートリアルが増えると、出す時期はそれぞれ違うので、そのたびにスクリプトを書くことになる。

## 決定
1. 「いつ出すか」は **チュートリアルのデータ（`TutorialData`）に `TutorialTiming` として持たせる**。編集ウィンドウで種類を選び、値を入れるだけで決まる。
2. 種類（たたき台）：コードから呼んだときだけ／ゲームを始めてすぐ／決まった年齢の日／家出の判定が始まる日（ずらせる）／子どものストレスが ○ 以上／お金が ○ 円より少ない。
3. 出す係は共通の **`TutorialScheduler`** 1つ（`TutorialCanvas/Triggers`）。登録されたチュートリアルを毎日判定し、条件を満たした最初の日に `ShowOnce` する。
4. `RunawayTutorialTrigger` は廃止し、家出のチュートリアルは「家出の判定が始まる日（ずらす日数 0）」に置き換えた。出る日は変わらない（12歳4月1日）。
5. 編集ウィンドウに「すべてのチュートリアル（いつ出るか）」の一覧を置き、出る順・登録漏れを一目で確かめられるようにする。

## 検討した他の案と、採らなかった理由
- チュートリアルごとにきっかけのスクリプトを書く … 毎回 DayTicker の購読などを書くことになり、どれがいつ出るかを一覧で見られない。
- タイミングをスケジューラ側（シーン）に持たせる … チュートリアルのデータと出す時期が別の場所に分かれ、編集するときに2か所を見ることになる。

## 結果・影響
- コード：`Core/Tutorial/TutorialTiming.cs`・`TutorialScheduler.cs` を追加、`TutorialData` に `timing` を追加、`RunawayTutorialTrigger.cs` を削除。プレハブ `TutorialCanvas` の `Triggers` を `TutorialScheduler` に置き換えた。
- 表にない場面（ボタン・イベントの直後など）は、タイミングを「コードから呼んだときだけ」にして `ShowOnce` を呼ぶ。種類を足すのは `TutorialTiming` と `TutorialScheduler.IsDue` の2か所（＋編集ウィンドウの入力欄）。
- ドキュメント：[../code/ui-tutorial.md](../code/ui-tutorial.md)、画像つきの解説 [../guides/tutorial-tool/index.html](../guides/tutorial-tool/index.html)
