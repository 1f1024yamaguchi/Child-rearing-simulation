---
title: Antigravity 用プロンプト（子どもステータス・特性・状態）
updated: 2026-09-25
status: 運用中
related: [docs/design/child-status.md, docs/design/traits.md, docs/design/effects.md]
---

# Antigravity 用プロンプト（子どもステータス・特性・状態）

> 子どものステータスと、特性・状態の実装を Antigravity に依頼するための命令文。
> **段階ごとに分けて渡す。** 毎回「共通の前文」を頭に付けてから、その段階の指示を貼る。
> 仕様は [../design/child-status.md](../design/child-status.md) と [../design/traits.md](../design/traits.md)。

## 段階の全体像

| 段階 | 内容 | 終わると何ができるか |
|---|---|---|
| ① | `PersonStatus` を作り、`ParentStatus` を移行 | 見た目は何も変わらない（土台の入れ替え） |
| ② | `ChildStatus` ＋ 毎日の成長計算 ＋ 家出判定 | 子どもの値が時間とともに動く（UIはまだ無い） |
| ②-b | 段階②のレビュー指摘の修正 | ③④がそのまま乗る状態になる |
| ③ | 特性（Trait）・状態（Condition）と補正計算 | Inspector で特性を付けると効果が出る |
| ③-b | 状態の毎日の効果・残り日数の通知・確認用メニュー | 風邪気味が毎日確実に効く。右クリックで特性・状態を付けられる |
| ④ | UI スクリプト | 子どもカードが表示される（配置は人間が行う） |

## 始める前に確認すること

- **③に入る前に、有坂さん・草尾さん・丸山さんに `effects.md` の `EffectKind` の名前を確定してよいか一声かける。** 3機能が共有する名前のため。
- `GameState.Stress` は旧仕様として残す。削除しない（`Assets/Maruyama/StressUI.cs` が参照しているため）。
- Effect / EffectApplier の土台は別担当。この一連の作業では作らない。
  そのため、**③④が終わっても特性はイベントで自動的には付かない**（Inspector から手で付けて動作確認する）。

---

## 共通の前文（毎回、段階の指示の前に付ける）

```
あなたは Unity 製の子育てシミュレーションゲームの実装を担当します。

【必ず最初に読むもの】
1. リポジトリ直下の AGENTS.md（エージェント向けルールとドキュメント索引）
2. docs/code/architecture.md（GameState の API、時間の流れ）
3. 今回の作業に対応する docs/design/ の仕様書（各段階の指示で指定します）

【守ること】
- 仕様を推測しない。ドキュメントに書かれていないことは「未定」として
  作業を止め、何が分からないかを報告する。勝手に決めない。
- docs/design/ の数値はすべて調整前のたたき台。正式な仕様として扱わない。
  数値は [SerializeField] にして Inspector から変えられる形にする。
- 機能ごとにクラス・ファイルを分ける。1つのファイルに長々と書かない。
- お金・年齢は GameState.I の窓口メソッドを経由する。自前のフィールドで持たない。
- Assets/Arisaka/ Assets/Kusao/ Assets/Maruyama/ は変更しない。
  作るファイルはすべて Assets/Yamaguchi/Scripts/ の下に置く。
- シーンファイル（.unity）は編集しない。プレハブも作らない。
  シーンへの配置は人間が手で行うので、Inspector で繋げるように
  必要な参照はすべて [SerializeField] で公開し、日本語の [Tooltip] を付ける。
- コメントは日本語で書く。既存コードの文体に合わせる。

【今回やらないこと】
- Effect / EffectApplier の土台（docs/design/effects.md）はまだ作らない。
  別の担当が作る予定なので、勝手に実装しないこと。
- GameState.Stress は旧仕様として残す。削除もしない。

【終わったら報告すること】
- 追加・変更したファイルの一覧と、それぞれ何をするものか1行ずつ
- Unity のコンソールにエラー・警告が出ていないこと
- 仕様が足りずに判断できなかった点があれば、その一覧
```

---

## 段階①：PersonStatus への移行

```
docs/design/child-status.md の 1章「誰が何を持つか」と 2章「書き込みの窓口は
ひとつにする」を読んでから作業してください。

【目的】
母親・父親・子どもに共通する「人」の土台クラスを作り、既存の ParentStatus を
その上に乗せ替える。この段階ではゲームの挙動を一切変えない。

【作るもの】
Assets/Yamaguchi/Scripts/Core/PersonStatus.cs
- abstract class PersonStatus : MonoBehaviour
- 表示名（displayName）とストレス（stress, 0〜100）を持つ
- 読み取り：DisplayName / Stress(int) / StressLevel
- 書き込み：AddStress(float) / SetStress(float)
- 変更通知：OnStressChanged(int)
- StressLevel は enum（良好 0-30 / 普通 31-70 / 危険 71-90 / 限界 91-100）。
  境界の値は docs/design/rules-and-stats.md「ストレス値の段階」に合わせる。

【変更するもの】
Assets/Yamaguchi/Scripts/Core/ParentStatus.cs
- MonoBehaviour ではなく PersonStatus を継承する
- displayName / stress / DisplayName / Stress / AddStress / OnStressChanged を
  PersonStatus に移し、ParentStatus からは削除する
- フィールド名は変えないこと（シーンに保存済みの値が外れるため）
- 役職・月給・ワークライフバランス関連はそのまま残す

【触らないもの】
- GameState.cs（Stress は旧仕様として残す）
- Assets/Maruyama/StressUI.cs
- SaveSystem.cs
- シーンファイル

【確認】
- Unity がコンパイルを通ること
- Main_test2 シーンを再生し、母親・父親カードの月給・日給・積立・ストレスが
  これまでどおり表示されることを目視で確認できる状態になっていること
  （再生確認は人間が行うので、確認手順を報告に書く）
```

---

## 段階②：ChildStatus と毎日の成長計算

```
docs/design/child-status.md の 1章〜4章を読んでから作業してください。
数値は 3章の表に従いますが、すべて調整前のたたき台です。

【目的】
子どものステータスを持つクラスと、それを毎日動かす仕組みを作る。
UI はこの段階では作らない。

【作るもの】

Assets/Yamaguchi/Scripts/Core/ChildStatus.cs
- class ChildStatus : PersonStatus
- 持つ値：
  studyPlayBalance（0〜1、0が勉強寄り、既定0.5）
  grade（成績 0〜100、既定50）
  health（体調 0〜100、既定80）
  hobby（趣味の種類）、hobbyExp（経験値）、expPerLevel（既定100）
  passLine（合格ライン 0〜100、既定60）
- 趣味の種類は enum HobbyKind { なし, スポーツ, 音楽, 芸術, ゲーム, 読書 }
- 読み取り：StudyPlayBalance / Grade(int) / Health(int) / Hobby / HobbyLevel /
  PassLine / IsAbovePassLine / AgeYears / ProfileLine
- AgeYears は GameState.I.AgeYears から求める（自前で年齢を持たない）
- ProfileLine は「8歳・スポーツが好き」の形。趣味が なし のときは
  「8歳・好きなことを探し中」
- 書き込み：SetStudyPlayBalance / AddGrade / AddHealth / AddHobbyExp / SetHobby
- 変更通知：OnStudyPlayBalanceChanged / OnGradeChanged / OnHealthChanged /
  OnHobbyChanged
- 値のクランプは書き込みメソッドの中で行う。外から直接書き換えられる
  public フィールドは作らない。

Assets/Yamaguchi/Scripts/Core/ChildGrowthSystem.cs
- DayTicker.I.OnDayPassed を購読し、1日ぶんの成長を適用する
- 計算式は docs/design/child-status.md 3章の表のとおり
- 表の値は「1ヶ月あたり」なので、30 で割って毎日適用する
- 表に出てくる数値（+6 / -5 / +2 / -2 など）はすべて [SerializeField] にして
  Inspector から変えられるようにする。ハードコードしない
- 親の平均バランス・平均ストレスは Household から取る

Assets/Yamaguchi/Scripts/Core/RunawayWatcher.cs
- docs/design/child-status.md 4章のとおり
- 子のストレスが limitStress（既定91）以上の日を数える
- 下回ったらカウントを 0 に戻す
- runawayDays（既定30・仕様未定の仮値であることを Tooltip に明記）に達したら
  GameClock.SetRunning(false) で時間を止める
- event OnRunaway と、カウントが変わったことを知らせる
  OnDangerDaysChanged(int) を公開する
- 家出までの残り日数を返す RemainingDays を公開する
- エンディング画面やゲームオーバー演出は作らない（未実装のため）

【変更するもの】
Assets/Yamaguchi/Scripts/Core/Household.cs
- ChildStatus child フィールドと Child プロパティを追加
- 親だけを返す Parents と、親＋子を返す Members を追加
- AverageParentWorkLifeBalance（親のライフ⇔ワークの平均）を追加
- AverageParentStress（親のストレスの平均）を追加

【注意】
- DayTicker.I / Household.I は Awake で代入されるので、購読は Start で行う。
  既存の ParentStressSystem.cs と同じ書き方に合わせること。
- GameState は年齢の取得にだけ使う。子のステータスを GameState に持たせない。

【触らないもの】
GameState.cs / SaveSystem.cs / Assets/Maruyama/ / Assets/Arisaka/ /
Assets/Kusao/ / シーンファイル

【確認】
- コンパイルが通ること
- GameManager にどのコンポーネントを足せばよいか（ChildStatus を付ける
  GameObject、ChildGrowthSystem、RunawayWatcher）を報告に書くこと。
  シーンへの配置は人間が行う。
```

---

## 段階②-b：レビュー指摘の修正

> 段階②の実装をレビューして見つかった4点を直す。
> いずれも仕様書に書き漏れていたもので、child-status.md に追記済み。
> ③に入る前にここを直すと、③④がそのまま乗る。

```
docs/design/child-status.md をもう一度読んでください（追記があります）。
すでにある実装を修正するタスクです。新しいファイルは作りません。

【修正1】家出を一度きりにする
対象：Assets/Yamaguchi/Scripts/Core/RunawayWatcher.cs
仕様：docs/design/child-status.md 4章「家出は一度きり」
- 家出したらフラグを立て、以降は日が進んでも判定を行わない
- OnRunaway は必ず1回だけ発火させる
- フラグを外から読めるよう HasRunAway のようなプロパティを公開する
- 現状は GameClock を止めているので結果的に1回だが、
  後から一時停止やイベントが入ると二重で発火するため、フラグで明示的に止める

【修正2】値が変わらないときは変更通知を出さない
対象：PersonStatus.cs の AddStress / SetStress、
      ChildStatus.cs の AddGrade / AddHealth
仕様：docs/design/child-status.md 2章「通知は『表示が変わるときだけ』出す」
- 変更前の丸めた整数と変更後の丸めた整数を比べ、違うときだけ Invoke する
- 内部の float はこれまでどおり毎日更新する（累積を落とさない）
- AddHobbyExp と SetStudyPlayBalance は現状のままでよい

【修正3】合格ラインの判定を表示と揃える
対象：ChildStatus.cs の IsAbovePassLine
仕様：docs/design/child-status.md 5章
- 内部の float ではなく、丸めた整数（Grade）で比べる
- 成績 59.6 のときに「60%」と表示しながら「不足」と出る状態をなくす

【修正4】Parents / Members が毎回リストを作らないようにする
対象：Household.cs
仕様：docs/design/child-status.md 1章
- いまはプロパティを呼ぶたびに new List している
- yield return で返すか、内部でリストを使い回す形にする
- 毎日全員をなめる処理（③の ConditionSystem）から呼ばれるため

【確認】
- カードに表示する値がこれまでと変わらないこと（修正2は通知の回数を減らすだけで、
  値そのものは変えない）
- 家出のログが1回しか出ないこと
- コンパイルが通ること
```

---

## 段階③：特性（Trait）と状態（Condition）

```
docs/design/traits.md を全部読んでから作業してください。
docs/decisions/0007-traits-and-conditions.md にも目を通すこと。

【目的】
永続の「特性」と一時の「状態」を持てるようにし、その補正が
ステータスの書き込み窓口で効くようにする。

【作るもの】

Assets/Yamaguchi/Scripts/Core/EffectKind.cs
- docs/design/effects.md 2章の enum EffectKind を、書かれているとおり
  一字一句そのまま写す。新しい値を足したり、名前を変えたりしない。
- Effect クラスと EffectApplier は別の担当が作るので実装しない。

Assets/Yamaguchi/Scripts/Core/StatModifier.cs
- enum ModifierOp { Add, Multiply }
- [Serializable] class StatModifier { EffectKind kind; ModifierOp op; float value; }

Assets/Yamaguchi/Scripts/Core/TraitDefinition.cs
- ScriptableObject。id / 表示名 / 説明 / List<StatModifier>
- [CreateAssetMenu] を付けて、アセットとして増やせるようにする

Assets/Yamaguchi/Scripts/Core/ConditionDefinition.cs
- ScriptableObject。TraitDefinition の項目に加えて
  既定の持続日数 / バッジ色 / 優先度（複数付いたとき表に出す順番）
- [CreateAssetMenu] を付ける

Assets/Yamaguchi/Scripts/Core/TraitSet.cs / ConditionSet.cs
- それぞれ [Serializable] な入れ物クラス。MonoBehaviour にはしない
- TraitSet：Add / Remove / Has / All / 変更通知 OnChanged
- ConditionSet：上記に加えて、残り日数を1日減らして期限切れを外す TickDay、
  優先度が一番高いものを返す Top
- ※ この2つは必ず別クラスにする。1つにまとめて「永続フラグ」で分けないこと。
  理由は 0007 の ADR を参照。

Assets/Yamaguchi/Scripts/Core/ConditionSystem.cs
- DayTicker.I.OnDayPassed を購読し、Household.I.Members 全員の
  ConditionSet.TickDay() を呼ぶ
- TraitSet はなめない（期限が無いため）

【変更するもの】

Assets/Yamaguchi/Scripts/Core/PersonStatus.cs
- TraitSet と ConditionSet を1つずつ持ち、Traits / Conditions で公開する
- AddTrait(TraitDefinition) / AddCondition(ConditionDefinition, int days = -1) /
  RemoveCondition(string id) を用意する
  （days を省略したら定義の既定日数を使う）
- 指定した EffectKind の補正を、Traits と Conditions の両方から集めて返す
  メソッドを1つ用意する。加算の合計と倍率の合計を返す形にする
- AddStress の中で、delta > 0 のときだけ Stress の補正を掛ける
  計算順は 加算 → 倍率：(delta + 加算の合計) * (1 + 倍率の合計)
  ※ 下がるときには掛けない。「上がりにくい」であって「下がりにくい」ではない

Assets/Yamaguchi/Scripts/Core/ChildStatus.cs
- AddGrade / AddHealth / AddHobbyExp の中で、同じように
  Grades / Health / Hobby の補正を掛ける（プラスのときだけ）

Assets/Yamaguchi/Scripts/Core/ParentStatus.cs
- DailyWage の計算の最後に Money の補正を掛ける

【動作確認用に作ってよいもの】
Assets/Yamaguchi/ScriptableObjects/ の下に、確認用のアセットを2つだけ作る
- 特性「勉強が得意」：Grades を Multiply +0.2
- 状態「風邪気味」：既定7日、Health を Add -1、Grades を Multiply -0.5
これらは仮のサンプルであり、正式な内容ではないことを報告に書くこと。

【触らないもの】
GameState.cs / SaveSystem.cs / 他メンバーのフォルダ / シーンファイル
Effect クラスと EffectApplier（別担当）

【確認】
- コンパイルが通ること
- 特性・状態をセーブ対象にしていないこと（保存は未対応でよい）
- 補正が効く場所の一覧（どのメソッドの中で掛けたか）を報告に書くこと
```

---

## 段階③-b：毎日の効果・残り日数の通知・確認用メニュー

> 段階③の動作確認で見つかった点と、④の UI 確認に必要なもの。Core 側の変更なので ④ の前にやる。
> **サンプルアセットの修正は人間が行う**（末尾参照）。

```
docs/design/traits.md の 3章「補正は『値が上がるときだけ』掛ける」と
「状態の『毎日の効果』（dailyEffects）」を読んでから作業してください。

【修正1】状態に「毎日の効果」を持たせる
作るもの：Assets/Yamaguchi/Scripts/Core/DailyEffect.cs
- [Serializable] class DailyEffect { EffectKind kind; float value; }
- 加算のみ。倍率（ModifierOp）は持たせない

変更するもの：
- ConditionDefinition.cs に List<DailyEffect> dailyEffects を追加
  （TraitDefinition には追加しない。毎日の効果は状態だけが持つ）
- PersonStatus.cs に「毎日の効果を1件適用する」メソッドを virtual で用意する
  - PersonStatus 側：Stress → AddStress、Money → GameState.I.AddMoney（整数に丸める）
  - ChildStatus 側で override：Health → AddHealth、Grades → AddGrade、Hobby → AddHobbyExp
  - その人が持っていない値（親の Grades など）は何もしない
  - 必ず書き込み窓口を通すこと。値を直接書き換えない
- ConditionSystem.cs：毎日、家族全員の付いている状態について
  ① dailyEffects を適用 → ② TickDay の順で行う
  （7日の状態なら、毎日の効果はちょうど7回効く）

【修正2】残り日数が減るたびに通知する
対象：ConditionSet.cs の TickDay
- いまは状態が外れたときしか OnChanged が出ない
- 状態が1件でもあれば、日数を減らしたときに OnChanged を出す
  （④でカードに「風邪気味 ｜ あと5日」と出すため）
- 状態が0件の日は通知しない

【修正3】確認用の右クリックメニューを作る
対象：PersonStatus.cs（親にも子にも使えるように基底クラスで）
- Inspector に「テスト用」の欄を作り、TraitDefinition と ConditionDefinition を
  1つずつ入れられるようにする
- [ContextMenu] で次の3つを用意する
  「テスト：特性を付ける」→ AddTrait
  「テスト：状態を付ける」→ AddCondition（日数は定義の既定値）
  「テスト：特性・状態を全部外す」
- 必ず AddTrait / AddCondition を通すこと（通知を出すため。リストを直接触らない）
- 再生中でなければ何もせず、警告ログだけ出す
  （停止中に付けるとシーンに保存されてしまうため）

【確認】
- コンパイルが通ること
- すでに動いている補正（成績の伸び・給料）の挙動を変えないこと
```

### ③-b のあと、人間がやること

`Condition_風邪気味` の **Modifiers から `Health / Add / -1` を消し、Daily Effects に `Health / -1` を入れ直す**。
（Modifiers には `Grades / Multiply / -0.5` だけ残る）

---

## 段階④：UI スクリプト

```
docs/design/child-status.md の 6章、docs/design/traits.md の 6章、
docs/code/ui-scroll-patterns.md を読んでから作業してください。

【目的】
子どもカードと、ストレス段階バッジのスクリプトを作る。
シーンへの配置は人間が手で行うので、このタスクではシーンを編集しない。

【前提】
- カードの大きさは親カードと同じ 200×250
- ステータス欄は既存の StatusListUI / StatusRowUI をそのまま使う。作り直さない
- 表示するだけで、値の計算は一切しない（計算は ChildGrowthSystem の担当）

【作るもの】

Assets/Yamaguchi/Scripts/UI/StatMeterUI.cs
- ラベル＋バー＋数値 の 1本ぶんを表示する部品
- バーは Slider と Image(Filled) のどちらでも動くように、両方の参照を
  [SerializeField] で持ち、入っているほうを使う
- SetValue(float 0〜100) と SetValue01(float 0〜1) を公開
- 数値の書式は [SerializeField] の文字列（既定 "{0}%"）で変えられるようにする

Assets/Yamaguchi/Scripts/UI/StressBadgeUI.cs
- ストレスの段階を色付きバッジで出す。母・父・子どのカードにも使える
- Bind(PersonStatus) で対象を決め、OnStressChanged を購読して更新する
- 段階と表示名・色は docs/design/child-status.md 6章の表のとおり
  （好調＝緑 / 普通＝灰 / 注意＝赤 / 限界＝濃い赤）
- 表示名と色は [SerializeField] で変えられるようにする（表示名は仮のため）
- ※ これは風邪気味などの状態バッジではない。ストレス値を分かりやすくする
  ものなので、混同しないこと

Assets/Yamaguchi/Scripts/UI/PersonTraitRowsUI.cs
- PersonStatus の特性・状態を、ステータス欄（StatusListUI）の行として出す
- 母・父・子のどのカードからも使えるようにする
- 書式は docs/design/traits.md 6章のとおり：
  1件につき1行。左に名前、右に効果（状態は「あと○日」）
  例）勉強が得意 ｜ 成績+20%　／　風邪気味 ｜ あと5日
- 行のキーは trait_<id> / condition_<id> のように1件ごとに分ける
- 付いていないときは行を出さない。外れたら RemoveStatus で行を消す
- 効果の文字列は補正のリストから組み立てる。倍率は "+20%"、加算は "+15" の形。
  補正が複数ある特性は代表の1つを出し、残りは「他+1」のように添える
- 画面での呼び方は「特性」。「メリット」とは書かない
  （デメリットの特性も付けられるため）

Assets/Yamaguchi/Scripts/UI/ChildCardUI.cs
- 既存の ParentCardUI.cs と同じ作りにする
  （Start で Household.I.Child を取得 → 購読 → 表示するだけ）
- 繋ぐもの：
  勉強⇔遊びスライダー（Slider）… 値が変わったら
    ChildStatus.SetStudyPlayBalance を呼ぶ
  成績・ストレス・体調の StatMeterUI 3本
  StressBadgeUI
  プロフィールの1行（TextMeshProUGUI）… ChildStatus.ProfileLine
  StatusListUI … 趣味 / 合格ライン の行を出す
  PersonTraitRowsUI … 特性・状態の行はこちらに任せる
  RunawayWatcher（任意）… 限界が続いている間だけ「家出まで ○日」の行を出し、
    0 に戻ったら RemoveStatus で行ごと消す
- 年齢の表示を更新するため GameState.I.OnAgeChanged も購読する
- OnEnable / OnDisable で購読と解除を行う（ParentCardUI と同じ）

Assets/Yamaguchi/Scripts/UI/PersonStressUI.cs
- Assets/Maruyama/StressUI.cs を複製して Yamaguchi 側に置いたもの
- 違いは、GameState.I ではなく [SerializeField] で指定した PersonStatus の
  ストレスを見ること
- 元の Assets/Maruyama/StressUI.cs は変更も削除もしない

【変更するもの】
Assets/Yamaguchi/Scripts/UI/ParentCardUI.cs
- StressBadgeUI と PersonTraitRowsUI の [SerializeField] を追加し、
  Start で対象の ParentStatus に繋ぐ
- それ以外の既存の動きは変えない

【TextMeshPro の注意】
docs/code/ui-scroll-patterns.md の「ハマりどころ」を必ず読むこと。
- TextOverflowModes.Ellipsis にすると文字が一切描画されなくなる。使わない
- 日本語フォントは Resources.Load<TMP_FontAsset>("JPFont")

【触らないもの】
シーンファイル / プレハブ / 他メンバーのフォルダ / Core 側のロジック

【確認】
- コンパイルが通ること
- 人間がシーンに配置するための手順を報告に書くこと。
  どの GameObject に何のコンポーネントを付け、Inspector の
  どのフィールドに何を入れればよいかを、上から順に書く
```

---

## 今後：イベント機能と繋ぐ段階で必ずやること

> まだ着手しない。Effect / EffectApplier の土台ができて、イベントから `AddTrait` / `AddCondition` を呼ぶようになったら、
> その段階の指示文に **下のブロックをそのまま足す**。
> いまはステータスの変化を受けて状態を付け外しする処理が無いので実害はない。

```
【必須】ConditionSystem のループ中に状態が増減しても壊れないようにする
対象：Assets/Yamaguchi/Scripts/Core/ConditionSystem.cs の TickAll

【背景】
TickAll は member.Conditions.All を foreach で回しながら ApplyDailyEffect を呼んでいる。
ApplyDailyEffect は AddStress などを呼び、その変更通知を受けた処理が
同じ人に AddCondition / RemoveCondition すると、回している最中のリストが変わり、
InvalidOperationException（Collection was modified）で止まる。
止まるとその日の残り日数が減らず、後ろの家族も処理されない。
例：「ストレスが限界になったら寝不足になる」「体調が回復したら風邪を外す」

【直し方】
- 回す前に、その人の状態リストを写しに詰め、写しのほうを回す
- 写しは ConditionSystem のフィールドに1つ持って使い回す（毎日 new List しない）
  Clear してから AddRange で詰め直す
- 写しを回す中で、適用する前に「その状態がまだ付いているか」を
  member.Conditions.Has(id) で確かめ、外れていたら飛ばす
- TickDay はこれまでどおり元のリストに対して呼ぶ（後ろから for で回しているので安全）
- PersonStatus / ConditionSet には手を入れない

【挙動として正しいもの】
- ループ中に付いた状態は、その日は毎日の効果がかからず、翌日からかかる
- ループ中に外れた状態は、その日の残りの効果もかからない

【確認】
- 既存の挙動（風邪気味が7日で外れ、毎日体調が1下がる）が変わらないこと
```

