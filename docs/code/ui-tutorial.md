---
title: チュートリアル画面（画像＋見出し＋説明文・スワイプでページ送り）
updated: 2026-11-08
status: 実装済み（山口）
---

# チュートリアル画面

上に画像、下に見出しと説明文を出す画面。画像の枠を左右にスワイプ（ドラッグ）するか矢印を押すと、次のページの画像が横から滑って出てきて、説明文も切り替わる。最後のページの「わかった」で閉じる。表示中は時間が止まる（`PushPause` / `PopPause`）。

- 画像つきの使い方の解説：[../guides/tutorial-tool/index.html](../guides/tutorial-tool/index.html)（ブラウザで開く）
- 1本目：**家出**（`Tutorial_Runaway`、1ページ）。家出の判定が始まる日（12歳4月1日）に1回だけ出る。→ [../decisions/0034-tutorial-panel.md](../decisions/0034-tutorial-panel.md)
- **いつ出すかは、チュートリアルのデータ（TutorialData）の「出すタイミング」で選ぶ。** 出す係は共通の `TutorialScheduler` 1つ。→ [../decisions/0035-tutorial-timing-in-data.md](../decisions/0035-tutorial-timing-in-data.md)

---

## 1. 新しいチュートリアルの足し方（スクリプトは触らない）

1. メニュー **「子育て → チュートリアル → 編集ウィンドウ」** を開き、「新しく作る」（保存先 `Assets/Yamaguchi/Data/Tutorial/`）。
2. 「＋ ページを追加」でページを足し、各ページに **画像（ドット絵）／絵の指示メモ／見出し／説明文** を入れる。↑↓で並べ替え。
   - 画像が未設定のあいだは、灰色の差し替え枠に「絵の指示メモ」が出る。絵ができたら Sprite を入れるだけ。
3. 「Canvas で見る」を押すと、**再生しなくても**シーンの TutorialCanvas にそのページが出る。位置・大きさは Canvas 上のオブジェクトを直接動かして直す。終わったら「プレビューを閉じる」（閉じ忘れて保存しても、ゲーム開始時には自動で閉じる）。
4. **「出すタイミング」を選ぶ**（下の表）。「→」の行に、いつ出るかが日本語で出る。新しく作ったチュートリアルは自動で `TutorialScheduler` に登録される（「⚠ まだ登録されていない」と出たら「登録する」）。シーンを保存する。
5. 表にない場面で出したいときだけ、タイミングを「コードから呼んだときだけ」にして、その場面のスクリプトから次の1行を呼ぶ：

```csharp
TutorialManager.I.ShowOnce(tutorialData);   // まだ見ていなければ出す（「見た」はセーブに入る）
TutorialManager.I.Show(tutorialData);       // 見たかどうかに関係なく出す（見返す用）
```

### 出すタイミングの種類（`TutorialTiming`）

| いつ出すか | 設定する欄 | 判定（`TutorialScheduler.IsDue`） |
|---|---|---|
| コードから呼んだときだけ | なし | 自動では出さない |
| ゲームを始めてすぐ | なし | 最初に日が進んだとき |
| 決まった年齢の日 | 歳・月・日 | その日以降 |
| 家出の判定が始まる日（ずらせる） | ずらす日数（−30＝30日前） | `RunawayWatcher.ActiveFromDay` ＋ ずらす日数 以降 |
| 子どものストレスが ○ 以上になったら | ストレス、判定を始める日 | その日以降で `Child.Stress >= ○` |
| お金が ○ 円より少なくなったら | 金額、判定を始める日 | その日以降で `Money < ○` |

- 判定は毎日1回（`DayTicker.OnDayPassed`）。条件を満たしていて、まだ見ていないものを `ShowOnce` に渡す。
- 条件の日を過ぎたセーブをロードしたときも、まだ見ていなければ次の日に出る。
- **種類を足すとき**は `TutorialTimingKind` に1つ足し（数字は後から変えない）、`TutorialScheduler.IsDue` と `TutorialTiming.Describe`、編集ウィンドウの `DrawTiming` に1つずつ足す。チュートリアルごとのスクリプトは作らない。

- **ID は後から変えない**（「見た」の記録のキー）。空ならアセット名を使う。

---

## 2. シーンの構成

メニュー **「子育て → チュートリアル → 画面をシーンに作る」** で作る（プレハブ `Assets/Yamaguchi/Prefabs/UI/TutorialCanvas.prefab` にも保存）。**作り直すと Canvas で調整した位置は元に戻る**ので、1回作ったら以後は手で調整する。

```
TutorialCanvas（Canvas Overlay / 並び順 100 / 800x600・match 0.5）… TutorialManager（常に表示）
├ TutorialPanel（ふだん非表示）… TutorialPanelUI
│  ├ Dim（黒60%。後ろを押せないようにする）
│  └ Sheet（クリーム色の台紙 740x580）
│     ├ ImageFrame（黒枠 560x290。ここをスワイプ）… TutorialSwipeArea
│     │  └ Viewport（RectMask2D。はみ出しを隠す）
│     │     └ Track（スライドで動く台）
│     │        ├ Slot_Prev / Slot_Current / Slot_Next … TutorialImageSlot
│     │        │  └ Placeholder（灰色の差し替え枠）└ Note（絵の指示メモ）
│     ├ Arrow_Left / Arrow_Right（最初・最後のページでは隠れる）
│     ├ PageDots … TutorialPageDots（HorizontalLayoutGroup）
│     │  └ DotTemplate（見本の点・Image・非表示）→ 実行時に Dot_1, Dot_2 … を複製
│     ├ TextBlock（CanvasGroup。ページが変わるとふわっと出る）
│     │  ├ Heading（TMP 44 太字）
│     │  └ Body（TMP 17 左寄せ）
│     └ Btn_OK「わかった」（最後のページだけ）
└ Triggers … TutorialScheduler（自動で出すチュートリアルの一覧）
```

- **ページの点はすべて Image。** 見本の点（DotTemplate）の大きさ・画像を変えれば全部の点が変わる。今のページ／ほかのページの画像と色は `TutorialPageDots` の Inspector で変えられる（葉っぱのドット絵などに差し替え可）。
- 画像3枚の**横位置だけ**は、窓（Viewport）の幅に合わせて `TutorialPanelUI` が並べる。それ以外の位置・大きさはコードで決めない。

---

## 3. スクリプト（機能ごとに分割）

| ファイル | 役割 |
|---|---|
| `Core/Tutorial/TutorialPage.cs` | 1ページのデータ（画像・絵の指示メモ・見出し・説明文） |
| `Core/Tutorial/TutorialData.cs` | チュートリアル1本（ID＋ページのリスト）。ScriptableObject |
| `Core/Tutorial/TutorialManager.cs` | 出す窓口。順番待ち・「見た」の記録（セーブキー `tutorial`・順番 60） |
| `Core/Tutorial/TutorialTiming.cs` | 出すタイミングの種類と設定値、人が読む説明（`Describe`） |
| `Core/Tutorial/TutorialScheduler.cs` | 登録したチュートリアルを、それぞれのタイミングで出す（毎日判定） |
| `UI/Tutorial/TutorialPanelUI.cs` | 画面の制御（ページ送り・スライド・時間停止・エディタのプレビュー） |
| `UI/Tutorial/TutorialImageSlot.cs` | 画像1枚の見た目（画像／差し替え枠） |
| `UI/Tutorial/TutorialSwipeArea.cs` | ドラッグ量と速さを知らせるだけ |
| `UI/Tutorial/TutorialPageDots.cs` | ページの点（Image を複製して並べる） |
| `Editor/TutorialUIBuilder.cs` | 「画面をシーンに作る」 |
| `Editor/TutorialEditorWindow.cs` | 「編集ウィンドウ」（出すタイミング・ページ・全チュートリアルの一覧・スケジュール登録）と、TutorialData の Inspector の「編集ウィンドウで開く」ボタン |

### 動きの数値（`TutorialPanelUI` の Inspector・たたき台）
- 滑る時間 0.25秒／ページ幅の2割ドラッグで次へ／800（Canvas単位/秒）以上ではじけば短くても次へ／端で引っぱったときの動きやすさ 0.3／文字のフェード 0.2秒。

### 出るタイミングのルール（`TutorialManager`）
- 他の判断画面が開いている間（`GameClock.PauseRequests > 0`）は順番待ち。**全部閉じてから3フレーム待って**出す（月初めは食事の選択画面が1フレーム遅れて開くため）。
- 開いた時点で「見た」にする。ゲームが終わっている（家出・破綻・ゴール）ときは出さない。
- キーボード：←→でページ送り、最後のページで Enter / Space で閉じる。
- デバッグツールの「全部オート」では、そのまま閉じる（`DebugDialogResolver.TryTutorial`）。

### テスト
- 再生中に編集ウィンドウの「今すぐ出す（テスト）」「見た記録を消す」。
- `TutorialManager` の右クリック「テスト：見た記録を全部消す」。
