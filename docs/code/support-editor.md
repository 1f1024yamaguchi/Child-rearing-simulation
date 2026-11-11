---
title: 支援制度の編集ウィンドウ（エディタ拡張）
updated: 2026-11-11
status: 実装済み（山口）／Unity での動作確認待ち
related: [docs/code/how-to-add-support-scheme.md, docs/design/support-systems.md, docs/design/support-apply.md]
---

# 支援制度の編集ウィンドウ

支援制度（`SupportScheme`）を、スクリプトを触らずに **作る・直す・チェックする・SupportManager に登録する** ためのエディタ拡張。TutorialEditorWindow（[ui-tutorial.md](ui-tutorial.md)）と同じ作りで、ひらがな多めの日本語 UI。

- 開き方：メニュー **「子育て → 支援制度 → 編集ウィンドウ」**。または、制度アセットの Inspector の **「編集ウィンドウで開く」**（Inspector には制度のまとめ文も出る）。
- ランタイムのスクリプト（`SupportScheme` / `SupportManager` / `PaymentTier` / `RequirementSO` など）は **変更していない**。追加したのは `Assets/Yamaguchi/Scripts/Editor/Support/` の中だけ。
- 値の書き換えは **すべて `SerializedObject` / `SerializedProperty` 経由**。Ctrl+Z で戻せて、アセットのダーティも効く。

---

## 1. 使い方

1. 上の「制度」に制度アセットを入れる（Project で選んでも出る）。無ければ **［新しく作る ▼］** でテンプレを選び、保存先（`Assets/Yamaguchi/Data/Supports/`）とファイル名を決める。**［複製して作る］** は今開いている制度のコピー（id は新しいファイル名から付け直す）。
2. 上の **「まとめ」** で、カテゴリ・申請開始・期限・支給表・条件・合計の目安を確認する。合計の目安は「条件を全部満たした場合」の数字（各段の月額 × 段の月数の合計＋申請時のお金）。
3. **「使う項目」** のスイッチで、この制度で使う欄を選ぶ。ONにした項目の欄だけが下に出る。
   - ONにすると空の1行が足される。OFFにすると確認ダイアログが出て、入っている中身が消える。
   - スイッチの状態はデータには保存しない。「リストが空でないか」「`changesWorkMode` が true か」で毎回決まる。
4. 上から順に、基本 → もらい方 → 申請できる期間 → 申請条件 → お金・効果 → 肩代わり・医療費 → 働き方・給与連動 を埋める。
5. **「チェック」** の ✕ を全部消す（⚠ は「たぶん意図と違う」ので内容を見て判断）。
6. **「シーンへの登録」** で SupportManager に登録されているか確認する。新しく作った制度は自動で登録される。
7. 再生して、**「再生中のテスト」** で申請・サポート画面の確認をする。

---

## 2. 画面の並び（上から）

| 欄 | 中身 | ファイル |
|---|---|---|
| 説明・制度・［新しく作る］［複製］ | メニュー・選択・全体の並び | `SupportEditorWindow.cs` |
| まとめ | 文章にして確認 | `SupportSchemeDescriber.cs` |
| 基本 | id / title / description / icon（サムネイル）/ category / merits / demerits | `SupportBasicSection.cs` |
| もらい方 | `effectTiming`（OnApply／Monthly のラジオ） | `SupportPaymentSection.cs` |
| 使う項目 | 6つのトグル | `SupportFeatureToggles.cs` |
| 申請できる期間 | start、deadlineType と、種類に合う欄だけ。下に文章の説明 | `SupportPeriodSection.cs` |
| 申請条件 | `RequirementSO` のリスト（`PaymentTier.requirements` にも同じ部品） | `SupportRequirementList.cs` |
| お金・効果 | effects、支給表（monthlyTiers）、支給月（payoutMonths） | `SupportPaymentSection.cs` |
| 肩代わり・医療費 | covers / medicalExpenseCovers | `SupportCoverSection.cs` |
| 働き方・給与連動 | workTarget / workMode / 給与連動の段 | `SupportWorkSection.cs` |
| チェック | ✕・⚠ のリスト | `SupportSchemeValidator.cs` |
| シーンへの登録 | SupportManager の `schemes` に登録／外す | `SupportRegistry.cs` |
| 再生中のテスト | 状態・申請・サポート画面を開く・ログ | `SupportPlayModeSection.cs` |
| すべての制度 | 一覧・カテゴリのタブ・表示順の入れ替え | `SupportListSection.cs` |

共通の入力部品（`GameDay` を「□歳 [月▼] [日▼]」で入れる／円入力／1〜12月のトグル／文字列リスト／↑↓削除ボタン）は `SupportEditorGUI.cs`。新規作成・複製・id の自動生成は `SupportSchemeFactory.cs`。

---

## 3. 各欄のポイント

### 3.1 もらい方（effectTiming）と「お金・効果」

- **OnApply**：`effects` の見出しは「申請したときに起きること」。
- **Monthly**：`effects` の見出しは「申請したとき1回だけ起きること（手間のストレスなど・空でよい）」。毎月の支給は支給表（monthlyTiers）・肩代わり・医療費・給与連動が担当する。
- `effects` の1行は kind / target / value / duration（`Days` のときだけ days が出る）。kind が Money なら value の横に「+15,000 円」と出る。

### 3.2 毎月の支給表（monthlyTiers）

- 1段 ＝ 名前 / 何歳から / 何歳まで / **毎月の金額（円）** / お金以外の効果（折りたたみ）/ この段だけの条件。追加・↑↓・削除ボタン付き。
- 「毎月の金額」は、その段の `monthlyEffects` のうち **kind=Money・target=Household の最初の1行** を直接書き換える。無ければ、金額を変えた時点で1行足す。「お金以外の効果」にはその行を出さない。
- 下に「年齢ごとの月額」の表が出る（各歳の初日に当てはまる段。追加条件は見ない）。
- **支給月（payoutMonths）**：1〜12月のトグル。全部OFF（＝項目自体がOFF）で毎月支給。項目がONのあいだは最低1つの月が残る（最後の1つは外せない）。

### 3.3 申請条件（SupportRequirementList）

- **［＋条件を足す ▼］**：「既存アセットから選ぶ」か、「新しく作る」（`TypeCache` で見つけた `RequirementSO` の子クラスが並ぶ。新しい条件クラスを足せば自動で増える）。
- 新規は `Assets/Yamaguchi/Data/Supports/Requirements/Req_<型名からSOを除いた名前>_<制度id>.asset` に保存（フォルダが無ければ作る）。
- 各条件は `Editor.CreateCachedEditor` でその場で中身を編集でき、`Describe()` の結果が出る。
- 同じアセットを **ほかの制度**（`applyRequirements` / `monthlyTiers[].requirements`）や **支出項目**（`ExpenseItem`）が参照していたら「ほかでも使っています：〇〇」と警告が出る。**［この制度専用にコピー］** で `AssetDatabase.CopyAsset` して、この制度の参照だけ差し替える。
- 行の ✕ は参照を外すだけで、条件アセットは消さない。

### 3.4 肩代わり・医療費

- 種類は 全額／割合／○円まで。値は、全額なら出さない・割合は 0〜100% のスライダー（データは 0〜1）・○円までは円入力。
- `expense`（肩代わりする支出）は covers のみ。医療費の助成には無い。

### 3.5 働き方・給与連動

- 「対象の親（workTarget）」は、働き方を変えるときも給与連動だけのときも使うので、どちらかがONなら出す。
- 給与連動の段は `fromDay` / `toDay`（-1＝ずっと）/ `rate`（％で入力、データは 0〜1）/ `basis`。

### 3.6 チェック（SupportSchemeValidator）

| 印 | 内容 |
|---|---|
| ✕ | id が空／id が他の制度と重複（全 SupportScheme アセット中）／id に英小文字・数字・`_` 以外／title が空／段・肩代わり・医療費の fromAge > toAge／Once で deadline < start |
| ⚠ | Monthly なのに支給表・肩代わり・医療費・給与連動がすべて空／OnApply なのに支給表がある／支給表の段の期間が重なる（この段だけの条件がある段どうしは数えない）／条件・expense が None の行／icon なし／SupportManager に未登録 |

### 3.7 登録（SupportRegistry）

- シーンの `SupportManager`（`FindFirstObjectByType`、非アクティブ含む）の private な `schemes` に、`SerializedObject` で登録／外す。
- SupportManager が **プレハブのインスタンス** なら、元のプレハブを開いて書き込む（シーン側は上書きにならない）。
- SupportManager が無いシーンでは警告が出るだけで、アセットの編集はできる。
- 一覧の ↑↓ は `schemes` の並び（＝サポート画面の表示順）を、**同じカテゴリの前後** と入れ替える。

### 3.8 再生中のテスト

- 状態（`GetState`）・満たしていない条件（`UnmetRequirements` の `Describe`）・期限まで（`DaysUntilDeadline`）を出す。
- **［申請する（テスト）］**：`SupportManager.I.Apply`。`CanApply` が false のときは理由を出す。
- **［サポート画面でこの制度を開く］**：`SupportPanelController.Open(id)`。
- **［状態をログに出す］**：Console に1行出す。
- 再生中に変えた値は **アセットに残る**（再生を止めても戻らない）。気になるときは Ctrl+Z か git で戻す。

---

## 4. 新しく作る（テンプレ）

| テンプレ | 中身 |
|---|---|
| 空 | 何も入れない |
| 一時金 | OnApply。effects に Money +0 / Household を1行 |
| 毎月の手当 | Monthly。支給表1段（0歳〜2歳12月31日・お金 0） |
| 無償化 | Monthly。covers 1行（全額・0歳〜5歳12月31日・expense は None） |
| 医療費の助成 | Monthly。medicalExpenseCovers 1行（全額・0歳〜5歳12月31日） |
| 育休・時短 | Monthly。changesWorkMode ON。wageLinkedTiers 1段（1日目からずっと・67%・休業前の給与） |

- **id は自動生成**：ファイル名の先頭の `Support_` を外して、PascalCase を snake_case にする（`Support_ChildAllowance` → `child_allowance`）。ほかの制度と重なったら `_2` `_3` を付ける。
- 作ったら SupportManager に自動登録し、選択状態にする。**シーンを保存する**のを忘れない（登録はシーンのデータ）。

---

## 5. 設計の判断（なぜこうしたか）

- **機能ごとにファイルを分けた**：1ファイル1責務の規約（[unity-coding.md](../../.agents/rules/unity-coding.md)）に合わせた。新しいデータ項目は、該当の Section を直せば済む。
- **「使う項目」を保存しない**：データを増やすと `SupportScheme` を変えることになる。中身が空かどうかで判定すれば、ランタイムに手を入れずに済む。
- **UI の描画数は Layout と Repaint で合わせる**：IMGUI は2回描くので、「見つかったり見つからなかったりする」値（SupportManager、再生中の状態）は Layout のときだけ読み直す（`SupportRegistry.FindManager` / `SupportPlayModeSection`）。ずれると「Getting control N's position in a group with only N controls」のエラーになる。
- **ボタンで配列を変えたら、その回の描画をやめる**：並べ替え・削除のあとは同じ配列を回し続けない（`break`）。

## 6. 足すとき

- **新しい条件クラス**（`RequirementSO` の子）：ウィンドウ側は何もしなくてよい。「新しく作る」の一覧に自動で出る。
- **`SupportScheme` に新しい項目を足した**：該当の Section に1行足す。使う項目のトグルが要るなら `SupportFeatureToggles.cs`、チェックが要るなら `SupportSchemeValidator.cs`、一覧の「種類」に影響するなら `SupportSchemeDescriber.KindOf`。
- **`EffectKind` / 各 enum を足した**：`SupportEditorGUI.cs` の日本語ラベルの配列（`KindNames` など）に、**enum と同じ順番で** 足す（足し忘れると英語名で出る）。

