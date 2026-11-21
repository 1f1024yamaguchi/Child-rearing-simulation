---
title: Antigravity 用プロンプト（誕生イベント＝子どもの名前を決める）
updated: 2026-11-21
status: 運用前
related: [docs/team/antigravity-event-editor.md, docs/team/antigravity-title-screen.md, docs/design/child-status.md, docs/design/tutorial.md, docs/decisions/0004-pause-during-decisions.md]
---

# Antigravity 用プロンプト（誕生イベント＝子どもの名前を決める）

> 「最初から」を押した直後に「子どもが生まれた！！！名前を決めよう」の画面を出し、プレイヤーが決めた名前をゲーム全体に反映する。
> **段階ごとに分けて渡す。** 毎回「共通の前文」を頭に付けてから、その段階の指示を貼る。誰が Antigravity に渡してもよい。
> **Unity MCP が使える環境なら、シーンの配置・接続まで MCP で行ってよい。** 使えない環境では人間が手で行う。どちらの場合でも、各段階の「手で行う手順」は必ず報告に書いてもらう（後から別の人が直せるように）。

## 先に結論

- **Antigravity で作れる。** スクリプトは Antigravity。シーンの配置は「MCP があれば Antigravity、無ければ人間」。
- これは **イベント（EventData）としては作らない。** EventData は「選択肢ボタンを押す」形式で、文字入力や候補選びの部品が無い。しかも「ゲーム開始直後に1回だけ・時間を止めて出す」ものなので、**専用のパネル**として作る。
- `{name}` の置き換えは**もう動いている**（下の表）。足りないのは「名前を決める画面」「名前を書き込む窓口」「セーブ」「子どもカードの名前欄」の4つ。

## 現状の確認結果（2026-11-18、Unity とリポジトリを直接確認）

| 項目 | 現状 | 影響 |
|---|---|---|
| 開始の流れ | `Start_scene` → `TitleMenuUI` → `GameLaunchRequest` → `Main_test2`。名前画面はスポットライト型チュートリアルの完了後に表示 | 「続きから」では表示せず、直接 Play は `showInEditorPlay` が ON のときだけ表示 |
| `GameStartup` | `Consume` の結果に応じてロードまたは名前決めを開始 | `Consume` はこのクラスで1回だけ読む |
| 名前の持ち方 | `PersonStatus.displayName` に保持し、`SetDisplayName` と `OnDisplayNameChanged` で変更・通知する | 母・父にも使える基底クラスの窓口 |
| セーブ | `FamilySaveParticipant.SavedChild` に表示名を保存する | 古いセーブで名前が無い場合は既存名を維持 |
| `{name}` の置き換え | `Story/StoryTextFormatter.Format()` が置換する。名前が空または「未設定」なら「みらい」を返す | イベント・チュートリアル・選択肢表示はこの窓口を使う |
| 置き換え漏れ | 選択肢の家計簿名も `eventTitle` と `choice.label` を `Format()` に通す | イベント表示ラベルと家計簿名を一致させる |
| 子どもカードの名前 | `ChildCardUI.nameLabel` に表示し、名前変更通知を購読する。未接続時は子カード内の「子ども」等の TMP を探す | シーンの参照接続が無くても表示できる |
| 時間停止 | AGENTS.md ルール8：判断画面は `OnEnable` で `GameClock.I.PushPause()`、`OnDisable` で `PopPause()`（`CalendarUI` と同じ形） | 名前のパネル自身が時間を止める。流れの担当（`ChildNamingFlow`）では止めない |

## 決めておくこと（たたき台。変えてよい）

- 名前の長さ：1〜8文字。前後の空白は削る。空のままは決定できない。`{` `}` を含む名前は不可（置き換えを壊すため）。
- 名前が未設定（空・「未設定」）のときは、今の `StoryTextFormatter` と同じく「**みらい**」を使う。
- 候補ボタン（例：みらい・はると・ゆい・そうた・めい・あおい）＋「ランダム」を必ず用意する。最初に名前欄に入っている値は「みらい」。
- 自由入力欄は `[SerializeField] bool useFreeInput` で ON/OFF できるようにする。
- 決定の前に「○○でいいですか？［はい］［やり直す］」の確認を1回挟む。

### ⚠ 要確認：自由入力は WebGL（unityroom）・iPad で動かない可能性がある

Unity の入力欄（TMP_InputField）は、WebGL では日本語の IME 入力やタブレットのキーボード表示がうまく動かないことがある。**確証は無い**（Unity のバージョンと端末で変わる）。公開予定が unityroom＋iPad なので、**候補ボタン＋ランダムを主役にし、自由入力は「動いたら使う」扱い**にする。
→ 本採用の前に **WebGL ビルドを iPad で試す**。動かなければ `useFreeInput=false` にするだけで済む作りにしてある。

---

## 段階の全体像

| 段階 | 内容 | 丸山さんのフォルダ | 終わると何ができるか |
|---|---|---|---|
| A | 名前の書き込み窓口・通知・セーブ | 触らない | 名前を持てて、セーブ/ロードで残る。見た目は変わらない |
| B | 名前を決める画面と、開始時の呼び出し | 触らない | 「最初から」で画面が出て、決めた名前が子どもに入る |
| C | 子どもカードの名前・家計簿の置き換え漏れ・固定文字の洗い出し | 触る（1か所） | カードに名前が出る。家計簿にも名前で残る |

## 始める前にやること（人間）

- [x] このファイルを `docs/team/antigravity-child-name.md` としてリポジトリに置き、AGENTS.md の索引に1行足す（2026-11-18 済）。
- [x] AGENTS.md のルール5に、今回の例外（`Assets/Maruyama/Scripts/UI/EventChoiceButtonUI.cs` は山口が変更してよい）を足す（2026-11-18 済）。
- [ ] 段階C の前に、丸山さんに「`EventChoiceButtonUI.cs` の家計簿の名前を `{name}` 対応で1か所変更する」と一声かける。

---

## 共通の前文（毎回、段階の指示の前に付ける）

```
あなたは Unity 製の子育てシミュレーションゲームの実装を担当します。

【必ず最初に読むもの】
1. リポジトリ直下の AGENTS.md（エージェント向けルールとドキュメント索引）
2. docs/code/architecture.md（GameState の API、時間の流れ）
3. docs/team/antigravity-child-name.md（今回の依頼の全体。「現状の確認結果」の表を必ず読む）
4. 今回の段階の指示で指定したファイル

【Unity MCP について】
- Unity MCP ツールが使える環境なら、使ってよい。スクリプトを書いたあと、
  シーンへの配置・コンポーネントの追加・Inspector の参照の接続・OnClick の登録まで
  MCP で行ってよい。終わったらシーンを保存し、コンソールにエラーが無いことを確認する。
- MCP が使えない環境なら、シーンとプレハブは触らず、人間がやる手順を報告に書いて終える。
- どちらの場合でも、「人間が手で同じことをする手順」を報告に必ず書く。
  MCP で済ませた場合も省略しない（後から別の人が直したり作り直したりするため）。
  手順は「どの GameObject に、何のコンポーネントを付け、Inspector のどの欄に何を入れるか」を
  上から順に書く。
- シーンファイル（.unity）・プレハブのテキストを直接書き換えない。変更は必ず
  Unity（MCP）経由か、人間の手で行う。
- MCP で再生して確認した場合は、何を確認したかを報告に書く。

【守ること】
- 仕様を推測しない。ドキュメントに書かれていないことは「未定」として作業を止め、
  何が分からないかを報告する。勝手に決めない。
- 数値・文字数・候補の名前・画面の文言はすべて [SerializeField] にして
  Inspector から変えられる形にする。
- 機能ごとにクラス・ファイルを分ける。1つのファイルに長々と書かない。
- 作るファイルは Assets/Yamaguchi/Scripts/ の下に置く。
- Assets/Maruyama/ は、その段階の指示で「変更してよい」と書いたファイルだけ変更してよい。
  それ以外の Maruyama のファイル、Assets/Arisaka/、Assets/Kusao/ は変更しない。
- シリアライズしているフィールドの名前は変えない（保存済みの値が外れるため）。
- {name} の置き換えは Assets/Yamaguchi/Scripts/Story/StoryTextFormatter.Format を使う。
  同じ働きのクラスや、自前の Replace("{name}", …) を新しく作らない。
- 時間を止める画面は AGENTS.md ルール8 に従う（OnEnable で PushPause、OnDisable で PopPause）。
- 参照は [SerializeField] で公開し、日本語の [Tooltip] を付ける。
- UI の文字は TextMeshPro（JPFont）。TextOverflowModes.Ellipsis は使わない
  （docs/code/ui-scroll-patterns.md の「ハマりどころ」）。
- ボタンの押下は OnClick に public メソッドを Inspector で登録する形にする
  （既存の TitleMenuUI と同じ。コードで AddListener しない）。OnClick の繋ぎ忘れには
  OnClickCheck.Warn で警告を出す。
- コメントは日本語で書く。既存コードの文体に合わせる。

【終わったら報告すること】
- 追加・変更したファイルの一覧と、それぞれ何をするものか1行ずつ
- Assets/Maruyama/ で変更した箇所の一覧（丸山さんに伝えるため）
- MCP で行ったシーンの変更の一覧（行っていなければ「なし」）
- 人間が手で配置・接続する手順（MCP で済ませた場合も書く）
- 人間が Unity で確認する手順（上から順に）
- コンパイルが通ること
- 仕様が足りずに判断できなかった点があれば、その一覧
```

---

## 段階A：名前の書き込み窓口・通知・セーブ

```
Assets/Yamaguchi/Scripts/Core/ の PersonStatus.cs / ChildStatus.cs / FamilySaveParticipant.cs と
Assets/Yamaguchi/Scripts/Story/StoryTextFormatter.cs を読んでから作業してください。

【目的】
子どもの名前を「書き込む窓口」「変更通知」「セーブ」で扱えるようにする。見た目は変えない。

【変更してよい Assets/Maruyama/ のファイル】
なし。

【変更するもの】

PersonStatus.cs
- public event Action<string> OnDisplayNameChanged を足す
- public void SetDisplayName(string value) を足す。
  前後の空白を削り、変わらないときは通知しない。空文字は受け付けず LogWarning だけ出す。
  （母・父にも使える形で基底クラスに置く。フィールド名 displayName は変えない）

FamilySaveParticipant.cs
- SavedChild に string displayName を足す（Capture で書き、RestoreChild で SetDisplayName）
- 古いセーブ（displayName が無い・空）をロードしてもエラーにならず、名前を上書きしない
- 母・父の名前はこの段階では保存しない

StoryTextFormatter.cs
- ChildName：DisplayName が空のときに加えて「未設定」のときも "みらい" を返す
  （PersonStatus の既定値が "未設定" のため。シーンで名前を入れ忘れても「未設定」と出さない）
- それ以外の動きは変えない

【シーンの作業】
なし。

【確認】
- コンパイルが通ること
- 人間の確認手順：再生中に Household の子ども（ChildStatus）の名前を変えてもエラーが出ないこと／
  セーブ → ロードで名前が戻ること／名前の無い古いセーブでも読み込めること
```

---

## 段階B：名前を決める画面と、開始時の呼び出し

```
段階A が終わっていることを前提に、Assets/Yamaguchi/Scripts/Core/GameStartup.cs、
GameLaunchRequest.cs、Assets/Yamaguchi/Scripts/Title/TitleMenuUI.cs、
既存の ConfirmDialogUI（確認ダイアログの作り）、CalendarUI（時間の止め方）と
docs/decisions/0004-pause-during-decisions.md を読んでから作業してください。

【目的】
「最初から」で本編が始まった直後に「子どもが生まれた！！！名前を決めよう」の画面を出し、
プレイヤーが決めた名前を ChildStatus に入れる。決めるまで時間を止める。

開始時のスポットライト型チュートリアル（TutorialGuideController）がある場合は、
チュートリアルの完了またはスキップを待ってから名前画面を開く。
チュートリアルを確認済み、またはシーンにチュートリアルが無い場合はすぐに開く。

【変更してよい Assets/Maruyama/ のファイル】
なし。

【作るもの】Assets/Yamaguchi/Scripts/UI/Naming/ の下

ChildNameValidator.cs
- static。長さ（1〜8。値は呼ぶ側から渡す）・前後の空白・
  禁止文字（{ と }）の判定を1か所にまとめる
- 結果（OK か）と、画面に出す理由の文字列を返す

ChildNamePanelUI.cs（画面の部品。流れの制御はしない）
- Open(Action<string> onDecided) / Close()
- 時間の停止：OnEnable で GameClock.I.PushPause()、OnDisable で GameClock.I.PopPause()
  （AGENTS.md ルール8。CalendarUI と同じ形。GameClock.SetRunning は使わない）
- 見出し「子どもが生まれた！！！」、案内「名前を決めよう」（文言は [SerializeField]）
- 候補ボタン：候補の文字列は [SerializeField] List<string>
  （既定：みらい / はると / ゆい / そうた / めい / あおい）。
  ボタンは入れ物（Transform）の下にプレハブから作る。押すと名前欄に入る（まだ決定ではない）
- ［ランダム］：候補から1つ入れる。直前と同じ名前は続けて出さない
- 自由入力欄（TMP_InputField）：useFreeInput が false のときは欄を SetActive(false)。
  maxLength（既定8）を入力欄にも設定する
- 名前欄の初期値は "みらい"（[SerializeField]）
- 判定に通らないとき［決定］を押せなくし、理由を小さく出す
- ［決定］→ 確認「○○でいいですか？」［はい］［やり直す］
  （ConfirmDialogUI の文言を外から変えられるなら使い回す。変えられないなら
   Naming/ に小さな確認部品を足す。ConfirmDialogUI 自体は変えない）
- 確認画面の質問文・［はい］・［やり直す］の文字色は黒
- ［はい］で onDecided(name) を呼んで閉じる

ChildNamingFlow.cs（流れの担当。時間は止めない）
- Begin()：
    ① ChildNamePanelUI.Open（パネルが表示されると、パネル自身が時間を止める）
    ② 決まったら Household.I.Child.SetDisplayName(name)
    ③ パネルを閉じる（閉じるとパネル自身が時間を戻す）
- 二重に呼ばれても1回しか動かない
- Household.I / Child が無いときは LogWarning を出して何もしない（パネルも開かない）

【変更するもの】

GameStartup.cs
- ChildNamingFlow の参照を [SerializeField] で持つ
- GameLaunchRequest.Consume の結果が NewGame のときだけ ChildNamingFlow.Begin() を呼ぶ
  - Continue：呼ばない（ロードした名前を使う）
  - None（エディタで本編を直接再生）：[SerializeField] bool showInEditorPlay（既定 false）が
    ON のときだけ呼ぶ
- Consume はこのクラスの中で1回だけ読む（他で読むと値が消える）
- 参照が未設定なら LogWarning を1回出し、名前は「みらい」のまま始める

【シーンの作業（MCP があれば行う。無ければ手順を書く）】
Main_test2 で：
1. Canvas の一番下（最前面）に ChildNamePanel を作る。暗幕（黒60%の Image・全画面）＋
   パネル（クリーム色）。中に見出し・案内・候補ボタンの入れ物（GridLayoutGroup 3列）・
   ランダムボタン・名前欄（TMP_InputField）・理由の TMP・決定ボタン
2. 候補ボタンのプレハブを Assets/Yamaguchi/Prefabs/UI/ に作る（Button＋TMP）
3. ChildNamePanel に ChildNamePanelUI を付け、各欄を繋ぐ。最初は非表示（SetActive false）
4. ボタンの OnClick に ChildNamePanelUI の public メソッドを登録
5. GameManager に ChildNamingFlow を付け、ChildNamePanelUI を繋ぐ
6. GameManager の GameStartup の「ChildNamingFlow」欄に繋ぐ
7. シーンを保存
※ レイアウトの寸法はたたき台。4:3（800×600 基準）で文字が読めること

【確認】
- コンパイルが通ること
- 人間の確認手順：Start_scene から「最初から」→ パネルが出る → お金・年齢が止まっている →
  候補を押す → 決定 → 確認で「はい」→ 時間が動き出す → 子どもの名前が変わっている
- 「続きから」ではパネルが出ないこと
- パネルが出ている間に年齢が進まないこと
```

---

## 段階C：子どもカードの名前・家計簿の置き換え漏れ・固定文字の洗い出し

```
段階A・B が終わっていることを前提に、StoryTextFormatter.cs、
Assets/Yamaguchi/Scripts/UI/ChildCardUI.cs、
Assets/Maruyama/Scripts/UI/EventChoiceButtonUI.cs を読んでから作業してください。

【目的】
決めた名前を子どもカードに出し、家計簿に残る文字の置き換え漏れを直す。
イベントのタイトル・説明・選択肢のラベル・チュートリアルはすでに StoryTextFormatter.Format を
通しているので、触らない。

【変更してよい Assets/Maruyama/ のファイル】
- Scripts/UI/EventChoiceButtonUI.cs … 家計簿に書く名前（ledgerName）の {name} 置き換えだけ

【1】子どもカードの名前
- ChildCardUI に TextMeshProUGUI nameLabel を [SerializeField] で足す（Tooltip：カード上部の名前）
- StoryTextFormatter.ChildName を表示し、PersonStatus.OnDisplayNameChanged を購読して更新
- 購読と解除は既存の Subscribe / Unsubscribe に入れる（ラムダにしない）
- nameLabel が未設定なら、カード内にある「子ども」「こども」「みらい」の TMP を名前欄として探す

【2】家計簿の置き換え漏れ
- EventChoiceButtonUI の ledgerName を作るところで、eventTitle と choice.label の両方を
  StoryTextFormatter.Format に通す（表示のラベルと同じ文字になるように）
- 医療費などの付け足しの文字（label += …）は家計簿に入れない今の動きを変えない
- Debug.Log の中身は変えなくてよい
- 既存イベントアセットに「みらい」が直書きされている場合は `{name}` に置き換える。
  イベント作成用の Editor スクリプト側も同様にし、再生成後に固定名へ戻らないようにする

【3】名前を出したい他の場所を洗い出す（実装はしない）
- 画面に「子ども」「こども」「みらい」を固定で出している箇所を探し、一覧で報告する
  （スクリプトの文字列・シーン/プレハブの TMP・イベント／チュートリアルのアセットの文章）
- 振り返り（LifeRecorder）や育成記録に、置き換える前の文字（{name} のまま）を保存していないかも確認する
- 一覧にはファイル名・行（またはオブジェクトのパス）・今の文字を書く。直すかどうかは人間が決める

【シーンの作業（MCP があれば行う。無ければ手順を書く）】
1. Main_test2 の子どもカードで「子ども」と書いてある TMP を探す
2. ChildCardUI の nameLabel に繋ぐ
3. シーンを保存

【確認】
- コンパイルが通ること
- 人間の確認手順：名前を決める → 子どもカードに名前が出る →
  {name} を書いたイベントを編集ウィンドウの［今すぐ出す］で出す → 名前に置き換わる →
  その選択肢を押す → 家計簿にも名前で残る → セーブ → ロードで名前が残る
- {name} を書いていない既存のイベントの表示と家計簿が変わらないこと
```

---

## 段階のあとに人間がやること（まとめ）

1. MCP を使わなかった場合：段階B・C の「シーンの作業」を手順どおりに行う
2. 自由入力を使うなら **WebGL ビルドを iPad で試す**。動かなければ `useFreeInput` を OFF
3. 段階C の洗い出しの一覧を見て、名前に変える場所を決める
4. イベント・チュートリアルの文章に `{name}` を書き込む（例：「{name}が熱を出した」）

## 他の依頼文との関係

- `antigravity-event-editor.md` 段階⑤の「【2】{name}」は**実装済み**（`StoryTextFormatter`）。この依頼では作り直さない。
- タイトル画面（`antigravity-title-screen.md`）の流れは変えない。名前画面は本編側（`GameStartup`）から出す。

## 将来の拡張メモ（今は作らない）

- 母・父の名前も決められるようにする（`SetDisplayName` は基底クラスに置くので、画面を足すだけで済む）
- エンディング・育成記録に名前を出す
- 「○○ちゃん」「○○くん」の呼び分けは性別の設定が無いため未定
