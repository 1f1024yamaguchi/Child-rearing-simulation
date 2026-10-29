---
title: タイトル画面（続きから／最初から／育成記録）
updated: 2026-10-14
status: 確定（育成記録の画面は本編後）
related: [docs/design/save-system.md, docs/team/antigravity-title-screen.md, docs/decisions/0019-two-save-slots.md, docs/design/rules-and-stats.md, docs/design/child-status.md]
---

# タイトル画面（続きから／最初から／育成記録）

> ゲーム起動時の画面。セーブがあれば **続きから** を既定で選んだ状態にし、**最初から** は確認を挟む。
> セーブは **オートセーブ** と **手動セーブ** の2枠（[ADR 0019](../decisions/0019-two-save-slots.md)）。続きからを押すと、どちらから再開するか選ぶパネルを出す（3章）。
> ゲームが終わったときの記録（就いた職業・稼いだお金・家出したか）を見る **育成記録** は、本編ができてから作る（6章は今は設計だけ）。
> 参考：ゲーム発展国++ のタイトル（最初から／ゲームロード／ハイスコア）とセーブ選択（AUTO枠＋手動枠）。

---

## 1. 決まったこと（2026-10-14）

| 項目 | 決定 |
|---|---|
| 既定の選択 | セーブがあれば **続きから** |
| セーブが無いときの「続きから」 | **グレーアウト**（interactable=false）。非表示にはしない |
| 最初からを押したとき | 確認ダイアログを出す。**セーブは消さない**（次のセーブで上書きされる） |
| セーブ枠 | **オート**と**手動**の2枠。続きからで選ぶ |
| オートセーブのタイミング | **月初の処理（給料・支給・食事など）が全部終わった後**（今の `AutoSaver` のとおり） |
| ボタン・テキスト | uGUI の `Button` と `TextMeshProUGUI` をシーンに置き、必ず Inspector に出す（2章） |
| タイトルロゴ・背景 | チームで自作する。シーンには差し替え用の Image を置いておく |
| 表記 | 「シミュレーション」に統一する（AGENTS.md ルール10） |
| 育成記録の件数 | **上限なし** |
| 育成記録の並び順 | **ゲームが終わった日時の新しい順** |

---

## 2. 画面の構成

```
┌──────────────────────────────┐
│                                │
│        （タイトルロゴ：自作）      │
│                                │
│         [ 続きから ]  ← 既定で選択中 │
│         [ 最初から ]               │
│         [ 育成記録 ]  ← 本編後に有効 │
│                         ver 0.1 │
│                         [ 終了 ] │
└──────────────────────────────┘
```

### シーン
- タイトルは **`Assets/Yamaguchi/scenes/Start_scene.unity`**（作成済み）。Build Settings の **0番** に置き、本編シーン（いまは Main_test2）を 1番 に足す。
  今の Build Settings には `SampleScene` しか入っていない。
- タイトル → 本編は `SceneManager.LoadScene`。
- タイトルシーンには `GameManager`（`GameState` など）を置かない。理由は 4章。
- EventSystem は本編と同じ `InputSystemUIInputModule`（新 Input System）。

### UI は「シーンに置いて Inspector で見える」を守る

**ボタン・テキストはすべて uGUI の `Button` と `TextMeshProUGUI` を人間がシーンに置く。** コードで生成しない。

| ルール | 理由 |
|---|---|
| ボタンの文字（「続きから」など）は各ボタンの子の TMP に直接書く。コードで上書きしない | Inspector で見たまま変えられるように |
| ダイアログ・パネルの固定の文言も TMP に直接書く（`[SerializeField] string` で持たない） | 同上 |
| コードが TMP に入れるのは **セーブごとに変わる値だけ**（年齢・お金・保存日時） | 固定の文字と変わる値を分ける |
| スクリプトは `[SerializeField]` で Button / TMP / パネルの参照を受け取るだけ | 見た目と処理を分ける |
| ボタンの押下は **Button の OnClick()（Inspector）に public メソッドを登録**する | 何が繋がっているか Inspector で見える |
| 日本語フォントは既存の `JPFont`（TMP） | |

> 本編の `SaveButton` や `MainTestHUD` は実行時に AddListener しているが、タイトル画面では Inspector の OnClick() を使う。
> 繋ぎ忘れに気付けるよう、`Start` で各ボタンの `onClick.GetPersistentEventCount() == 0` なら警告ログを出す。

### 既定の選択（「続きから」が光っている状態）
- `Start` で `EventSystem.current.SetSelectedGameObject(続きからボタン)`。
- 見た目は Button の Transition（Selected Color）で出す。ゲーム発展国の水色のハイライトに相当。
- **2枠のどちらにもセーブが無い**ときは、続きからを interactable=false（Disabled Color でグレー）にし、既定の選択を **最初から** に移す。

### 最初からの確認

```
┌──────────────────────────┐
│ 最初から始めますか？               │
│ 今のセーブデータは、次にセーブした    │
│ ときに上書きされます                │
│   [ はい ]   [ いいえ ]          │  ← 既定は「いいえ」
└──────────────────────────┘
```

| 状況 | 動き |
|---|---|
| セーブがある | 確認ダイアログを出す。既定の選択は **いいえ**（誤操作に強くするため） |
| セーブが無い | 確認せずにすぐ本編へ |

- **押した時点ではセーブを消さない。**
  - オート枠：新しい育成で次の月替わりが来たときに上書きされる。
  - 手動枠：プレイヤーが［セーブ］を押したときだけ上書きされる。→ 前の育成を手動枠に残しておける。
- ダイアログが開いている間は、後ろのボタンを押せないようにする（ダイアログの背面に全画面の暗幕 Image を置き raycastTarget=true。Main_test2 の supports/background と同じ作り）。
- いいえ／暗幕クリック／Esc で閉じ、選択を **最初から** に戻す。

### 終了
- `Application.Quit()`。エディタでは効かないので、`#if UNITY_EDITOR` で `EditorApplication.isPlaying = false` にする。

---

## 3. セーブ選択パネル（続きから → オートか手動かを選ぶ）

```
┌──────────────────────────┐
│     セーブデータを選んで下さい      │
│                              │
│ ┌ [AUTO] オートセーブ ───────┐ │ ← 保存日時が新しいほうが既定で選択中
│ │ 8歳 3月1日      1,234,000円 │ │
│ │ 2026/10/14 14:55 に保存     │ │
│ └──────────────────────┘ │
│ ┌ [💾] 手動セーブ ──────────┐ │
│ │ 7歳 12月2日       980,000円 │ │
│ │ 2026/10/13 21:10 に保存     │ │
│ └──────────────────────┘ │
│                    [ もどる ] │
└──────────────────────────┘
```

### 2つの枠
| 枠 | いつ書かれるか | ファイル |
|---|---|---|
| オートセーブ | 月初の処理が全部終わった後（`AutoSaver`）。カレンダーのメモを書いたとき（`CalendarManager.AutoSave`）も | `save_auto.json`（新規） |
| 手動セーブ | 本編の［セーブ］ボタン（`SaveButton`） | `save.json`（**今のファイルをそのまま手動枠にする**） |

- 枠はこの2つで固定（手動を複数スロットにはしない）。
- 月替わりに保存するのは、月初の判断画面（食事など）の途中で保存すると、再開したときに処理が二重に走るおそれがあるため。今の `AutoSaver` は月替わりでフラグを立て、他の処理が全部終わった `LateUpdate` で1回だけ保存している。
- 引数なしの `SaveSystem.Save()` は **オート枠** に保存する。カレンダー（草尾さん）のコードは直さずに済む。

### パネルの動き
- 続きからを押すと、タイトルの上にセーブ選択パネルを開く（暗幕つき。後ろのボタンは押せない）。
- カードは **2枚をシーンに固定で置く**（オート用・手動用）。カードは Button。コードで生成しない。
- 既定の選択は **保存日時が新しいほう**（片方しか無ければそちら）。
- セーブが無い枠のカードは **interactable=false**、中身は「データがありません」。
- カードを押したらその枠から再開（確認は挟まない。ゲーム発展国と同じ）。
- もどる／暗幕クリック／Esc でパネルを閉じ、選択を 続きから に戻す。

### カードに出すもの
| 項目 | 例 | 取得元 |
|---|---|---|
| 枠のアイコン・名前 | AUTO オートセーブ | 固定（シーンの Image / TMP） |
| 子どもの年齢と日付 | 8歳 3月1日 | `SaveData.ageDays` → `GameDate.Format` |
| お金 | 1,234,000円 | `SaveData.money` |
| 保存した日時（実時間） | 2026/10/14 14:55 に保存 | `SaveData.savedAt` |

- `SaveData` の先頭にはすでに `savedAt` / `ageDays` / `money` がある。カードはこの3つだけを使う（新しい項目は足さない）。
- 年齢の書式は `GameDate.Format` を使う。HUD（`MainTestHUD`）と書式が違う場合は、合わせ方を相談する（今回は HUD を直さない）。
- 子どもの名前の仕組みができたら、ゲーム発展国の会社名の位置に名前を出す（今は無し）。

---

## 4. タイトル → 本編の受け渡し

タイトルは「どう始めるか」だけを決め、**実際の読み込みは本編シーン側で行う**。

```
Start_scene                        本編シーン（Main_test2）
TitleMenuUI / SaveSelectPanelUI    GameStartup（GameManager に付ける）
  カードを押す → Continue + 枠  ─→   Start で Request を見る
  最初から    → NewGame         ─→     Continue … SaveSystem.Load(枠)
                                         NewGame  … 何も読まず初期値で開始
                                         None     … いまと同じ（エディタで本編を直接再生した時）
```

- 受け渡しは **static クラス `GameLaunchRequest`**（`Mode { None, NewGame, Continue }` ＋ Continue のときの `SaveSlot`）。MonoBehaviour にしない。
- 本編側で読んだら `None` に戻す。
- **既定が `None` なのが大事**：Main_test2 をエディタで直接再生しても今と同じ動きになり、各メンバーの作業を邪魔しない。
- タイトルに `GameState` を置かない理由：`DontDestroyOnLoad` のシングルトンなので、タイトルと本編の両方に置くと二重になる。タイトルに必要なのは「各枠の見出し」だけで、これはファイルを読めば分かる。
- `SaveSystem.Load()` は `GameState` と `GameClock` が要るので、タイトルでは呼べない。必ず本編の `GameStartup` から呼ぶ。
- 「本編からタイトルに戻る」は今回は作らない。

### SaveSystem に足すもの（[save-system.md](save-system.md) 3章も更新済み）
- `enum SaveSlot { Auto, Manual }`。
- `Save(SaveSlot)` / `Load(SaveSlot)` / `HasSave(SaveSlot)` / `Delete(SaveSlot)` / `FilePathOf(SaveSlot)`。
- 引数なしの `Save()` は `Save(SaveSlot.Auto)`。`SaveButton` は `Save(SaveSlot.Manual)` に変える。
- `HasAnySave()`：どちらかの枠にあるか。
- `GetSlotInfo(SaveSlot)`：ファイルを読んで `savedAt` / `ageDays` / `money` を返す。ファイルが無い・壊れているときは例外を出さず `exists=false`。
- 引数なしの `Delete()` は **2枠とも消す**（ゲームが終わったときに `AutoSaver` が呼ぶ。下記）。

### ゲームが終わったとき（既存の決定どおり）
- ゴール・家出でゲームが終わったら、**2枠とも消す**（save-system.md 3章「ゲームオーバー・クリア → セーブを消す」）。終わった育成の続きから遊べてしまうのを防ぐ。
- 終わった育成は、代わりに **育成記録** に1件残る（6章。本編後）。

---

## 5. スクリプトの分け方

| ファイル | 役割 | いつ |
|---|---|---|
| `Scripts/Core/SaveSlot.cs` | `enum SaveSlot` と、見出しの `SaveSlotInfo`（exists / savedAt / ageDays / money） | 今回 |
| `Scripts/Core/SaveSystem.cs`（変更） | 2枠対応（4章） | 今回 |
| `Scripts/UI/SaveButton.cs`（変更） | 手動枠に保存する | 今回 |
| `Scripts/Core/GameLaunchRequest.cs` | タイトル→本編の受け渡し（static） | 今回 |
| `Scripts/Core/GameStartup.cs` | 本編の開始時に Request を見て読み込み／新規開始 | 今回 |
| `Scripts/UI/ConfirmDialogUI.cs` | はい/いいえの汎用ダイアログ。文言はシーンの TMP | 今回（他の確認にも使い回す） |
| `Scripts/Title/TitleMenuUI.cs` | タイトルのボタンの押下・既定の選択・続きからの有効/無効 | 今回 |
| `Scripts/Title/SaveSelectPanelUI.cs` | セーブ選択パネルの開閉・既定の選択（新しいほう）・カードの押下 | 今回 |
| `Scripts/Title/SaveSlotCardUI.cs` | カード1枚の表示（SaveSlotInfo を受け取って TMP に値を入れるだけ） | 今回 |
| `PlayRecord.cs` / `PlayRecordStore.cs` | 記録1件のデータ／一覧の保存・読み込み | 本編後 |
| `PlayRecordListUI.cs` | 育成記録の画面 | 本編後 |

---

## 6. 育成記録（本編ができてから。今は設計だけ）

ゲーム発展国の「ハイスコア」に当たる画面。**ゲームが終わる（20歳到達・家出）たびに1件残す。**

### 1件の中身（たたき台）
| 項目 | 例 | 取得元 |
|---|---|---|
| 終わった日時（実時間） | 2026/10/14 15:00 | `DateTime.Now`（並び順にも使う） |
| 結末 | 巣立ち ／ 家出 ／ 家計破綻 | `GameState.OnGameEnded(GameEndReason)`（決定 0024）。保存は [result-scene.md](result-scene.md) 5章の `RecordStore`（2026-10-29 決定） |
| 就いた職業 | 研究者 | エンディング判定（未設計。rules-and-stats.md 1章） |
| 到達年齢 | 20歳 ／ 14歳（家出） | `GameState.AgeDays` |
| 稼いだお金の合計 | 9,840万円 | **生涯の給料累計が必要**（下記） |
| 最終の貯金 | 1,230万円 | `GameState.Money` |
| 子どもの成績・趣味 | 82% ／ スポーツLv5 | `ChildStatus` |
| 子育てにかかったお金 | 2,100万円 | 家計簿の累計（expenses.md） |

- 「稼いだお金の合計」は `IncomeHistory`（直近12か月）では足りない。**生涯の給料累計**を別に数える必要がある。→ 本編側で今のうちに入れておくと後が楽（セーブの参加者にも入れる）。
- 「子育てにかかったお金」はこのゲームの学習目的（子育てにいくらかかるか）に一番効く項目なので、記録に入れることを推奨。

### 保存
- **セーブデータとは別のファイル**（例 `records.json`）に、記録のリストとして保存する。最初からを押しても、ゲームが終わってセーブが消えても残る。
- **件数の上限なし**（2026-10-14 決定）。1件は数百バイトなので、何百件あっても問題ない。
- 記録を書くのは、セーブを消す（`AutoSaver.HandleGameOver`）**前**。

### 画面
- タイトルの「育成記録」ボタン → 一覧（縦スクロール。既存の `status_scroll` と同じ作り）→ 1件を押すと詳細。
- 並び順は **終わった日時の新しい順**（2026-10-14 決定）。
- 一覧の行は件数が決まらないので、**行のプレハブ（Button＋TMP）を Inspector で作っておき、それを件数分 Instantiate する**。行の中の固定の文字はプレハブの TMP に書く。
- 記録が0件のときは「まだ記録がありません」（シーンの TMP）。
- **本編ができるまで、育成記録ボタンは interactable=false**（シーンには置いておく）。

---

## 7. まだ決めていないこと

- 年齢の書式を `GameDate.Format` と HUD のどちらに合わせるか（違っていた場合）。
- 育成記録の「就いた職業」の判定（エンディング側の設計待ち）。
