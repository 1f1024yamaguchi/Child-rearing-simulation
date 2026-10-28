---
title: リザルト画面を end シーンで出す（巣立ち・家出）
updated: 2026-10-21
status: 設計中（たたき台・要合意）
related: [docs/design/ending.md, docs/team/antigravity-ending.md, docs/team/antigravity-result-scene.md, docs/design/title-screen.md, docs/decisions/0020-ending-on-leaving-home.md]
---

# リザルト画面を end シーンで出す（巣立ち・家出）

> [ending.md](ending.md) 7章のリザルトを、本編（Main）の上に重ねるのではなく **`end` シーン（`Assets/Yamaguchi/Scenes/end.unity`）に切り替えて出す**。
> **巣立ちでも家出でも同じ `end` シーン** に入り、見出し・職業の欄・色だけを変える。
> 職業の判定・進路・EndingResult の中身は ending.md が正。この文書は **「シーンを分ける部分」と「家出のリザルト」** だけを足す。

---

## 1. 決めたいこと

| 項目 | 案 |
|---|---|
| リザルトの場所 | 本編とは別の `end` シーン |
| 入る条件 | 巣立ち（GraduationWatcher）と 家出（RunawayWatcher.OnRunaway）の両方 |
| 家出のとき | 職業は出さない。「○歳○か月で、家を出ていきました」。記録の数字は巣立ちと同じく全部出す |
| セーブ | 何もしない（`AutoSaver` が巣立ち・家出のどちらでもセーブを消す。実装済み） |
| 終わったら | ［タイトルへ］で `Start_scene` に戻る |

家出でも数字を全部出すのは、**どこでお金や心の負担が重くなったかを振り返れる** のが学習ゲームとして大事なため。

---

## 2. 流れ

```
Main シーン
  ├ 巣立ち：GraduationWatcher.OnGraduated(EndingResult)  … ending.md 依頼5
  │        （依頼5 ができるまでは GameState.OnGameOver で代用）
  └ 家出  ：RunawayWatcher.OnRunaway
                     ▼
            EndingFlow（Main に置く。1回だけ動く）
              ① GameClock.SetRunning(false)
              ② EndingResult を用意（巣立ちは受け取ったもの／家出はその場で作る）
              ③ ResultCarrier.Set(result)
              ④ 少し待って "end" シーンを読み込む
                     ▼
end シーン
  ResultScreenUI が ResultCarrier.Current を受け取り、上から順に流す → ［タイトルへ］
```

### なぜ値を写して持っていくのか

`GameState` と `GameClock` は `DontDestroyOnLoad` で残るが、`Household`・`ChildStatus`・`SupportManager`・`IncomeHistory` は Main シーンの中にあり、**シーンを切り替えた瞬間に消える**。
だから終わった瞬間の値を `EndingResult`（ただのデータ）に写し、static の `ResultCarrier` に置いて end シーンへ渡す。
`EndingResult` はそのまま育成記録（[title-screen.md](title-screen.md) 6章）の保存にも使える。

### 巣立ちと家出が同じ日に来たら

ending.md 6.2 どおり **家出を優先**。EndingFlow は先に来た方で1回だけ動き、`RunawayWatcher.HasRunAway` が立っていれば巣立ちは無視する。

---

## 3. EndingResult に足すもの

ending.md 依頼5 の `EndingResult` に、次を足す（依頼5 より先に作る場合は、ここで EndingResult を作り、依頼5 で職業まわりを埋める）。

| 項目 | 内容 |
|---|---|
| `reason` | `EndingReason { 巣立ち, 家出 }` |
| `childName` | 見出しに使う子の名前 |
| `endAgeDays` | 終わった日の年齢（日数）。表示は「○歳○か月」 |
| `finishedAt` | 実時間の日時（育成記録の並び順用。文字列で持つ） |

家出のときは職業の項目を空にし、進路のあゆみ・受験の記録は **その時点までのもの** を入れる。

**今のコードで取れない値は 0 のまま**（子育てにかかったお金＝家計簿が未実装、支援の合計＝SupportManager に合計の窓口が無い）。画面は 0 の項目を出さない（ending.md 依頼6 と同じ）。

---

## 4. 画面（end シーン）

```
┌──────────────────────────────┐
│          巣立ちの日            │ ← 家出なら「家出」。背景色も変える
│   [職業のイラスト]             │
│   ○○は △△ になりました        │ ← 家出：「○歳○か月で、家を出ていきました」
│                              │
│ ┌ あゆみ ──────────────────┐ │ ← 高校 → 大学 → 職業（ending.md 7章の2）
│ ├ 数字のまとめ ─────────────┤ │ ← 巣立ち/家出の年齢・子育てのお金・支援・稼いだお金・奨学金
│ ├ 子どもの最終ステータス ───┤ │ ← 成績・趣味・体調・特性
│ └ エピローグ（あれば） ─────┘ │
│        ［スキップ］［タイトルへ］│
└──────────────────────────────┘
```

- 上から順に流す。段ごとの待ち秒数は Inspector。［スキップ］かクリックで全部出す。
- 記録の欄は既存の `status_scroll` / `StatusListUI` と同じ作り（[../code/ui-scroll-patterns.md](../code/ui-scroll-patterns.md)）。
- 夢と違う職業でも「叶わなかった」とは出さない（ending.md 5.3）。
- **end シーンを単独で再生したとき**（`ResultCarrier` が空）は、Inspector の **テスト用の EndingResult** を表示する。本編を巣立ちまで進めずに見た目を調整するため。巣立ち用・家出用を切り替えられるようにする。
- Canvas は Main_test2 と同じ（Scale With Screen Size／800×600）。

---

## 5. クラス（機能ごとに分ける）

| 名前 | 種類 | 置き場所 | 役割 |
|---|---|---|---|
| `EndingReason` | enum | Core/Ending | 巣立ち／家出 |
| `EndingResult` | データ | Core/Ending | ending.md 8章のもの＋3章の項目 |
| `EndingResultBuilder` | static | Core/Ending | Main にある値から EndingResult を作る（家出用。依頼5 で巣立ちも使う） |
| `ResultCarrier` | static | Core/Ending | シーンをまたいで EndingResult を1つ渡すだけ |
| `EndingFlow` | MonoBehaviour | Core/Ending | 巣立ち・家出を受けて、時間を止め、写して、end シーンへ |
| `ResultScreenUI` | UI | UI/Result | end シーンで流して表示するだけ。計算しない |
| `ResultHeaderUI` | UI | UI/Result | 見出し・職業／家出の文・イラスト・色 |
| `ResultStatsUI` | UI | UI/Result | 数字とステータスを StatusListUI に並べる。お金の書式は1箇所 |

ending.md 8章の `ResultScreenUI` は **end シーンに置く** ものとして扱う（Main には置かない）。

---

## 6. 決めないといけないこと

- 家出したときの文と雰囲気（暗くするか、落ち着いた色にするか）。
- 家出のリザルトに「何がきっかけだったか」（限界にいた日数・最後のストレス）を出すか。
- 育成記録への保存をこのタイミング（EndingFlow）で行うか、タイトル画面の育成記録を作るときにするか。
- end シーンに BGM を付けるか（AudioManager は DontDestroyOnLoad か要確認）。
