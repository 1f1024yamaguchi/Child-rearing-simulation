---
title: リザルトの end シーンの実装法と Antigravity への依頼文
updated: 2026-10-21
status: 依頼前
related: [docs/design/result-scene.md, docs/design/ending.md, docs/team/antigravity-ending.md]
---

# リザルトの end シーンの実装法と Antigravity への依頼文

> 仕様は [../design/result-scene.md](../design/result-scene.md)。
> [antigravity-ending.md](antigravity-ending.md) の **依頼5・6 のうち「リザルト」部分を、end シーンに分けて先に作る** ための依頼。
> 2回に分けて頼み、1回ごとに Unity で確認してから次へ進む。

---

## 1. antigravity-ending.md との関係

| ここ | antigravity-ending.md 側 | どうなるか |
|---|---|---|
| 依頼R1 で `EndingResult` を作る | 依頼5 の 4.（EndingResult を作る） | 依頼5 では **作らずに職業まわりの項目を足す** だけにする |
| 依頼R1 の `EndingFlow` は `GameState.OnGameOver` で巣立ち扱い | 依頼5 の `GraduationWatcher.OnGraduated` | 依頼5 のあと **OnGraduated に付け替える** |
| 依頼R2 の `ResultScreenUI`（end シーン） | 依頼6 の 3.（ResultScreenUI） | 依頼6 では **作らない**。あゆみ・職業のイラストの表示を足すだけ |

先に依頼5 を済ませた場合も、R1 の「EndingResult を作る」を「既にあるものに足す」と読み替えれば使える。

---

## 2. 依頼文

### 依頼R1：終わったら値を写して end シーンへ（画面なし）

```
docs/design/result-scene.md の 1〜3章・5章 と docs/design/ending.md の 6.2・7章・8章 を読んでから作業してください。
置き場所は Assets/Yamaguchi/Scripts/Core/Ending/。

1. EndingReason.cs：enum EndingReason { 巣立ち, 家出 }
2. EndingResult.cs（[Serializable]・MonoBehaviour にしない）：
   reason、childName、endAgeDays（int）、finishedAt（string）、
   職業id・職業名（今は空）、
   子育てにかかったお金・受け取った支援の合計・親が稼いだお金の合計・奨学金の合計（long。取れないものは 0）、
   最終の成績・趣味・趣味Lv・体調・子ストレス・特性の表示名リスト、エピローグ文（空でよい）。
   表示用に AgeText（「○歳○か月」）を読み取り専用で持つ。
   ※すでに EndingResult.cs がある場合は作り直さず、足りない項目だけ足す。
3. EndingResultBuilder.cs（static）：Build(EndingReason reason) で今の値から EndingResult を作る。
   取得元は Household / ChildStatus / GameState / IncomeHistory / SupportManager。
   合計を返す窓口が無いものは勝手に作らず 0 を入れ、報告に「窓口が必要」と書く。
4. ResultCarrier.cs（static）：Current / HasResult / Set(EndingResult) / Clear()。
5. EndingFlow.cs（MonoBehaviour。Main に置く）：
   - RunawayWatcher.OnRunaway と GameState.OnGameOver を購読（今の書き方に合わせ OnEnable/OnDisable か Start で）。
     GameState.OnGameOver は、GraduationWatcher（ending.md 依頼5）ができるまでの巣立ちの代わり。
     Tooltip とコメントに「依頼5 のあと GraduationWatcher.OnGraduated に付け替える」と書く。
   - どちらか一方で1回だけ動く（フラグ）。RunawayWatcher.HasRunAway が true なら巣立ちは無視（家出を優先）。
   - ① GameClock.SetRunning(false) ② EndingResultBuilder.Build → ResultCarrier.Set
     ③ [SerializeField] 待ち秒数（既定 2）を WaitForSecondsRealtime で待つ
     ④ [SerializeField] シーン名（既定 "end"）を SceneManager.LoadScene
   - 確認用 [ContextMenu]：「Debug/今すぐ巣立ち」「Debug/今すぐ家出」（RunawayWatcher の判定は通さず、この流れだけ動かす）

守ること：
- セーブは触らない（AutoSaver がすでに巣立ち・家出でセーブを消している）。
- RunawayWatcher の家出の判定・GameState・SaveSystem の中身は変えない。
- シーンファイル・Build Settings は触らない（人間がやる）。
- 他のメンバーのフォルダのファイルは変更しない。
- Unity の MCP ツール（unityMCP）は使わないでください。終わったら、EndingFlow をどの GameObject に付けるか、Inspector の設定、Unity で確認してほしいことを箇条書きで教えてください。
```

**確認（依頼R1）**
1. File → Build Settings に `Start_scene`・Main_test2・`end` が入っていることを確認（無ければ足す）。
2. Main_test2 に EndingFlow を置き、Play → 「Debug/今すぐ家出」→ 時間が止まり、2秒後に end シーンに切り替わる。
3. 「Debug/今すぐ巣立ち」でも同じく切り替わる。
4. 切り替わった後、ログで ResultCarrier.Current の中身（年齢・お金・ステータス）が入っていることを確認。
5. 続きからのセーブが消えている（タイトルの「続きから」が押せない）。

---

### 依頼R2：end シーンのリザルト画面

```
docs/design/result-scene.md の 4章・5章、docs/design/ending.md 5.3・7章、docs/code/ui-scroll-patterns.md を読んでから作業してください。
置き場所は Assets/Yamaguchi/Scripts/UI/Result/。UI はプレハブにする（シーンを直接いじらない）。

1. ResultScreenUI.cs（進行役）：
   - Start で ResultCarrier.Current を受け取る。空なら [SerializeField] のテスト用 EndingResult を使う
     （巣立ち用・家出用の2つを持ち、bool で切り替え）。
   - 見出し → 職業（または家出の文）→ 数字のまとめ → 最終ステータス → エピローグ → ボタン の順にコルーチンで出す。
     段ごとの待ち秒数は [SerializeField]。［スキップ］か画面クリックで全部すぐ出す。
   - ［タイトルへ］：ResultCarrier.Clear() → [SerializeField] シーン名（既定 "Start_scene"）を読み込む。
   - end シーンに来た時点で GameClock が止まっていることを前提にする（動かさない）。
2. ResultHeaderUI.cs：
   - 巣立ち：見出し「巣立ちの日」、「{childName}は {職業名} になりました」、イラスト。職業名が空なら「{childName}は 巣立っていきました」。
   - 家出：見出し「家出」、「{AgeText}で、家を出ていきました」、イラストは隠す。
   - 見出しの文字・文の書式・背景色は巣立ち／家出で別々に [SerializeField]。
   - 夢と違っても「叶わなかった」とは出さない。
3. ResultStatsUI.cs：
   - 既存の StatusListUI に SetStatus で行を並べる。
   - 年齢の行は 巣立ちなら「巣立った年齢」、家出なら「家出した年齢」。
   - お金は「○○万円」（1万円未満は切り捨て）。書式は1つのメソッドにまとめる。値が 0 の項目は出さない。
4. 計算はしない。値は EndingResult から受け取るだけ。

TextMeshPro の注意：
- TextOverflowModes.Ellipsis は使わない（文字が描画されなくなる）。
- 日本語フォントは Resources.Load<TMP_FontAsset>("JPFont")。

守ること：
- Core 側・シーンファイル・他のメンバーのフォルダは変更しない。
- Unity の MCP ツール（unityMCP）は使わないでください。終わったら、作るプレハブの階層、end シーンへの置き方（Canvas は Main_test2 と同じ Scale With Screen Size／800×600）、Inspector の設定、確認手順を箇条書きで教えてください。
```

**確認（依頼R2）**
1. end シーンだけを開いて Play → テスト用の巣立ちの結果が順に流れる。bool を切り替えて家出の見た目も確認。
2. ［スキップ］で全部すぐ出る。
3. Main_test2 から「Debug/今すぐ家出」→ end シーンで家出のリザルトが出る。0 の項目は出ていない。
4. ［タイトルへ］で Start_scene に戻り、「続きから」が押せない状態になっている。

---

## 3. 進み具合

| 依頼 | 内容 | 状態 |
|---|---|---|
| R1 | 終わったら値を写して end シーンへ | 未依頼 |
| R2 | end シーンのリザルト画面 | 未依頼 |
| （後で） | 依頼5 のあと EndingFlow を GraduationWatcher.OnGraduated に付け替え | 依頼5 待ち |
