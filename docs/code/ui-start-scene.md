---
title: スタート画面（Start_scene）の構築・設定手順
updated: 2026-10-14
status: 確定
related: [docs/design/title-screen.md, docs/design/save-system.md, docs/decisions/0019-two-save-slots.md]
---

# スタート画面（Start_scene）の構築・設定手順

> `Assets/Yamaguchi/scenes/Start_scene.unity` を Unity エディタ上で手動構築するための完全ガイドです。
> スクリプト側の詳細仕様は [docs/design/title-screen.md](../design/title-screen.md) を参照してください。

---

## 1. シーンの GameObject 階層構造

シーン内に以下の階層で GameObject を配置します。  
**注意:** `SaveSelect` および `ConfirmDialog` スクリプトは「常にアクティブな親オブジェクト」に付け、開閉する表示実体はその子（`SaveSelectRoot` / `ConfirmRoot`）とします（閉じている間も Esc キー入力を検知するため）。

```text
Start_scene
├── EventSystem                  # InputSystemUIInputModule をアタッチ
├── Main Camera
└── Canvas                       # Render Mode: Screen Space - Overlay, Canvas Scaler
    ├── Background               # Image（背景画像・自作差し替え用）
    ├── Logo                     # Image（タイトルロゴ画像・自作差し替え用）
    ├── TitleMenu                # [TitleMenuUI] をアタッチ
    │   ├── ContinueButton       # Button + 子 TMP「続きから」
    │   ├── NewGameButton        # Button + 子 TMP「最初から」
    │   ├── RecordsButton        # Button + 子 TMP「育成記録」
    │   ├── QuitButton           # Button + 子 TMP「終了」
    │   └── VersionText          # TMP_Text「ver 0.1」
    │
    ├── SaveSelect               # [SaveSelectPanelUI] をアタッチ（常時アクティブ）
    │   └── SaveSelectRoot       # 初期状態: 非アクティブ（パネル本体）
    │       ├── Dim              # 全画面 Image (半透明黒) + Button（暗幕）
    │       ├── PanelBackground  # パネル枠・背景
    │       ├── TitleText        # TMP_Text「セーブデータを選んで下さい」
    │       ├── AutoCard         # Button + [SaveSlotCardUI]
    │       │   ├── SlotLabel    # TMP_Text / Image「[AUTO] オートセーブ」
    │       │   ├── Content      # セーブ情報親オブジェクト
    │       │   │   ├── AgeText      # TMP_Text（例: 8歳 3月1日）
    │       │   │   ├── MoneyText    # TMP_Text（例: 1,234,000円）
    │       │   │   └── SavedAtText  # TMP_Text（例: 2026/10/14 14:55 に保存）
    │       │   └── EmptyLabel   # TMP_Text「データがありません」
    │       ├── ManualCard       # Button + [SaveSlotCardUI]
    │       │   ├── SlotLabel    # TMP_Text / Image「[💾] 手動セーブ」
    │       │   ├── Content      # セーブ情報親オブジェクト
    │       │   │   ├── AgeText      # TMP_Text
    │       │   │   ├── MoneyText    # TMP_Text
    │       │   │   └── SavedAtText  # TMP_Text
    │       │   └── EmptyLabel   # TMP_Text「データがありません」
    │       └── BackButton       # Button + 子 TMP「もどる」
    │
    └── ConfirmDialog            # [ConfirmDialogUI] をアタッチ（常時アクティブ）
        └── ConfirmRoot          # 初期状態: 非アクティブ（ダイアログ本体）
            ├── Dim              # 全画面 Image (半透明黒) + Button（暗幕）
            ├── DialogBackground # ダイアログ枠・背景
            ├── MessageText      # TMP_Text「最初から始めますか？\n今のセーブデータは...」
            ├── YesButton        # Button + 子 TMP「はい」
            └── NoButton         # Button + 子 TMP「いいえ」
```

---

## 2. コンポーネントと Inspector 設定

各スクリプトが要求する `[SerializeField]` への参照を Inspector で紐付けます。

### 2.1 TitleMenu (TitleMenuUI)
- **Continue Button:** `Canvas/TitleMenu/ContinueButton`
- **New Game Button:** `Canvas/TitleMenu/NewGameButton`
- **Records Button:** `Canvas/TitleMenu/RecordsButton`
- **Quit Button:** `Canvas/TitleMenu/QuitButton`
- **Confirm Dialog:** `Canvas/ConfirmDialog` (ConfirmDialogUI)
- **Save Select Panel:** `Canvas/SaveSelect` (SaveSelectPanelUI)
- **Game Scene Name:** `Main_test2` (本編シーン名)

### 2.2 SaveSelect (SaveSelectPanelUI)
- **Root:** `Canvas/SaveSelect/SaveSelectRoot`
- **Auto Card:** `Canvas/SaveSelect/SaveSelectRoot/AutoCard` (SaveSlotCardUI)
- **Manual Card:** `Canvas/SaveSelect/SaveSelectRoot/ManualCard` (SaveSlotCardUI)
- **Back Button:** `Canvas/SaveSelect/SaveSelectRoot/BackButton`
- **Background Button:** `Canvas/SaveSelect/SaveSelectRoot/Dim` (Button)
- **Game Scene Name:** `Main_test2`

### 2.3 AutoCard / ManualCard (SaveSlotCardUI)
※ AutoCard と ManualCard それぞれにアタッチして設定
- **Button:** 自身の `Button` コンポーネント
- **Age Text:** `Content/AgeText`
- **Money Text:** `Content/MoneyText`
- **Saved At Text:** `Content/SavedAtText`
- **Content:** `Content` (GameObject)
- **Empty Label:** `EmptyLabel` (GameObject)

### 2.4 ConfirmDialog (ConfirmDialogUI)
- **Root:** `Canvas/ConfirmDialog/ConfirmRoot`
- **Yes Button:** `ConfirmRoot/YesButton`
- **No Button:** `ConfirmRoot/NoButton`
- **Background Button:** `ConfirmRoot/Dim` (Button)

---

## 3. Button の OnClick() イベント登録対応表

本タイトル画面では、スクリプト内での `AddListener` ではなく **Inspector の `OnClick()` による明示的な配線** を基本とします。

| ボタン GameObject | 登録先オブジェクト | 呼び出す関数 (No parameters) |
|---|---|---|
| `ContinueButton` | `TitleMenu` | `TitleMenuUI.OnClickContinue` |
| `NewGameButton` | `TitleMenu` | `TitleMenuUI.OnClickNewGame` |
| `RecordsButton` | `TitleMenu` | `TitleMenuUI.OnClickRecords` |
| `QuitButton` | `TitleMenu` | `TitleMenuUI.OnClickQuit` |
| `AutoCard` | `SaveSelect` | `SaveSelectPanelUI.OnClickAutoSlot` |
| `ManualCard` | `SaveSelect` | `SaveSelectPanelUI.OnClickManualSlot` |
| `BackButton` | `SaveSelect` | `SaveSelectPanelUI.OnClickBack` |
| `SaveSelect/Dim` | `SaveSelect` | `SaveSelectPanelUI.OnClickBackground` |
| `YesButton` | `ConfirmDialog` | `ConfirmDialogUI.OnClickYes` |
| `NoButton` | `ConfirmDialog` | `ConfirmDialogUI.OnClickNo` |
| `ConfirmDialog/Dim` | `ConfirmDialog` | `ConfirmDialogUI.OnClickBackground` |

> [!TIP]
> 登録漏れがある場合、起動時に `[OnClickCheck]` から「○○ボタンの OnClick が未設定です」という警告ログが出力されます。

---

## 4. 本編シーン（Main_test2）と Build Settings の設定

### 4.1 本編シーン側の準備
1. `Assets/Yamaguchi/scenes/Main_test2.unity` を開く。
2. `GameManager` オブジェクトに **`GameStartup.cs`** を追加する。
   - `GameStartup` は、タイトルから渡された開始要求（`GameLaunchRequest`）を検出し、必要なセーブスロットを1フレーム待機後にロードします。

### 4.2 Build Settings への登録
`File > Build Settings...` を開き、Scenes in Build に以下の順序で登録します。
- **0:** `Assets/Yamaguchi/scenes/Start_scene.unity`
- **1:** `Assets/Yamaguchi/scenes/Main_test2.unity`

---

## 5. UI 設定上の注意点

1. **固定文言の直書きルール:**
   - ボタン名（「続きから」「最初から」「もどる」など）やダイアログのメッセージ文言は、シーン側の TextMeshProUGUI に直接記入してください（コード側で上書きしません）。
   - コードが書き換えるのは動的な数値（年齢・お金・保存日時）のみです。
2. **フォント:**
   - 日本語を表示する TextMeshPro にはプロジェクト共通の `JPFont` を指定してください。
3. **キーボード・ゲームパッドのフォーカス移動:**
   - タイトルメニューのボタンの Navigation 設定は、ダイアログやパネル表示中に背面へフォーカスが逃げないよう適切に設定してください（Panel表示中は Dim が入力をブロックします）。
4. **暗幕 (Dim):**
   - 背後へのマウスクリック貫通を防ぐため、Image の `Raycast Target` を **ON** にしてください。

---

## 6. 動作確認チェックリスト

- [ ] **初回起動（セーブなし）:**
  - 「続きから」ボタンがグレーアウト（Disabled Color）していること。
  - 初期フォーカスが「最初から」ボタンにあること。
  - 「最初から」を押すと確認ダイアログなしで `Main_test2` が初期状態で開始すること。
- [ ] **セーブ作成:**
  - `Main_test2` で `SaveDebugMenu` を使用し、オート枠と手動枠の両方にセーブを作成する。
- [ ] **セーブありでの起動:**
  - `Start_scene` を再生したとき、「続きから」が有効化され初期選択状態であること。
  - 「続きから」を押すとセーブ選択パネルが開き、より保存日時が新しいスロットにフォーカスが当たること。
  - 各カードに正しい年齢・お金・日時が表示されていること。
  - 各カードをクリックして本編へ遷移し、正しくデータが復元されること。
- [ ] **「最初から」の確認ダイアログ:**
  - セーブがある状態で「最初から」を押すと確認ダイアログが開くこと。
  - 初期フォーカスが「いいえ」になっていること。
  - Escキー、暗幕クリック、「いいえ」でダイアログが閉じ、フォーカスが「最初から」に戻ること。
  - 「はい」を押して新規ゲームを開始しても、元のセーブファイルが削除されていないこと。
