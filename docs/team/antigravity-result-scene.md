---
title: リザルトの end シーン・育成記録の保存の実装法と Antigravity への依頼文
updated: 2026-10-29
status: 依頼前
related: [docs/design/result-scene.md, docs/design/ending.md, docs/team/antigravity-ending.md, docs/decisions/0024-bankruptcy-and-game-end-reasons.md]
---

# リザルトの end シーン・育成記録の保存の実装法と Antigravity への依頼文

> 仕様は [../design/result-scene.md](../design/result-scene.md)。
> [antigravity-ending.md](antigravity-ending.md) の依頼5・6 のうち **「リザルト」部分を end シーンに分けて先に作る** ための依頼。
> 2回に分けて頼み、1回ごとに Unity で確認してから次へ進む。
> **前提**：決定 0024（`GameEndReason`・`GameState.EndGame`・`OnGameEnded`・`BankruptcyWatcher`）がコミットされていること。

---

## 1. antigravity-ending.md との関係

| ここ | antigravity-ending.md 側 | どうなるか |
|---|---|---|
| 依頼R1 で `EndingResult` を作る | 依頼5 の 4.（EndingResult を作る） | 依頼5 では **作らずに職業まわりの項目を埋める** だけ |
| 依頼R1 の `EndingFlow` は `OnGameEnded` を購読 | 依頼5 の `GraduationWatcher.OnGraduated` | 依頼5 では GraduationWatcher が巣立ちの日に `GameState.EndGame(GoalReached)` を呼ぶようにする。**EndingFlow は付け替え不要** |
| 依頼R2 の `ResultScreenUI`（end シーン） | 依頼6 の 3.（ResultScreenUI） | 依頼6 では **作らない**。あゆみ・職業のイラストの表示を足すだけ |

---

## 2. 依頼文

### 依頼R1：終わったら値を写し、育成記録に保存して end シーンへ（画面なし）

```
docs/design/result-scene.md の全章と、docs/design/ending.md の 7章・8章、docs/decisions/0024-bankruptcy-and-game-end-reasons.md を読んでから作業してください。
置き場所は Assets/Yamaguchi/Scripts/Core/Ending/。終わり方の enum は新しく作らず、既存の GameEndReason を使ってください。

1. EndingResult.cs（[Serializable]・MonoBehaviour にしない。JsonUtility で保存できる型だけ）：
   result-scene.md 3.1・3.2 の項目をすべて持つ。
   お金は long（finalMoney はマイナスもありうる）。traits は List<string>。
   表示用の GetAgeText()（「○歳○か月」）はメソッドで持つ（保存されない）。
   ※すでに EndingResult.cs がある場合は作り直さず、足りない項目だけ足す。
2. EndingResultBuilder.cs（static）：Build(GameEndReason reason, string childName) で今の値から EndingResult を作る。
   - 取得元は result-scene.md 3章の表のとおり（GameState / ChildStatus / Ledger.I / RunawayWatcher / BankruptcyWatcher）。
   - totalChildCost = Ledger.I.TotalChildCost、totalSupport = Ledger.I.TotalSupport。
   - totalIncome は Ledger.I.YearSummary(age).income を 0歳〜今の歳まで足す（Ledger は変更しない）。
   - causeDays / causeValue は reason が Runaway・Bankrupt のときだけ入れる（3.2）。Bankrupt の causeValue は -GameState.Money（正の数）。
   - careerId / careerName / totalScholarship / epilogue は空・0 のまま（依頼5・7 で埋める）。
   - シーン内のものは FindObjectOfType で探し、見つからなければその項目は 0・空にして Debug.LogWarning。
3. ResultCarrier.cs（static）：Current / HasResult / Set(EndingResult) / Clear()。
4. RecordStore.cs（static）：result-scene.md 5章のとおり。
   - Application.persistentDataPath/records.json に RecordList { List<EndingResult> records } を JsonUtility で保存。
   - Add(EndingResult)：読み込み → 末尾に足す → 書き込み。
   - LoadAll()：finishedAt の新しい順で返す。ファイルが無い・壊れているときは空のリストを返し、例外を出さない（壊れていたら LogWarning）。
   - Count、DeleteAll()。
   - Editor メニュー（Assets/Yamaguchi/Scripts/Editor/ に RecordStoreMenu.cs）：「ChildRearing/育成記録をログ」「ChildRearing/育成記録を消す」。
5. EndingFlow.cs（MonoBehaviour。Main に置く）：
   - GameState.I.OnGameEnded を購読（GameState は DontDestroyOnLoad なので、OnDisable で必ず解除）。
   - 1回だけ動く（フラグ）。
   - ① GameClock.SetRunning(false)
     ② EndingResultBuilder.Build(reason, childName) … childName は [SerializeField]（既定 "みらい"）
     ③ RecordStore.Add(result)
     ④ ResultCarrier.Set(result)
     ⑤ [SerializeField] 待ち秒数（既定 2）を WaitForSecondsRealtime で待つ
     ⑥ [SerializeField] シーン名（既定 "end"）を SceneManager.LoadScene
   - 確認用 [ContextMenu]：「Debug/今すぐ巣立ち」「Debug/今すぐ家出」「Debug/今すぐ家計破綻」
     → それぞれ GameState.I.EndGame(GoalReached / Runaway / Bankrupt) を呼ぶだけ（各 Watcher の判定は通さない）。

守ること：
- GameState・RunawayWatcher・BankruptcyWatcher・AutoSaver・SaveSystem・Ledger の中身は変えない。
- セーブは触らない（AutoSaver が OnGameEnded でセーブを消している）。records.json はセーブとは別ファイル。
- シーンファイル・Build Settings は触らない（人間がやる）。
- 他のメンバーのフォルダのファイルは変更しない。
- スクリプトは機能ごとに分け、1ファイルに詰め込まない。
- Unity の MCP ツール（unityMCP）は使わないでください。終わったら、EndingFlow をどの GameObject に付けるか、Inspector の設定、Unity で確認してほしいことを箇条書きで教えてください。
```

**確認（依頼R1）**
1. File → Build Settings に `Start_scene`・`Main_test2`・`end` が入っていることを確認（無ければ足す）。
2. Main_test2 の GameManager の下に EndingFlow を置く。
3. Play → 数日進めてから EndingFlow の「Debug/今すぐ家出」→ 時間が止まり、2秒後に end シーンに切り替わる。
4. 「ChildRearing/育成記録をログ」→ 1件増えていて、reason・年齢・子育てのお金・支援・稼いだお金・きっかけ（家出の日数・ストレス）が入っている。
5. 「Debug/今すぐ巣立ち」「Debug/今すぐ家計破綻」でも同じく切り替わり、記録が増える。破綻は causeDays・causeValue（不足額）が入っている（所持金がプラスなら不足額は 0 以下になるのは想定どおり）。
6. タイトルに戻って「続きから」が押せない（セーブが消えている）。
7. 「ChildRearing/育成記録を消す」で 0 件に戻る。

---

### 依頼R2：end シーンのリザルト画面

```
docs/design/result-scene.md の 4章・6章、docs/design/ending.md 5.3・7章、docs/code/ui-scroll-patterns.md を読んでから作業してください。
置き場所は Assets/Yamaguchi/Scripts/UI/Result/。UI はプレハブにする（シーンを直接いじらない）。

1. ResultScreenUI.cs（進行役）：
   - Start で ResultCarrier.Current を受け取る。空なら [SerializeField] のテスト用 EndingResult を使う
     （巣立ち用・家出用・破綻用の3つを持ち、[SerializeField] GameEndReason testReason で切り替え）。
   - 見出し → 一文 → きっかけ（家出・破綻のみ）→ 数字のまとめ → 最終ステータス → エピローグ（空なら飛ばす）→ ボタン
     の順にコルーチンで出す。段ごとの待ち秒数は [SerializeField]。［スキップ］か画面クリックで全部すぐ出す。
   - ［タイトルへ］：ResultCarrier.Clear() → Ledger.I.Clear() → [SerializeField] シーン名（既定 "Start_scene"）を読み込む。
     （Ledger は static なので、消さないと［最初から］で前のプレイの家計簿が残るため）
   - end シーンに来た時点で GameClock が止まっている前提（動かさない）。
2. ResultHeaderUI.cs：
   - result-scene.md 4章の表のとおり、reason ごとに 見出し・一文の書式・背景色 を [SerializeField] で別々に持つ。
   - 巣立ちのときだけ職業のイラストを出す（Sprite が無ければ隠す）。職業名が空なら「{名前}は 巣立っていきました」。
   - 夢と違っても「叶わなかった」とは出さない。
3. ResultStatsUI.cs：
   - 既存の StatusListUI に SetStatus で行を並べる。
   - きっかけの行（家出・破綻のみ）、年齢の行（巣立った年齢／家出した年齢／終わった年齢）、数字のまとめ、最終ステータスの順。
   - お金は「○○万円」（1万円未満は切り捨て。マイナスは「−○○万円」）。書式は1つのメソッドにまとめる。
   - 値が 0 の項目は出さない。ただし最終の所持金は 0 でも出す。
4. 計算はしない。値は EndingResult から受け取るだけ。

TextMeshPro の注意：
- TextOverflowModes.Ellipsis は使わない（文字が描画されなくなる）。
- 日本語フォントは Resources.Load<TMP_FontAsset>("JPFont")。

守ること：
- Core 側・シーンファイル・他のメンバーのフォルダは変更しない。
- Unity の MCP ツール（unityMCP）は使わないでください。終わったら、作るプレハブの階層、end シーンへの置き方（Canvas は Main_test2 と同じ Scale With Screen Size／800×600）、Inspector の設定、確認手順を箇条書きで教えてください。
```

**確認（依頼R2）**
1. end シーンだけを開いて Play → テスト用の巣立ちの結果が順に流れる。testReason を Runaway・Bankrupt に切り替えて、見出し・一文・きっかけ・色が変わることを確認。
2. ［スキップ］で全部すぐ出る。
3. Main_test2 から「Debug/今すぐ家計破綻」→ end シーンで破綻のリザルトが出る。0 の項目は出ていない。
4. ［タイトルへ］で Start_scene に戻り、「続きから」が押せない。［最初から］で始めて家計簿を開くと、前のプレイの記録が残っていない。

---

## 3. 進み具合

| 依頼 | 内容 | 状態 |
|---|---|---|
| R1 | 終わったら値を写し、育成記録に保存して end シーンへ | 未依頼 |
| R2 | end シーンのリザルト画面（巣立ち・家出・家計破綻） | 未依頼 |
| （後で） | 依頼5：GraduationWatcher が巣立ちの日に EndGame(GoalReached) を呼ぶ・職業を EndingResult に埋める | 依頼1〜4 待ち |
| （後で） | タイトル画面の育成記録の一覧・詳細（RecordStore.LoadAll を使う） | R1 のあと |
