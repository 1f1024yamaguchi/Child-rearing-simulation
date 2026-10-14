---
title: Antigravity 用プロンプト（タイトル画面とセーブ2枠）
updated: 2026-10-14
status: 運用中
related: [docs/design/title-screen.md, docs/design/save-system.md, docs/decisions/0019-two-save-slots.md]
---

# Antigravity 用プロンプト（タイトル画面とセーブ2枠）

> 仕様は [../design/title-screen.md](../design/title-screen.md)。
> 頭に [antigravity-child-status.md](antigravity-child-status.md) の「共通の前文」を付けてから、下の指示を貼る。
> 育成記録（title-screen.md 6章）はまだ依頼しない。

```
docs/design/title-screen.md と docs/design/save-system.md を全部読んでから作業してください。
今回は title-screen.md の 1〜5章が対象です。6章（育成記録）は作りません。

【Unity MCP について】
Unity の MCP ツールは使わないこと。シーンの配置・再生確認は人間が行う。

【いちばん大事なこと】
ボタンとテキストは、人間がシーンに置く uGUI の Button / TextMeshProUGUI を使う。
- コードで UI を生成しない
- ボタンやダイアログの固定の文字をコードで書き換えない。
  コードが TMP に入れてよいのは、セーブごとに変わる値（年齢・お金・保存日時）だけ
- ボタンの押下は、Button の OnClick()（Inspector）に登録できる public メソッドとして用意する。
  タイトル画面のスクリプトでは AddListener しない
- 参照はすべて [SerializeField] で受け取り、日本語の [Tooltip] を付ける

【1. セーブを2枠にする】

Assets/Yamaguchi/Scripts/Core/SaveSlot.cs（新規）
- enum SaveSlot { Auto, Manual }
- [Serializable] class SaveSlotInfo：slot / exists / savedAt（string）/ ageDays / money

Assets/Yamaguchi/Scripts/Core/SaveSystem.cs（変更）
- 枠ごとにファイルを分ける：Manual = 今の save.json（そのまま。今あるセーブは手動枠になる）、
  Auto = save_auto.json
- 追加：Save(SaveSlot) / Load(SaveSlot) / HasSave(SaveSlot) / Delete(SaveSlot) / FilePathOf(SaveSlot)
  / HasAnySave() / GetSlotInfo(SaveSlot)
- 今の引数なしのメソッドは残す（他の人のコードが呼んでいるため）：
  ・Save()     → Save(SaveSlot.Auto)   ※ Assets/Kusao/CalendarManager.cs が呼んでいる。Kusao のファイルは触らない
  ・Load()     → 保存日時が新しいほうの枠を読む
  ・HasSave()  → HasAnySave()
  ・Delete()   → 2枠とも消す（AutoSaver がゲーム終了時に呼ぶ）
  ・FilePath   → FilePathOf(SaveSlot.Manual)
- GetSlotInfo は SaveData の savedAt / ageDays / money だけを使う。
  ファイルが無い・壊れているときは例外を出さず exists=false で返す
- SaveData の項目は増やさない。ロードの順番（時間を止める → Resync → 値 → 参加者）は変えない

Assets/Yamaguchi/Scripts/UI/SaveButton.cs（変更）
- SaveSystem.Save(SaveSlot.Manual) を呼ぶように変える。それ以外は変えない

Assets/Yamaguchi/Scripts/Core/AutoSaver.cs
- SaveSystem.Save() が Auto 枠になるので、変更は不要のはず。読んで確認し、変えたなら理由を報告

Assets/Yamaguchi/Scripts/Core/SaveDebugMenu.cs（変更）
- 「オート枠に保存」「手動枠に保存」「オート枠をロード」「手動枠をロード」
  「両方のセーブを消す」「セーブの場所をログに出す（2枠とも）」にする

【2. タイトル → 本編の受け渡し】

Assets/Yamaguchi/Scripts/Core/GameLaunchRequest.cs（新規）
- static class。enum Mode { None, NewGame, Continue } と、Continue のときの SaveSlot
- 既定は None。Consume() で今の値を返して None に戻す

Assets/Yamaguchi/Scripts/Core/GameStartup.cs（新規）
- 本編シーンの GameManager に付ける
- GameLaunchRequest.Consume() を見て：
  ・Continue → SaveSystem.Load(枠)。失敗したらログを出して初期値のまま始める
  ・NewGame / None → 何もしない
- 参加者（ISaveParticipant）が全員 Register し終わってから Load すること。
  Start で足りるか、1フレーム待つ必要があるかを判断して、理由を報告に書く

【3. タイトル画面】

Assets/Yamaguchi/Scripts/UI/ConfirmDialogUI.cs（新規）
- はい／いいえの汎用ダイアログ。タイトル以外でも使い回せるように作る
- [SerializeField]：ダイアログのルート（GameObject）、はいボタン、いいえボタン
- Open(Action onYes, Action onNo = null)：表示して、既定の選択を「いいえ」にする
- public void OnClickYes() / OnClickNo() / OnClickBackground()（暗幕。いいえと同じ）
- Esc でも閉じる（新 Input System。Keyboard.current）
- 文言は持たない

Assets/Yamaguchi/Scripts/Title/SaveSlotCardUI.cs（新規）
- カード1枚の表示だけ
- [SerializeField]：Button、年齢と日付の TMP、お金の TMP、保存日時の TMP、
  中身（値の TMP をまとめた GameObject）、「データがありません」の GameObject
- Show(SaveSlotInfo)：
  ・exists → 年齢は GameDate.Format(ageDays)、お金は "N0"＋"円"、日時は「yyyy/MM/dd HH:mm に保存」。
    Button.interactable=true
  ・無い → 中身を隠して「データがありません」を出す。interactable=false
- 固定の文字（「オートセーブ」など）とアイコンはシーン側。コードで触らない

Assets/Yamaguchi/Scripts/Title/SaveSelectPanelUI.cs（新規）
- [SerializeField]：パネルのルート、オート用と手動用の SaveSlotCardUI、もどるボタン、本編シーン名
- Open()：GetSlotInfo で2枠を読んでカードに渡して表示。既定の選択は savedAt が新しいほう
- public void OnClickAutoSlot() / OnClickManualSlot()：
  GameLaunchRequest を Continue＋その枠にして本編シーンを読み込む（確認なし）
- public void OnClickBack() / OnClickBackground()、Esc：閉じて、閉じたことを event で知らせる

Assets/Yamaguchi/Scripts/Title/TitleMenuUI.cs（新規）
- [SerializeField]：続きから／最初から／育成記録／終了 の Button、ConfirmDialogUI、SaveSelectPanelUI、
  本編シーン名（string、既定 "Main_test2"。Build Settings への登録が必要なことを Tooltip に書く）
- Start：
  ・HasAnySave() が true なら 続きから を interactable=true にして既定で選択
  ・false なら 続きから を interactable=false（グレーアウト）、既定の選択を 最初から に
  ・育成記録ボタンは interactable=false
  ・各ボタン（パネル・ダイアログのボタンも）の onClick.GetPersistentEventCount() が 0 なら
    「○○ボタンの OnClick が未設定です」と警告ログ
- public void OnClickContinue()：SaveSelectPanelUI.Open()。閉じたら選択を 続きから に戻す
- public void OnClickNewGame()：
  ・セーブがあれば ConfirmDialogUI.Open。はい → NewGame で本編へ。いいえ → 選択を 最初から に戻す
  ・セーブが無ければ確認せず NewGame で本編へ
  ・セーブは消さない
- public void OnClickRecords()：今は何もしない（ログのみ）
- public void OnClickQuit()：Application.Quit。エディタでは EditorApplication.isPlaying = false

【触らないもの】
GameState.cs / シーンファイル / プレハブ / ProjectSettings / 他メンバーのフォルダ（Assets/Kusao など）

【確認】
- コンパイルが通ること
- 人間がシーンを作るための手順を、上から順に報告に書くこと：
  ・Start_scene に置く GameObject の階層（Canvas / ロゴと背景の Image / 各ボタン /
    セーブ選択パネルと2枚のカード / ダイアログ / 暗幕 / EventSystem）
  ・各 Button の OnClick() にどのオブジェクトのどのメソッドを登録するか
  ・Build Settings の順番（Start_scene を 0 番、Main_test2 を 1 番）
  ・GameStartup を付ける場所
- 動作確認の手順を報告に書くこと（SaveDebugMenu で2枠にセーブを作る → Start_scene から再生 →
  新しいほうが既定で選ばれる → それぞれの枠から再開できる → 最初からでセーブが消えていない）
```
