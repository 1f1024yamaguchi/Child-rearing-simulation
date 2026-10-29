---
title: リザルト画面を end シーンで出す（巣立ち・家出・家計破綻）と育成記録への保存
updated: 2026-10-29
status: 設計済み（依頼前）
related: [docs/design/ending.md, docs/team/antigravity-result-scene.md, docs/design/title-screen.md, docs/decisions/0020-ending-on-leaving-home.md, docs/decisions/0024-bankruptcy-and-game-end-reasons.md]
---

# リザルト画面を end シーンで出す（巣立ち・家出・家計破綻）と育成記録への保存

> [ending.md](ending.md) 7章のリザルトを、本編（Main）の上に重ねず **`end` シーン（`Assets/Yamaguchi/Scenes/end.unity`）に切り替えて出す**。
> 終わり方は **巣立ち・家出・家計破綻の3つ**（決定 0024 の `GameEndReason`）。どれでも同じ `end` シーンに入り、見出し・文・色・「きっかけ」の欄だけを変える。
> 職業の判定・進路の中身は ending.md が正。この文書は **シーンの切り替え・3つの終わり方の画面・育成記録への保存** を決める。

---

## 1. 決まったこと（2026-10-29）

| 項目 | 内容 |
|---|---|
| リザルトの場所 | 本編とは別の `end` シーン |
| 入るきっかけ | `GameState.OnGameEnded(GameEndReason)` の1本だけを購読（決定 0024 で終了理由がまとまったため） |
| 終わり方の種類 | 新しい enum は作らず **`GameEndReason` をそのまま使う**（GoalReached＝巣立ち／Runaway＝家出／Bankrupt＝家計破綻） |
| 家出・破綻の雰囲気 | どちらも **落ち着いた色**（暗すぎない）。巣立ちだけ明るい色 |
| きっかけの欄 | 家出・破綻のときに出す（3.2） |
| 数字のまとめ | 3つとも全部出す。どこでお金や心の負担が重くなったかを振り返れるのが学習ゲームとして大事なため |
| 育成記録 | **今回のエンディングで保存まで作る**（5章）。一覧画面はタイトル画面の作業で作る |
| セーブ | 何もしない（`AutoSaver` が `OnGameEnded` でセーブを消す。実装済み） |
| 終わったら | ［タイトルへ］で `ResultCarrier.Clear()`・`Ledger.I.Clear()` をしてから `Start_scene` に戻る（Ledger は static なので、消さないと［最初から］で前のプレイの家計簿が残る） |

### 巣立ちのきっかけ（暫定）

進路・巣立ちの判定（ending.md 依頼5 の `GraduationWatcher`）はまだ無い。今は `GameState` が `GoalAgeDays`（7300日＝20歳）で `EndGame(GoalReached)` を呼ぶので、それを **仮の巣立ち** として扱う。
依頼5 で `GraduationWatcher` が巣立ちの日に `EndGame(GoalReached)` を呼ぶようにすれば、EndingFlow は変えずに済む（職業の値を足すだけ）。

### 同じ日に2つ来たら

`GameState.EndGame` は最初の1回しか通らない（`IsGameOver` で止まる）。**先に呼ばれた方の終わり方になる。**
ending.md 6.2 の「家出を優先」は、この仕組みでは保証しない（ほぼ起きないので、これで良しとする）。

---

## 2. 流れ

```
Main シーン
  巣立ち（今は20歳）・家出・家計破綻
     └ どれも GameState.EndGame(reason) → OnGameEnded(reason)
                     ▼
            EndingFlow（Main に置く。1回だけ動く）
              ① GameClock.SetRunning(false)
              ② EndingResultBuilder.Build(reason)  … 終わった瞬間の値を写す
              ③ RecordStore.Add(result)            … 育成記録に1件足す（5章）
              ④ ResultCarrier.Set(result)
              ⑤ 少し待って "end" シーンを読み込む
                     ▼
end シーン
  ResultScreenUI が ResultCarrier.Current を受け取り、上から順に流す → ［タイトルへ］
```

### なぜ値を写して持っていくのか

`GameState`・`GameClock` は `DontDestroyOnLoad`、`Ledger` は static なので残るが、`Household`・`ChildStatus`・`SupportManager`・`RunawayWatcher`・`BankruptcyWatcher` は Main シーンの中にあり、**シーンを切り替えた瞬間に消える**。
だから終わった瞬間の値を `EndingResult`（ただのデータ）に写し、static の `ResultCarrier` で end シーンへ渡す。同じものをそのまま育成記録にも保存する。

---

## 3. EndingResult

`[Serializable]` のただのクラス（MonoBehaviour にしない）。JsonUtility でそのまま保存できる型だけを使う（Dictionary・プロパティは保存されない）。

### 3.1 共通の項目

| 項目 | 内容 | 取得元 |
|---|---|---|
| `reason` | `GameEndReason` | OnGameEnded の引数 |
| `childName` | 子の名前 | 今は名前の項目が無いので EndingFlow の Inspector（既定「みらい」） |
| `endAgeDays` | 終わった日の年齢（日数）。表示は「○歳○か月」 | GameState.AgeDays |
| `finishedAt` | 実時間の日時（文字列 `yyyy/MM/dd HH:mm`。育成記録の並び順用） | DateTime.Now |
| `careerId` / `careerName` | 職業（今は空。依頼5 で埋める。家出・破綻は常に空） | CareerJudge（未作成） |
| `totalChildCost` | 子育てにかかったお金の合計 | `Ledger.I.TotalChildCost` |
| `totalSupport` | 受け取った支援の合計 | `Ledger.I.TotalSupport` |
| `totalIncome` | 親が稼いだお金の合計 | `Ledger.I` の年ごとのまとめ（`YearSummary(age).income`）を0歳〜今の歳まで足す |
| `finalMoney` | 最終の所持金（マイナスもありうる） | GameState.Money |
| `totalScholarship` | 借りた奨学金の合計（今は 0。依頼7 で埋める） | — |
| `grade` / `health` / `childStress` | 最終の成績・体調・子ストレス | ChildStatus（子ストレスは今 RunawayWatcher が見ている値と同じもの） |
| `hobby` / `hobbyLevel` | 趣味と熟練度 | ChildStatus |
| `traits` | 特性の表示名のリスト | ChildStatus |
| `epilogue` | エピローグの文（今は空） | — |

> 合計は Ledger に窓口がすでにある（`TotalChildCost`・`TotalSupport`）。`totalIncome` だけ足し算が要る。Ledger に窓口を足すのではなく、Builder の中で足す。

### 3.2 きっかけ（家出・破綻のときだけ）

| 項目 | 家出 | 家計破綻 |
|---|---|---|
| `causeDays` | 限界のストレスが続いた日数（`RunawayWatcher.DangerDays`） | 赤字が続いた月数（`BankruptcyWatcher.DeficitMonths`） |
| `causeValue` | 最後の子ストレス | 最後の不足額（`-GameState.Money`。正の数で持つ） |

巣立ちのときは 0。

---

## 4. 画面（end シーン）

```
┌──────────────────────────────┐
│ 見出し                        │ ← 巣立ちの日／家出／家計破綻。背景色も変える
│   [職業のイラスト]             │ ← 巣立ちのみ
│   一文                        │ ← 下の表
│                              │
│ ┌ きっかけ ────────────────┐ │ ← 家出・破綻のみ
│ ├ 数字のまとめ ─────────────┤ │ ← 年齢・子育てのお金・支援・稼いだお金・最終の所持金・奨学金
│ ├ 子どもの最終ステータス ───┤ │ ← 成績・趣味・体調・特性
│ └ エピローグ（あれば） ─────┘ │
│        ［スキップ］［タイトルへ］│
└──────────────────────────────┘
```

| | 巣立ち | 家出 | 家計破綻 |
|---|---|---|---|
| 見出し | 巣立ちの日 | 家出 | 家計破綻 |
| 一文 | 「{名前}は {職業} になりました」（職業が空なら「{名前}は 巣立っていきました」） | 「{名前}は {○歳○か月}で、家を出ていきました」 | 「{○歳○か月}のとき、家計が立ち行かなくなりました」 |
| きっかけ | — | 「ストレスが限界のまま {causeDays}日 が過ぎました（最後のストレス {causeValue}）」 | 「赤字が {causeDays}か月 続きました（最後の不足額 {causeValue}円）」 |
| 年齢の行 | 巣立った年齢 | 家出した年齢 | 終わった年齢 |
| 色 | 明るい | 落ち着いた色 | 落ち着いた色（家出と同じ系統で、少し色味を変える） |

- 文と色は全部 Inspector で変えられる（[SerializeField]）。
- 上から順に流す。段ごとの待ち秒数は Inspector。［スキップ］かクリックで全部出す。
- お金は「○○万円」（1万円未満は切り捨て）。最終の所持金がマイナスなら「−○○万円」。**0 の項目は出さない**（ただし最終の所持金は 0 でも出す）。
- 夢と違う職業でも「叶わなかった」とは出さない（ending.md 5.3）。
- 記録の欄は既存の `status_scroll` / `StatusListUI` と同じ作り（[../code/ui-scroll-patterns.md](../code/ui-scroll-patterns.md)）。
- **end シーンを単独で再生したとき**（`ResultCarrier` が空）は、Inspector の **テスト用の EndingResult** を表示する。巣立ち・家出・破綻を enum で切り替えられるようにする。
- Canvas は Main_test2 と同じ（Scale With Screen Size／800×600）。

---

## 5. 育成記録への保存（RecordStore）

[title-screen.md](title-screen.md) 6章のとおり、**セーブとは別のファイル** に、終わるたびに1件足す。

| 項目 | 内容 |
|---|---|
| ファイル | `Application.persistentDataPath/records.json` |
| 中身 | `RecordList { List<EndingResult> records }`（JsonUtility 用の入れ物） |
| 件数 | 上限なし |
| 書くとき | EndingFlow の ③。セーブの削除（AutoSaver）とは別ファイルなので、順番は気にしなくてよい |
| 読み出し | `RecordStore.LoadAll()`：新しい順（finishedAt の降順）で返す。ファイルが無い・壊れているときは空のリスト（例外を出さない） |
| 確認用 | `RecordStore.Count`、Editor メニュー「ChildRearing → 育成記録をログ」「ChildRearing → 育成記録を消す」 |

一覧・詳細の画面はタイトル画面の作業で作る（title-screen.md 6章）。それまで育成記録ボタンは interactable=false のまま。

---

## 6. クラス（機能ごとに分ける）

| 名前 | 種類 | 置き場所 | 役割 |
|---|---|---|---|
| `EndingResult` | データ | Core/Ending | 3章の項目。表示用に `AgeText`（○歳○か月）を持つ（メソッドで。保存されない） |
| `EndingResultBuilder` | static | Core/Ending | Main にある値から EndingResult を作る |
| `ResultCarrier` | static | Core/Ending | シーンをまたいで EndingResult を1つ渡すだけ |
| `RecordStore` | static | Core/Ending | records.json への追加・読み出し |
| `EndingFlow` | MonoBehaviour | Core/Ending | OnGameEnded を受けて、時間を止め、写して、保存して、end シーンへ |
| `ResultScreenUI` | UI | UI/Result | end シーンで流して表示する進行役。計算しない |
| `ResultHeaderUI` | UI | UI/Result | 見出し・一文・イラスト・背景色 |
| `ResultStatsUI` | UI | UI/Result | きっかけ・数字・ステータスを StatusListUI に並べる。お金の書式は1か所 |

ending.md 8章の `ResultScreenUI` は **end シーンに置く** ものとして扱う（Main には置かない）。

---

## 7. まだ決めていないこと

- end シーンに BGM を付けるか（AudioManager が DontDestroyOnLoad か要確認）。
