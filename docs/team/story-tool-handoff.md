---
title: ストーリー管理・制作ツールの引き継ぎ
updated: 2026-11-13
status: イベント制作ツールは完成。次はストーリーウィンドウ（未着手）
related: [event-editor.md, story-tool.md, antigravity-event-editor.md]
---

# ストーリー管理・制作ツールの引き継ぎ（2026-11-13）

> 別の会話でストーリーのツールを作り始めるための、現状のまとめ。
> 設計の本体は `docs/design/story-tool.md`（Project では `game-design/story-tool.md`）。このメモは「いまのコードがどうなっているか」と「作るときの注意」を足したもの。

---

## 1. 次にやること

**ストーリーウィンドウ**（イベントのつながり全体を見る・フラグを管理する Unity エディタ拡張）を作る。メニュー案：子育て → ストーリー → ストーリーウィンドウ。
event-editor.md の段階⑥にあたる。①〜⑤（イベント編集ウィンドウ・ストーリーの土台）は終わっているので、着手できる。

- 設計は story-tool.md に、4つのタブ（フラグ／流れ図／ストーリーの文章／再生中の状態）と、スクリプトの分け方まで書いてある。
- 仁山は「ストーリー**管理**と**制作**のツール」と言っている。story-tool.md は主に「管理（見る・フラグを作る）」。**「制作」をどこまで含めるか（例：流れ図の上でイベントや続きを作る・つなぐ、ストーリーの文章を作る）は、新しい会話の最初に仁山に確かめる**こと。いまのイベント編集ウィンドウにはすでに「続きを新しく作る」「分岐の入口テンプレ」がある。
- 作り方：仁山は「Claude が直接実装してよい」と言っている（Antigravity にプロンプトを渡す形もあるが、段階④以降は Claude が直接作った）。

## 2. 提案する進め方（3回に分ける）

| 回 | 中身 | 理由 |
|---|---|---|
| A | フラグのタブ（一覧・作成・名前変更・削除・逆引き・チェック）＋再生中の状態のタブ | フラグのアセットがまだ1つも無い。先に作る道具が要る。逆引きの部品は EventRegistry にある |
| B | 流れ図 | いちばん大きい。A の索引を使う。IMGUI で箱と線を描くだけ（GraphView は使わない） |
| C | ストーリーの文章のタブ | チュートリアルの条件・タイミングを一覧する。編集はチュートリアルの編集ウィンドウのまま |

story-tool.md 4章の「まだ決めること」（流れ図の「全部」の上限・［取り消す］を入れるか・職業の箱は CareerDefinition ができてから）は、A の前後で仁山に聞く。

---

## 3. いまできていること（ランタイム）

保存先は `Assets/Yamaguchi/Scripts/Story/`。

| 部品 | 役目・使い方 |
|---|---|
| `StoryFlag`（ScriptableObject） | id・displayName・description・趣味の呼び名（useCustomHobbyName／customHobbyName／hobbyKind）。**アセットはまだ1つも無い**（story-tool.md は `Assets/Yamaguchi/Data/Story/Flags/Flag_<ID>.asset` に作る案） |
| `StoryState`（`StoryState.I`） | 窓口：`SetFlag` `ClearFlag` `HasFlag` `GetFlagAsset` `ActiveFlagOrder` `GetFlagSetDay`、`HasOccurred` `TimesOccurred` `ChoiceOf`、`Schedule` `ConsumeScheduledForDay` `ScheduledEvents`、`CaptureSaveData` `RestoreSaveData` `Clear`。イベント：`OnFlagChanged` `OnFlagIdChanged` `OnChoiceRecorded` `OnRestored`。**変更は必ずこの窓口を通す**（中のリストを直接触ると通知が出ない） |
| `StorySaveParticipant` | セーブキー `"story"`、RestoreOrder 115 |
| `StoryEventScheduler`（`.I`） | 毎日、予約日が来た「続き」と、条件を満たした「条件」イベントを `EventManager.Enqueue` する。予約日は `scheduledAgeDay <= ageDay` で判定（飛び越えても拾う）。同じ「条件」イベントを二重に積まない |
| `StoryBootstrap` | シーンに `EventManager` があって `StoryState` が無いとき、"Story" オブジェクト（StoryState・StorySaveParticipant・StoryEventScheduler）を自動で作る。手でシーンに置かなくてよい |
| `Conditions/*` | 条件の部品10個：DateRange・Month・SchoolGrade・ChildStat・Money・Support・Trait・Requirement・ChoiceHistory・StoryFlag。すべて `EventConditionSO` を継承し、`IDescribable.Describe()` を持つ |
| `HobbyDisplayName.Get(kind)` | 立っているフラグの呼び名（後から立ったもの優先）。画面の「スポーツ」を「野球」にする |
| `StoryTextFormatter.Format` | `{name}` を子どもの名前に置き換える |
| `Core/Requirements/StoryFlagRequirementSO.cs` | 職業・支援制度の条件からフラグを見る |

`EffectKind` は14個（末尾に Set/Clear Flag・SetHobby・ScheduleEvent・ShowStory を追加済み。**番号で保存されるので、途中に入れず後ろに足すだけ**）。`Effect.asset` に参照（フラグ・特性・イベント・チュートリアル）を入れる。

チュートリアル側：`TutorialTiming` に「フラグが立った日」（TutorialTimingKind.FlagSet）、`TutorialPage.showConditions`、`TutorialData.HasAvailablePage()`。出せるページが無いチュートリアルは「見た」にせず飛ばす。

## 4. いまできていること（エディタ）

`Assets/Yamaguchi/Scripts/Editor/Event/` にイベント編集ウィンドウ一式（メニュー：子育て → イベント → 編集ウィンドウ）。ストーリーウィンドウで**使い回せる**もの：

| 部品 | 使えるところ |
|---|---|
| `EventRegistry` | `All()` `AllFlags()` `FindFlagUsers(flag)`（立てる・消す・見る場所の逆引き）、`FindCallersOf` `FindFollowUpsOf`（続きの前後）、`FindConditionUsersOf`（前の選択を条件にしているもの）。アセットが変わると `Invalidate()` される（`EventRegistryWatcher`） |
| `EventConditionList` | 条件リストの編集（選ぶ・新しく作る＝サブアセット・共用警告）。`CleanupUnreferenced` で不要なサブアセットを消す |
| `EventPlayModeSection` | 再生中のテスト・ストーリーの状態。再生中タブと部品を共有する想定 |
| `EventDescriber` / `EventValidator` / `EventFactory` | まとめ文・チェック（✕⚠）・テンプレとファイル作成（ID の自動生成と重複確認） |
| `EventTimelineView` | 年表。流れ図の描き方の参考 |
| `TutorialEditorWindow.cs`（Editor 直下） | ストーリーの文章の編集はここ。ストーリーウィンドウからは「開く」だけ |
| `DebugToolsWindow.cs` | 「選択肢を固定」（イベント→選択肢のプルダウン。EditorPrefs `Kosodate.Debug.FixedChoices`） |

使い方のガイド（HTML）：`docs/guides/event-tool/index.html`（2026-11-13 に作り直し。画像は同じフォルダの img）。コードの説明：`docs/code/event-editor-window.md`。

## 5. 今日（2026-11-13）に直したイベントの画面

ストーリーのツールを作るときに前提になる変更。

- **カード（EventCard.prefab）**：幅 560。絵・タイトル・説明・選択肢を上から自動で並べる（VerticalLayoutGroup＋ContentSizeFitter）。選択肢1〜3は縦、4つは2×2（`GridChoicesContainer`、`gridChoicesContainer` に配線済み）。`ChoiceBlock` に高さ（1〜2個=100・3個=86・4個=118）を付けた。
- **`CardFitScaler`（新規・`Maruyama/Scripts/UI`）**：画面に入りきらないとき、カード（2枚並びなら並び全体）を縮める。ゲームのキャンバスは基準 800×600・幅合わせ（match 0）で、横長の画面では高さが足りなくなるため。2枚同時だと約0.63倍で文字が小さい（要相談）。
- **最前面（`EventDialogUI.MoveToTopCanvas`）**：起動時にイベントのダイアログを専用 Canvas（`EventDialogCanvas`、描画順1100）へ移す。食事選択（950）・カレンダー（999）・HUD（100）より前に出る。
- **食事のあとに出す（`EventManager`）**：食事選択（`MealSelectionDialogUI`）が開いている間はイベントを出さずに待ち、閉じたら出す（`TryShowNext` と `Update`）。月の初めに食事選択と固定イベントが重なっていたため。

## 6. まだ確認していないこと・残っていること

- **セーブ → タイトル → つづきから**で、フラグ・選んだ履歴・予約中の続きが戻るか。
- **年齢ジャンプ**で、続きの予約日を飛び越えたときに出るか（コードは `<=` で拾う作りだが実機で通していない）。
- メニュー「子育て → イベント → 選択肢に ID を付ける」を1回実行する（既存イベントの選択肢 ID が空のままだと警告が出る）。
- カードの2枚並びが小さい件（2枚のときだけ幅を狭くして文字を保つ案がある）。
- **git：Claude は何もコミットしていない**。`EventChoiceButtonUI.cs` はコンフリクトを手で解決済みだが、GitHub Desktop で「Continue merge」を押してコミット・プッシュする必要がある。
- `Maruyama/` 以下を変えたので、丸山さんに一声かける（決定事項）。
- `StoryFlag` のアセットが無いので、動作確認用のフラグ（例：`plays_baseball`／野球・スポーツ）は A の回で作る。
- CareerDefinition（職業）はまだ無い。流れ図の職業の箱はその後。

## 7. 決まっていること（変えないこと）

- イベントの編集ウィンドウ＝1つのイベントを作る道具／ストーリーウィンドウ＝つながり全体を見て、フラグを管理する道具。別のツール。
- 野球は趣味の種類を増やさず「スポーツ＋フラグ」。プレイヤーの画面には呼び名「野球」を出す。
- 選択肢は最大4つ、4つは2×2。
- ID（イベント・選択肢・フラグ）はセーブのキーなので、ゲームに出したあとは変えない。名前（タイトル・ラベル・表示名）は変えてよい。
- ストーリーウィンドウはランタイムに何も足さない（見る・編集するだけ）。変更は `StoryState` の窓口経由。
- 流れ図は IMGUI（GraphView を使わない）・自動配置・ドラッグで並べ替えはしない（たたき台）。
- 「ここで変えたフラグはセーブに残ります。効果（お金など）は戻りません」を再生中タブに書く。

## 8. 作業環境の注意（Claude 用）

- **Unity MCP の `execute_code` は C# 6（CodeDom）**：`using` 行は書けない（完全修飾名で書く）、`default` リテラル・ローカル関数は不可、`Object` は `UnityEngine.Object` と書く。`AssetDatabase.DeleteAsset` は `safety_checks=false` が要る。
- コンパイルのあと Unity が固まることがある（"Access version should be odd when acquiring lock" など）。応答が数分戻らなければ、仁山に Unity の再起動を頼む。コンソールの `NoSubscription`（Unity AI generators）は無関係。
- 丸山さんのファイルは CRLF。置換するときは改行コード・BOM を保つ（デバイス側に `~/edit.py` がある）。
- 画面のスクリーンショット：オーバーレイの UI は **Play 中**の `manage_camera screenshot`（カメラ指定なし）で写る。`screenshot_super_size: 2` で高解像度のファイルが `Assets/Screenshots/` にできる（使い終わったら消す）。
- 確認用の ScriptableObject は `ScriptableObject.CreateInstance` でメモリ上に作り、`EventDialogUI.Show(demerit, merit)` や `EventManager.Instance.Enqueue(data)` に渡せば、アセットを作らずに画面を確かめられる。
- 仁山（山口 航平）への報告は日本語で、短く。ファイルは `SendUserFile` で渡す。

## 9. 関連ファイル

| 場所 | 中身 |
|---|---|
| `docs/design/story-tool.md` | ストーリーウィンドウの設計 |
| `docs/design/event-editor.md` | イベント編集ウィンドウとストーリー分岐の設計（4章がランタイム） |
| `docs/team/antigravity-event-editor.md` | Antigravity への依頼文（段階①〜⑤。⑥は未作成） |
| `docs/code/event-editor-window.md` | イベント編集ウィンドウのコード説明 |
| `docs/guides/event-tool/index.html` | イベント制作ツールの使い方 |
| `docs/guides/tutorial-tool/index.html` | チュートリアル制作ツールの使い方 |
| `docs/decisions/0034-tutorial-panel.md` `0035-tutorial-timing-in-data.md` | チュートリアルまわりの決定 |
| `docs/decisions/0036-review-records-and-family-ending.md` | 振り返り（LifeRecorder）。`EventManager.OnEventResolved` を共有 |
