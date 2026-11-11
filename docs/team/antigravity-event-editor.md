---
title: Antigravity 用プロンプト（イベントの編集ウィンドウとストーリー分岐）
updated: 2026-11-11
status: 運用前
related: [docs/design/event-editor.md, docs/design/events.md, docs/design/effects.md, docs/design/traits.md, docs/code/support-editor.md, docs/code/ui-tutorial.md]
---

# Antigravity 用プロンプト（イベントの編集ウィンドウとストーリー分岐）

> イベントの編集ウィンドウと、選択で展開が変わるストーリーの土台を Antigravity に作ってもらうための依頼文。
> **段階ごとに分けて渡す。** 毎回「共通の前文」を頭に付けてから、その段階の指示を貼る。
> 仕様は [../design/event-editor.md](../design/event-editor.md)。ストーリーの管理ツール（段階⑥）は [../design/story-tool.md](../design/story-tool.md) で、依頼文は別に作る。

## 段階の全体像

| 段階 | 内容 | 丸山さんのフォルダ | 終わると何ができるか |
|---|---|---|---|
| ① | 編集ウィンドウ（今のデータのまま） | 触らない | イベントをウィンドウで作れる。決まった日を年月日で入れられる |
| ② | カードの見た目（画像なし・選択肢1〜4・4つは2×2）、新しい条件の部品、`Enqueue` | 触る | 画像なし・4択のイベントが出せる。「冬だけ」「小1の1学期だけ」が作れる |
| ③ | ストーリーの土台（選択肢ID・選んだ通知・StoryState・フラグ・前の選択の条件） | 触る | 「前に○○を選んだら出る／出ない」が作れる |
| ④ | 効果の種類を5つ追加・続きのイベント・条件を満たしたら1回 | 触る | 野球教室 → 試合 → 大会 のつながりが作れる |
| ④-b | レビュー指摘の修正（下記） | 触る | 続き・条件イベントが実際に出る。子どもへの効果が本物に届く |
| ⑤ | チュートリアル側・職業の条件・選択肢の出す条件・趣味の呼び名・オートの選択肢固定 | 触る | ストーリーの文章が分岐する。画面に「野球」と出る |

## 始める前にやること（人間）

- [ ] `docs/design/event-editor.md` と `docs/design/story-tool.md` をリポジトリに置く（Claude のプロジェクトの game-design/ から写す）。
- [ ] ②の前に、丸山さんに「イベントのフォルダ（Assets/Maruyama/）を山口が変更する」と一声かける。
- [ ] ④の前に、有坂さん・草尾さん・丸山さんに「EffectKind に5つ足す」と一声かける（共有の名前のため）。
- [ ] AGENTS.md の「他のメンバーの担当領域に踏み込まない」に、今回の例外（イベントのフォルダは山口が変更してよいファイル）を1行足す。足さないと Antigravity が作業を止める。

---

## 共通の前文（毎回、段階の指示の前に付ける）

```
あなたは Unity 製の子育てシミュレーションゲームの実装を担当します。

【必ず最初に読むもの】
1. リポジトリ直下の AGENTS.md（エージェント向けルールとドキュメント索引）
2. docs/code/architecture.md（GameState の API、時間の流れ）
3. docs/design/event-editor.md（今回の仕様。全部読むこと）
4. 今回の段階の指示で指定したファイル

【守ること】
- Unity の MCP ツールは使わない。Unity での確認は人間が行うので、
  確認手順を報告に書く。
- 仕様を推測しない。ドキュメントに書かれていないことは「未定」として
  作業を止め、何が分からないかを報告する。勝手に決めない。
- docs/design/ の数値・寸法はすべて調整前のたたき台。
  数値は [SerializeField] にして Inspector から変えられる形にする。
- 機能ごとにクラス・ファイルを分ける。1つのファイルに長々と書かない。
  ファイルの分け方は event-editor.md 6章の表に従う。
- 作るファイルは Assets/Yamaguchi/Scripts/ の下に置く。
- Assets/Maruyama/ は、その段階の指示で「変更してよい」と書いたファイルだけ変更してよい。
  それ以外の Maruyama のファイル、Assets/Arisaka/、Assets/Kusao/ は変更しない。
- enum に値を足すときは、必ず今ある値の一番後ろに足す。途中に入れたり
  並べ替えたりしない（アセットに番号で保存されているため、既存のイベントの
  効果や出方がずれる）。
- シリアライズしているフィールドの名前は変えない（保存済みの値が外れるため）。
- シーンファイル（.unity）とプレハブは編集しない。配置・レイアウトの変更は
  人間が行うので、必要な参照は [SerializeField] で公開し、日本語の [Tooltip] を付け、
  人間がやる手順を報告に書く。
- UI の文字は TextMeshPro（JPFont）。TextOverflowModes.Ellipsis は使わない
  （docs/code/ui-scroll-patterns.md の「ハマりどころ」）。
- エディタ拡張は既存の支援制度の編集ウィンドウ（Assets/Yamaguchi/Scripts/Editor/Support/）
  とチュートリアルの編集ウィンドウ（Editor/TutorialEditorWindow.cs）の書き方に合わせる。
  年齢・円・月のトグルなどの入力部品は SupportEditorGUI を使い回し、同じものを作り直さない。
- エディタ拡張で値を書くときは SerializedObject / SerializedProperty を通す
  （Undo と保存のため）。アセットのフィールドを直接書き換えない。
- コメントは日本語で書く。既存コードの文体に合わせる。

【終わったら報告すること】
- 追加・変更したファイルの一覧と、それぞれ何をするものか1行ずつ
- Assets/Maruyama/ で変更した箇所の一覧（丸山さんに伝えるため）
- コンパイルが通ること
- 人間が Unity で確認する手順（上から順に）
- 仕様が足りずに判断できなかった点があれば、その一覧
```

---

## 段階①：編集ウィンドウ（今のデータのまま）

```
docs/design/event-editor.md の 0章・3章・5章（5.1〜5.9）・6章を読んでから作業してください。
参考：docs/code/support-editor.md（同じ作りのウィンドウ）、
Assets/Maruyama/Scripts/Events/ の EventData.cs / EventChoice.cs / EventConditionSO.cs /
AgeRangeCondition.cs / ChildHealthCondition.cs / StressLevelCondition.cs / EventManager.cs、
Assets/Maruyama/Scripts/Core/Effect.cs。

【目的】
メリット／デメリットイベント（EventData）を、Inspector ではなく専用のウィンドウで
作れるようにする。この段階では EventData などランタイムのスクリプトは一切変えない。
エディタ用のスクリプトだけを足す。

【変更してよい Assets/Maruyama/ のファイル】
なし。

【作るもの】Assets/Yamaguchi/Scripts/Editor/Event/ の下

EventEditorWindow.cs
- メニュー「子育て/イベント/編集ウィンドウ」
- 上から：説明 → イベントの選択（ObjectField）と［新しく作る ▼］［複製して作る］
  → 選んだイベントの各欄 → すべてのイベント（一覧／年表）
- 各欄は下のクラスを呼ぶだけにする。OnGUI に中身を書かない

EventDataInspector.cs
- EventData の Inspector の上に［編集ウィンドウで開く］ボタンと、まとめの文
  （EventDescriber）を出し、その下は DrawDefaultInspector

EventBasicSection.cs（5章「基本」）
- ID / メリット・デメリット / タイトル / 説明（［{name}］ボタンで説明欄の最後に {name} を足す。
  置き換えの処理は⑤なので、ここでは文字を足すだけ）
- 画像：Sprite の欄と小さなプレビュー（SupportEditorGUI.DrawThumbnail）。空なら「画像なし」と出す

EventTimingSection.cs（5.2）
- 出方：今ある EventTrigger（Random / Fixed）をラジオボタンで「抽選」「決まった日」と表示
- 抽選：重み（weight）、クールダウン（cooldownDays。横に［半年］［1年］ボタンで 182 / 365 を入れる）
- 決まった日：
  「◉ 1回だけ □歳 [月▼][日▼]」→ fixedMinAgeYears = fixedMaxAgeYears = 歳、fixedMonth、fixedDay
  「○ 毎年 [月▼][日▼]（□歳〜□歳）」→ fixedMin / fixedMax を別々に
  どちらの形かは「fixedMinAgeYears == fixedMaxAgeYears か」で判定して表示する（データには持たない）
  日はその月の日数まで選べるようにする（GameDate.DaysInMonth）
- 下に文章で「6歳4月1日に1回」「毎年12月25日（0歳〜5歳）」と出す

EventConditionList.cs（5.3）
- conditions のリストを1行ずつ表示。各条件はその場で中身を編集できるようにする
  （Editor.CreateCachedEditor で埋め込む）。左に種類の名前、右に［✕］
- ［＋条件を足す ▼］のメニュー：
  「新しく作る/…」… TypeCache.GetTypesDerivedFrom<EventConditionSO>() で、
    abstract でない型を全部並べる（後で部品が増えたら自動で出る）。
    作った条件は、イベントのアセットの中にサブアセットとして保存する
    （AssetDatabase.AddObjectToAsset → SaveAssets）。名前は「Cond_<種類>」
  「既存のアセットから選ぶ」… プロジェクト内の EventConditionSO アセットを選ぶ
- ［✕］で外すとき、それがこのイベントのサブアセットなら、サブアセットごと消す
  （別アセットの条件は参照を外すだけで、アセットは消さない）
- 共有の条件（別アセット）を他のイベントも使っていたら
  「ほかでも使っています：○○、○○。ここで変えるとそちらも変わります」と出し、
  ［このイベント専用にコピー］（コピーをサブアセットにして差し替える）を付ける
- 各条件の説明文：今ある部品には Describe が無いので、EventDescriber に
  型ごとの説明文を作る処理を持たせる（AgeRange「3〜5歳」、ChildHealth「体調 0〜75」、
  StressLevel「子のストレスが危険〜限界」）。知らない型は型名を出す

EventChoiceSection.cs（5.4。この段階では「ストーリー」「出す条件」の欄は作らない）
- 選択肢を折りたたみで並べる。各選択肢：ラベル／効果のリスト／家計簿の種類／保険診療の自己負担
- 効果1行＝「種類・対象・量・長さ（・日数）」。名前は SupportEditorGUI の
  KindNames / TargetNames / DurationNames を使う。お金のときは量を円で表示
- 効果のリストの下に、カードに出るチップの見た目を文字で出す（例：体調 +15 ／ 子ストレス −5）。
  お金はチップではなくボタンの文字に付く（EventChoiceButtonUI と同じ規則）ことも表示する
- ［＋選択肢］、各選択肢に［↑］［↓］［✕］（SupportEditorGUI.ArrayButtons）
- この段階では選択肢は何個でも足せてよい。5個以上はチェック（EventValidator）で ✕ にする

EventDescriber.cs
- まとめの文：「メリット・抽選。3〜5歳。重み 1・クールダウン 1年。選択肢2つ：病院に行く（−8,000円・体調+15）／…」
- 条件の説明文（上記）

EventValidator.cs（5.7 のうち、この段階で判定できるもの）
- ✕：ID が空・重複・英数字と _ 以外／タイトルが空／選択肢0個・5個以上／ラベルが空／
     決まった日の月日がその月に無い日（2月30日など）／条件のリストに空（None）の行
- ⚠：抽選なのに重み 0／効果0個の選択肢／4つの選択肢のラベルが長い
     （文字数は [SerializeField] ではなく定数でよい。たたき台 10文字）／
     Resources/Events の外にある（EventManager が読み込まない）／画像なし
- ✕ と ⚠ のリストを返すだけ。表示はウィンドウ側

EventRegistry.cs
- プロジェクト内の EventData をすべて探す（AssetDatabase.FindAssets("t:EventData")）
- ID の自動生成（ファイル名 Event_Merit_Baseball → merit_baseball）と重複確認
- Resources/Events の中にあるかの判定と、［移動する］（AssetDatabase.MoveAsset）
- 条件アセットを使っているイベントの逆引き
- 結果はアセットが変わったときだけ作り直す（毎フレーム探さない）

EventFactory.cs（5.1）
- 保存先 Assets/Maruyama/Resources/Events/。ファイル名を決めるダイアログ → ID を自動で付ける
- テンプレ：空／抽選のメリット／抽選のデメリット／決まった日（1回・選択肢1つ「わかった」）
  （「続き」「条件を満たしたら」「分岐の入口」は出方が無いので④で足す）
- 複製して作る：今のイベントをコピーし、ID を新しいファイル名から付け直す。
  サブアセットの条件もコピーして、コピー先の中に入れ直す（コピー元を指したままにしない）

EventListSection.cs（5.8）
- タブ：メリット／デメリット／決まった日。行＝ファイル名・ID・年齢・選択肢の数・画像の有無・✕と⚠の数
- 年齢順に並べる（決まった日はその年齢、抽選は AgeRangeCondition の最小。無ければ 0）
- 行を押すと選択

EventTimelineView.cs（5.8「年表」）
- 横軸 0〜22歳。決まった日は点（1回）または点の並び（毎年）、
  抽選は AgeRangeCondition の範囲を横棒で描く（条件が無ければ全体）。メリット緑・デメリット赤
- 押すとそのイベントを選択。IMGUI で描く（GraphView は使わない）

EventPlayModeSection.cs（5.9 のうち、この段階でできるもの）
- 再生中だけ表示
- 条件の判定：各条件の IsSatisfied の ✓✗ と説明文、クールダウンの残りは出さなくてよい
- ［今すぐ出す（条件を無視）］：シーンの EventDialogUI を探し、
  カテゴリに合わせて Show(このイベント, null, null) か Show(null, このイベント, null) を呼ぶ。
  EventDialogUI が開いているときは押せないようにする。
  ※ EventManager の順番待ちを通らない仮の方法。②で EventManager.Enqueue に差し替える
- 注意書き：「再生中に変えた値はアセットにそのまま残ります」

【カードのプレビュー（5.6）について】
EventCardUI は Instantiate / Destroy を使うので、停止中には呼ばない。
この段階ではプレビューは作らず、再生中の［今すぐ出す］で確認する。

【触らないもの】
Assets/Maruyama/ のすべて、ランタイムのスクリプト、シーン、プレハブ

【確認】
- コンパイルが通ること
- 既存の16個のイベントを開いて、値が変わらずに表示されること（開いただけで保存が走らないこと）
- 決まった日のイベント（Event_Fixed_Birthday など）の「1回だけ／毎年」の表示が正しいこと
- 人間が確認する手順：新しく作る → 条件を足す（サブアセットになっていること）→
  選択肢を3つにする → 再生して年齢ジャンプ → ［今すぐ出す］
```

---

## 段階②：カードの見た目・新しい条件の部品・Enqueue

```
docs/design/event-editor.md の 2章・3章（3.1 の Enqueue と 3.2）を読んでから作業してください。
既存の実装を変える段階です。

【変更してよい Assets/Maruyama/ のファイル】
- Scripts/Events/EventData.cs … imageNote（絵の指示メモ）を足すだけ
- Scripts/Events/EventManager.cs … Enqueue を足すだけ
- Scripts/UI/EventCardUI.cs … 画像なし・選択肢の数に合わせた並べ方
- Scripts/UI/ChoiceBlockUI.cs … 高さの切り替え

【1】画像なしで文章が広がる（2.1）
- EventData に [TextArea] string imageNote を足す（絵を描く人向けのメモ）
- EventCardUI：画像が無いときは画像の GameObject を SetActive(false) するだけにする。
  位置の計算はしない（詰めるのはレイアウトグループの仕事。プレハブの変更は人間が行う）
- 画像が無く imageNote が入っているとき、差し替え枠（灰色）＋メモを出すか出さないかを
  [SerializeField] bool showPlaceholderInGame（既定 false）で選べるようにする
  （開発中だけ枠を出して、どの絵が足りないか見えるようにするため）

【2】選択肢 1〜4、4つは 2×2（2.2）
- EventCardUI に [SerializeField] で2つの入れ物を持たせる：
  縦1列用（VerticalLayoutGroup）と 2×2用（GridLayoutGroup・2列）
- 選択肢が4つのときは 2×2 用、1〜3つのときは縦1列用にだけ ChoiceBlock を作り、
  使わないほうは SetActive(false)
- 5つ以上のときは先頭4つだけ出し、LogWarning を1回出す
- 既存の choicesContainer は「縦1列用」として使い続ける（フィールド名は変えない）
- ChoiceBlockUI：[SerializeField] で数ごとの高さ（1〜2：90、3：72、4：たたき台 90）を持ち、
  LayoutElement.preferredHeight に入れる

【3】Enqueue（3.1）
- EventManager に public void Enqueue(EventData e) を足す：カテゴリに合わせて
  DialogRequest の片側に入れて pendingDialogs に積み、TryShowNext を呼ぶ
- 条件の判定はしない（呼ぶ側が判定する）
- 段階①の EventPlayModeSection の［今すぐ出す］をこれに差し替える

【4】新しい条件の部品（3.2）
作る場所：Assets/Yamaguchi/Scripts/Story/Conditions/
すべて Maruyama.Events.EventConditionSO を継承する。[CreateAssetMenu] は付けない
（編集ウィンドウからサブアセットとして作るため）。各部品に public string Describe() を持たせ、
段階①の EventDescriber は Describe を持つ部品ならそれを使うように変える
（Describe 用の小さな interface IDescribable を Story/ に作る）。

- DateRangeCondition：GameDay from / to（両端を含む）。「6歳4月1日〜6歳7月31日」
- MonthCondition：12か月のトグル（List<int> months）。「冬（12・1・2月）」のように、
  12・1・2 / 3・4・5 / 6・7・8 / 9・10・11 がそろっていれば季節名で出す
- SchoolGradeCondition：学年の範囲。学年は「その年度の4月1日時点の満年齢」から求める
  （0〜2歳：未就園、3歳：年少 … 6歳：小1 … 15歳：高1 … 17歳：高3）。
  docs/design/ending.md 1章「時期は年齢ではなく学年と月で決める」と矛盾がないか確かめ、
  食い違うときは作業を止めて報告する
- ChildStatCondition：値（成績／体調／趣味Lv）が ○以上・○以下、または趣味の種類が ○
- MoneyCondition：所持金（GameState.I.Money）が ○円以上／以下
- SupportCondition：SupportScheme を1つ。受給中／受けていない（SupportManager.I.GetState）
- TraitCondition：対象（母／父／子／家族のだれか）が TraitDefinition か ConditionDefinition を持っている／いない
- RequirementCondition：RequirementSO を1つ持ち、その判定をそのまま使う（支援制度の条件の流用）
- GameState / Household / SupportManager が無い環境（テストシーン）では true を返す
  （既存の AgeRangeCondition と同じ）

編集ウィンドウ側：
- EventConditionList のメニューに新しい部品が自動で出ることを確認する（型を並べているので変更は不要のはず）
- DateRangeCondition の GameDay は SupportEditorGUI.DrawGameDay、
  MonthCondition は SupportEditorGUI.DrawMonthToggles を使う
  （埋め込みの Editor では足りないので、この2つは CustomEditor を作る）

【人間がやること（報告に手順を書く）】
- EventCard.prefab：中身を VerticalLayoutGroup に入れて画像が消えたら詰まるようにし、
  説明文に LayoutElement（flexibleHeight）を付ける。2×2 用の入れ物（GridLayoutGroup）を足して
  EventCardUI に繋ぐ。カード・ダイアログを大きくする（寸法は Unity で決める）
- どのオブジェクトのどの欄に何を入れるかを、上から順に書くこと

【確認】
- コンパイルが通ること
- 既存のイベント（選択肢1〜2つ）の見た目と動きが変わらないこと（プレハブ変更前）
- 新しい条件の部品の説明文の例を報告に書くこと
```

---

## 段階③：ストーリーの土台

```
docs/design/event-editor.md の 4.1・3.2（前の選択・フラグ）を読んでから作業してください。

【変更してよい Assets/Maruyama/ のファイル】
- Scripts/Events/EventChoice.cs … id を足すだけ
- Scripts/Events/EventManager.cs / Scripts/UI/EventCardUI.cs ほか … 選んだ通知
  （docs/team/request-maruyama-event-notify.md の範囲）

【1】選択肢の ID
- EventChoice に string id を足す（空でもよい。空のときは "c" + 番号 を使う関数を用意し、
  警告を出す）
- 編集ウィンドウ：選択肢の欄に ID を出す。新しく足した選択肢には自動で付ける
  （c1, c2 … 空いている番号）。チェックに「選択肢の ID が空・重複」を ✕ で足す
- メニュー「子育て/イベント/選択肢に ID を付ける」：ID が空の選択肢に番号で付ける
  （既存の16個のため。1回だけ人間が実行する）

【2】選んだ通知（docs/team/request-maruyama-event-notify.md を全部読むこと）
- 通知は request-maruyama-event-notify.md の EventManager.OnEventResolved（EventResolvedInfo）を使う。
  別の通知を新しく作らない（振り返りの LifeRecorder と同じ通知を共有するため）
- まだ実装されていなければ、request-maruyama-event-notify.md 4章・6章のとおりに実装する
- EventResolvedInfo に string choiceId を足す（【1】の id。空なら番号から作ったもの）
- デバッグツールのオートもボタンを押すので、同じ通知が出ることを確認する

【3】記録とフラグ（Assets/Yamaguchi/Scripts/Story/）
- StoryFlag.cs：ScriptableObject。id / 表示名 / 説明。
  趣味の呼び名（bool 使う・string 呼び名・HobbyKind 分類）は⑤で使うので、欄だけ今作る。
  [CreateAssetMenu]（子育て/ストーリー/フラグ）
- StoryState.cs：MonoBehaviour・シングルトン（StoryState.I）。
  記録：起きたイベント（id → 回数・最後の日）、選んだ選択肢（イベントid → 選択肢id・日）、
       フラグ（id の集合）、予約中の続き（④で使う。入れ物だけ今作る）
  窓口：HasOccurred(id) / TimesOccurred(id) / ChoiceOf(eventId) / HasFlag(StoryFlag) /
       SetFlag(StoryFlag) / ClearFlag(StoryFlag)
  変更通知：OnFlagChanged(StoryFlag, bool) / OnChoiceRecorded(EventResolvedInfo)
  EventManager.OnEventResolved を購読して記録する（起きた回数・選んだ選択肢を同時に記録。OnDisable で解除）
- StorySaveParticipant.cs：ISaveParticipant。セーブキー "story"。
  既存の FamilySaveParticipant / EventManager の保存の書き方に合わせる
- フラグはアセットの参照ではなく id で保存する

【4】条件の部品（Story/Conditions/）
- ChoiceHistoryCondition：EventData と 選択肢id（選んだ／選ばなかった）、または
  「起きた／起きていない」。選択肢は編集ウィンドウでドロップダウン（そのイベントのラベル）にする
  （CustomEditor を作る）。「選ばなかった」は「起きていて、別の選択肢を選んだ」とする
  （起きていないときは false。理由を Describe で分かるように書く）
- StoryFlagCondition：StoryFlag と 立っている／立っていない

【5】抽選の「一生に1回だけ」
- 編集ウィンドウの抽選の欄に ☐ 一生に1回だけ を足す。
  ON にすると「このイベントが起きていない」の ChoiceHistoryCondition（自分自身を指す）を
  サブアセットで条件に足す。OFF で外す。データの欄は足さない
  （チェックの状態は、その条件が付いているかで判定する）

【確認】
- コンパイルが通ること
- 再生してイベントを選ぶと StoryState に記録されること（Inspector かログで）
- セーブ → ロードで記録が戻ること
- 古いセーブ（"story" が無い）をロードしてもエラーにならないこと
```

---

## 段階④：効果の種類・続きのイベント・条件を満たしたら1回

```
docs/design/event-editor.md の 3.1・4.2・4.3 と docs/design/traits.md 4章を読んでから作業してください。

【変更してよい Assets/Maruyama/ のファイル】
- Scripts/Events/EventData.cs … EventTrigger に値を足す
- Scripts/Core/Effect.cs … 参照の欄を足す
- Scripts/Core/EffectApplier.cs … 新しい種類の処理
- Scripts/Events/EventManager.cs … 新しい出方を抽選に入れない
- Scripts/UI/ChoiceBlockUI.cs … チップを出す種類の選別

【1】EffectKind に5つ足す（Assets/Yamaguchi/Scripts/Core/EffectKind.cs）
- AddCondition の後ろに、この順で足す：SetFlag, ClearFlag, SetHobby, ScheduleEvent, ShowStory
  （途中に入れない・並べ替えない）
- docs/design/effects.md 2章の enum も同じように更新する
- 効果の種類の表示名の一覧を、支援制度用（従来の9種類）とイベント用（14種類）に分ける
  （例：SupportEditorGUI.SupportKindNames / EventKindNames）。
  支援制度の編集ウィンドウでは新しい5種類を選べなくする（2026-11-11 決定。制度には意味がないため）。
  - 選択肢の番号と EffectKind の値を一致させる（9種類の一覧は enum の先頭9つと同じ並び）
  - 支援制度のアセットに新しい5種類が入っていた場合は、値を消さず種類の名前を表示したまま
    赤字で「この種類は支援制度では使えません（効果は起きません）」と出し、
    SupportSchemeValidator に ⚠ として出す。範囲外の番号で空欄や別の種類を表示しない
  - ランタイム（SupportManager / EffectApplier）で制度の効果を弾く処理は入れない

【2】Effect に参照の欄
- Effect に UnityEngine.Object asset を足す（[Tooltip]：フラグ・特性・状態・イベント・
  チュートリアルを入れる。種類に合わないものは無視）
- SetHobby の趣味の種類は value を (HobbyKind) にして使う。ScheduleEvent の日数は days を使う

【3】EffectApplier
- SetFlag / ClearFlag → StoryState.I.SetFlag / ClearFlag
- SetHobby → Household.I.Child.SetHobby
- AddTrait / AddCondition → 対象の PersonStatus.AddTrait / AddCondition
  （いまはログだけなら繋ぐ。対象は今の EffectTarget の振り分けを使う）
- ScheduleEvent → StoryState.I.Schedule(EventData, 今日 + days)
- ShowStory → TutorialManager.I.ShowOnce(TutorialData)
- 参照が空・型が違うときは LogWarning を出して飛ばす
- 下の【必須】ブロック（ConditionSystem のループ中の変更）を必ず入れる

【4】出方を足す（EventData.EventTrigger の後ろに足す）
- FollowUp（続き）/ OnCondition（条件を満たしたら）/ Manual（コードから）
- EventManager.AddToPool：Random 以外かつ Fixed 以外は、どの候補にも入れない
- EventData.IsFixed など既存のプロパティの意味は変えない

【5】StoryEventScheduler（Assets/Yamaguchi/Scripts/Story/）
- DayTicker.I.OnDayPassed を購読
- 予約日が今日の「続き」：条件（CheckConditions）を満たしていれば EventManager.Instance.Enqueue、
  満たしていなければログを出して捨てる
- OnCondition のイベント：まだ起きていなくて条件を満たしたら Enqueue（1回だけ）
- OnCondition のイベントの一覧は Resources/Events から集める（EventManager と同じパス。
  [SerializeField] で変えられるように）
- 予約は StoryState に持ち、セーブされること（③の入れ物）

【6】チップ（ChoiceBlockUI）
- SetFlag / ClearFlag / ScheduleEvent / ShowStory はチップを出さない
- SetHobby は「趣味：スポーツ」、AddTrait は「特性：○○」のチップ
  （EffectChipUI の文字の作り方に合わせる）

【7】編集ウィンドウ
- 出方に「続き」「条件を満たしたら」「コードから」を足す（5.2）。
  続き：このイベントを予約している選択肢の一覧（逆引き）、0件なら ⚠
  条件を満たしたら：説明だけ（一生に1回）
- 選択肢の「ストーリー」欄（5.4）：フラグを立てる・消す／趣味／特性／続き（イベント・何日後・
  ［＋新しく作る］で「続き」テンプレのイベントを作って繋ぐ）／ストーリーを出す。
  中身は effects のリストに行を足す・消すだけ（別のデータは持たない）。
  普通の効果のリストには、ストーリーの種類の行を出さない
- EventLinkSection.cs（5.5）：来る道・行く道・条件にしているもの・使っているフラグ。押すと移動
- EventFactory にテンプレ「続き」「条件を満たしたら」「分岐の入口（2択）」を足す
- チェックに足す：⚠ 続きなのにどこからも予約されていない／
  ✕ 効果・条件が参照しているイベント・選択肢・フラグが消えている／
  ⚠ フラグを見ている条件があるのに、立てる場所がどこにも無い
- 再生中の欄に、立っているフラグ・選んだ履歴・予約中の続きを表示（見るだけ。変更は⑥のストーリーウィンドウ）

【必須】ConditionSystem のループ中に状態が増減しても壊れないようにする
（docs/team/antigravity-child-status.md の末尾「今後：イベント機能と繋ぐ段階で必ずやること」の
ブロックをそのまま実施すること）

【確認】
- コンパイルが通ること
- 既存のイベントの効果（お金・ストレス・体調など）がこれまでどおり効くこと
  （enum の番号がずれていないこと）
- 人間が確認する手順：event-editor.md 4.3 の「野球教室の体験会」と「初めての試合」を
  ウィンドウで作り、年齢ジャンプで「通わせる」→ 180日後に「初めての試合」が出るまで
```

---

## 段階④-b：レビュー指摘の修正（2026-11-12）

> 段階①〜④（⑤の一部も実装済み）をレビューして見つかった点を直す。新しい機能は足さない。

```
docs/design/event-editor.md の 4章 と、このファイルの段階③・④を読んでから作業してください。
既存の実装を直すタスクです。

【変更してよい Assets/Maruyama/ のファイル】
- Scripts/Core/EffectApplier.cs … 子どもへの効果の送り先と、成績・趣味の効果
- Scripts/Events/EventChoice.cs … 警告ログの回数

【修正1】StoryState と StoryEventScheduler を本編シーンに置く形にする（最重要）
いまの問題：
- StoryEventScheduler.I をどこからも呼んでいないので、StoryEventScheduler が一度も作られず、
  「続き」「条件を満たしたら」のイベントが出ない
- StoryState は誰かが StoryState.I に触れたときに自動で作られる。ロード（GameStartup が
  1フレーム後に SaveSystem.Load）までに作られていないと、StorySaveParticipant が登録されておらず
  "story" が復元されない
- どちらも DontDestroyOnLoad なので、タイトルに戻って「最初から」を選ぶと前回のフラグ・履歴・
  予約が残る
直し方：
- StoryState / StorySaveParticipant / StoryEventScheduler は、EventManager と同じく
  本編シーンの GameManager に付けるコンポーネントにする（配置は人間が行う）
- 3つとも DontDestroyOnLoad をやめる。I の中の「見つからなければ new GameObject で作る」もやめ、
  見つからなければ null を返す（呼ぶ側はすでに null を確認している）
- StoryState は Awake で instance を設定する（Start より前に他から呼ばれるため）
- StoryEventScheduler が無いときは、StoryState の Start で LogWarning を1回出す
  （置き忘れに気づけるように）

【修正2】予約日が過ぎた「続き」が消えないようにする
- StoryState.ConsumeScheduledForDay は「予約日 == 今日」だけを取り出している。
  0日後の予約（今日はもう終わっている）や、日が飛んだときに永遠に出なくなる
- 「予約日 <= 今日」のものを全部取り出す形にする
- 編集ウィンドウの普通の効果の行（EventChoiceSection の days 入力）でも、
  種類が「続きを予約」のときは最小 1 日にする（ストーリー欄はすでに 1 以上）

【修正3】「条件を満たしたら」のイベントが二重に出ないようにする
- StoryEventScheduler.CheckOnConditionEvents は HasOccurred で判定しているが、
  HasOccurred が付くのは選択肢を押したとき。Enqueue してから押されるまでの間に日が進むと
  （デバッグツールの年齢ジャンプは1フレームに15日進める）、毎日 Enqueue される
- StoryEventScheduler に「順番待ちに入れたが、まだ選ばれていないイベントid」の集合を持ち、
  入っているものは Enqueue しない。EventManager.OnEventResolved で該当idを外す
  （セーブはしなくてよい。ロード後にまだ条件を満たしていれば、もう一度出るのが正しい）
- 続き（FollowUp）は予約を取り出した時点で消えるので、この対策は要らない

【修正4】イベントの子どもへの効果を、本物の ChildStatus に届ける（前からある問題）
- EffectApplier.GetChildStatus は MaruyamaChildStatus（テスト用の仮の子ども）を返している。
  そのため、イベントの「子ストレス」「体調」は子どもカードの値に反映されていない
- Household.I?.Child（ChildStatus）があればそちらに送る。無いとき（テストシーン）だけ
  MaruyamaChildStatus に送る
- 成績（Grades）と趣味（Hobby）の効果は、いまはログを出すだけで何も起きない。
  対象が Child / Everyone のとき ChildStatus.AddGrade / AddHobbyExp を呼ぶ
  （補正は書き込み窓口の中で掛かるので、ここでは掛けない。traits.md 3章）
- 親の成績・趣味は無いので何もしない（今と同じ）
- IChildStatus の形が ChildStatus と合わなければ、無理に interface に合わせず、
  EffectApplier の中で「ChildStatus があればそちら、無ければ MaruyamaChildStatus」と分ける

【修正5】選択肢 ID が空のときの警告を1回だけにする
- EventChoice.GetOrFallbackId は呼ばれるたびに LogWarning を出す。オートや表示のたびに
  同じ警告が大量に出るので、同じ選択肢（イベントid＋番号）については1回だけ出す
- ※ 既存の14個のイベントは選択肢 ID が空のまま。人間が「子育て/イベント/選択肢に ID を付ける」を
  1回実行する（報告にその手順を書く）

【触らないもの】
プレハブ・シーン（配置は人間）、他メンバーのフォルダ

【確認】
- コンパイルが通ること
- 人間がやる手順を報告に書くこと：GameManager に StoryState / StorySaveParticipant /
  StoryEventScheduler を付ける → 選択肢に ID を付けるメニュー → 「続き」を0日後と180日後で
  予約して年齢ジャンプ → 両方出ること
- イベントで「子ストレス +5」を選ぶと、子どもカードのストレスが上がること
- タイトルに戻って「最初から」で、フラグが残っていないこと
```

---

## 段階⑤：チュートリアル側・職業の条件・仕上げ

```
docs/design/event-editor.md の 2.3・4.3（プレイヤーにはどう見えるか）・4.4・5.9 と
docs/code/ui-tutorial.md を読んでから作業してください。

【変更してよい Assets/Maruyama/ のファイル】
- Scripts/Events/EventChoice.cs … showConditions を足す
- Scripts/UI/EventChoiceButtonUI.cs / ChoiceBlockUI.cs / EventCardUI.cs … 灰色表示と {name}

【1】選択肢の出す条件（2.3）
- EventChoice に List<EventConditionSO> showConditions を足す（空ならいつも押せる）
- 満たさない選択肢は消さずに押せなくし、理由（条件の Describe）を小さく出す
- 全部押せないカードになったら、最初の選択肢を押せるようにして LogWarning
  （ゲームが止まらないようにする）
- 編集ウィンドウ：選択肢に「出す条件（任意）」欄（EventConditionList を使い回す）。
  チェックに ⚠「全部の選択肢に出す条件が付いている」

【2】{name}
- 子どもの名前を1か所から取る窓口を作る（Household.I.Child.DisplayName）
- イベントのタイトル・説明・選択肢のラベル、チュートリアルの見出し・本文の {name} を置き換える

【3】チュートリアル（Assets/Yamaguchi/Scripts/Core/Tutorial/）
- TutorialTimingKind の後ろに「フラグが立った日（ずらせる）」を足す。StoryFlag と日数
- TutorialPage に List<EventConditionSO> showConditions を足す。満たさないページは飛ばす
  （全部飛ぶときは出さない）
- チュートリアルの編集ウィンドウに両方の欄を足す（条件は EventConditionList を使い回す）

【4】職業・支援制度からフラグを見る
- Assets/Yamaguchi/Scripts/Core/Requirements/StoryFlagRequirementSO.cs（RequirementSO を継承）
  フラグが立っている／立っていない。Describe も

【5】趣味の呼び名（4.3「プレイヤーにはどう見えるか」）
- 呼び名を決める窓口を1か所に作る（Story/HobbyDisplayName.cs）：
  立っているフラグのうち、呼び名を使う・分類が子の趣味と同じものの中で、後から立ったものの呼び名。
  無ければ分類の名前
- ChildStatus.ProfileLine・子どもカードの趣味の行・効果チップの趣味は、この窓口を通す
- フラグが立った／消えたら表示を更新する（StoryState.OnFlagChanged を購読）

【6】オートの選択肢固定（5.9）
- DebugAutoPilot に「イベントid → 選択肢id」の固定表を持たせ、固定があればその選択肢を押す
- デバッグツールのウィンドウに表を編集する欄を足す

【確認】
- コンパイルが通ること
- 野球のフラグが立つと、子どもカードに「野球が好き」「趣味　野球 Lv○」と出ること。消すと「スポーツ」に戻ること
- チュートリアルのページが、フラグの有無で出し分けられること
```

---

## 段階⑥：ストーリーウィンドウ

仕様は [../design/story-tool.md](../design/story-tool.md)。①〜⑤が終わってから依頼文を作る。
