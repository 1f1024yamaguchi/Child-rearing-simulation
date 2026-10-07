---
title: 制度カード（SupportCard）とサポート画面の実装
updated: 2026-10-07
status: 実装済み（ステップ2・ステップ3前半）
related: [docs/design/support-apply.md, docs/code/ui-scroll-patterns.md, docs/team/antigravity-support-apply.md]
---

# 制度カード（SupportCard）とサポート画面の実装

> サポート画面に並ぶ **制度カード1枚** の作りと使い方の記録。支援制度の申請（[support-apply.md](../design/support-apply.md)）のステップ2にあたる。
> このファイルは「仕様（なぜそうするか）」ではなく **「実際にどう作ったか・どう使うか」** を書く。仕様は support-apply.md 7章が正。
> カードを並べる・タブ切替・時間停止（ステップ3前半）は 8章。確認ダイアログと通知（ステップ3後半）は未実装。

---

## 1. できること

- 制度アセット（`SupportScheme`）を渡すと、カード1枚に **名前・説明・状態バッジ・効果チップ・支給表・期限** を表示する。
- 状態が変わったら（`SupportManager.OnStateChanged`）、**そのカードの表示だけ** を更新する。カードは作り直さない。
- 日付が進むたび（`DayTicker.OnDayPassed`）に「あと○日」を更新する。期限まで **あと7日以下は赤字**。
- 主ボタンは状態で「申請する／更新する／手続きする」に変わり、押せない状態では消える。
- ［カレンダーに追加］は、まだ申請・手続きできる制度にだけ出る（押してもログだけ。ステップ5でつなぐ）。

実際の見た目（Play で 0歳2月10日、児童手当を申請済みのとき）：

```
┌───────────────────────────────────────────────────────────┐
│ [icon] 児童手当                                [ 受給中 ]   │  ← バッジ（緑）
│        高校生年代まで毎月もらえる                            │
│        [毎月 お金 +15000]                                  │  ← 効果チップ
│        3歳未満 月15,000 → 3歳〜高校生年代 月10,000          │  ← 支給表の行
│        今：月15,000（次回 0歳3月1日）     [カレンダーに追加] │  ← 期限の行
└───────────────────────────────────────────────────────────┘
```

---

## 2. ファイル

| ファイル | 役割（これだけをやる） |
|---|---|
| `Assets/Yamaguchi/Scripts/UI/SupportScheduleText.cs` | カードに出す **文字列を作るだけ**（static）。`BadgeLabel` / `DeadlineLine` / `TierLine` / `IsUrgent` |
| `Assets/Yamaguchi/Scripts/UI/SupportSchemeCard.cs` | カード1枚の **表示とボタン**。`Bind(scheme)` / `Refresh()` / `OnPrimaryClicked` |
| `Assets/Yamaguchi/Prefabs/UI/SupportCard.prefab` | カードの見た目。`SupportSchemeCard` 付き |
| `Assets/Maruyama/Prefabs/Events/EffectChip.prefab` | 効果チップ（丸山さん作。**変更せずに参照だけ**） |

文字列づくりとカードの表示を分けているのは、**カレンダーでも同じ文（「申請期限：○月○日まで（あと○日）」）を使えるようにするため**。カレンダー側は `SupportScheduleText.DeadlineLine(scheme)` を呼ぶだけでよい。

---

## 3. プレハブの階層（SupportCard.prefab）

カード全体は 666 × 136。`LayoutElement.preferredHeight = 136`（Content の `VerticalLayoutGroup` が高さを決める → [ui-scroll-patterns.md](ui-scroll-patterns.md) 1章）。

```
SupportCard          Image（カード背景）＋ LayoutElement(ph=136) ＋ SupportSchemeCard
├ icon               Image 56×56（左・中央）。アイコン未設定の制度は非表示
├ title              TMP 20pt   上から 6px  / 左 84px・右 170px あけて横に伸ばす
├ desc               TMP 14pt   上から 32px
├ chip_row           HorizontalLayoutGroup（spacing 6、Control Child Size・Force Expand すべて OFF）上から 56px・高さ 32
├ tier_text          TMP 13pt   上から 92px（支給表が無い制度は非表示）
├ deadline_text      TMP 13pt   上から 110px
├ badge              Image 130×24（右上）
│  └ Label           TMP 13pt 白・中央
├ btn_apply          Button 130×36（右・中央）
│  └ Label           TMP 16pt
└ btn_calendar       Button 130×26（右下）
   └ Label           TMP 12pt「カレンダーに追加」
```

- 文字はすべて `JPFont`。**Overflow は既定のまま**（Ellipsis にすると何も描画されない → ui-scroll-patterns.md のハマりどころ）。
- 文字の行は右 170px をあけてある（バッジとボタンの列）。**1行に収まらない長さの文は作らない**（4章の判断を参照）。
- 効果チップ（140×32）は丸山さんのプレハブそのまま。カードが 120 では入らなかったので 136 にした。

---

## 4. 表示のルール（状態ごと）

support-apply.md 7.2 の表をそのまま実装した。色はプレハブの Inspector で変えられる。

| 状態 | バッジ（色） | 期限の行 | 主ボタン | カレンダー |
|---|---|---|---|---|
| Locked（開始日前） | まだ対象外（グレー） | 6歳4月1日から申請できます | なし | あり |
| Locked（条件未達） | 条件を満たしていません（グレー） | 条件：子どもが…／年収400万円以下 | なし | あり |
| Available | 申請できる（オレンジ） | 申請期限：0歳3月31日まで（あと89日）／いつでも申請できます | 申請する | あり |
| Received | 受け取り済み（緑） | 0歳1月1日に受け取りました（+500,000円） | なし | なし |
| Active | 受給中（緑） | 今：月15,000（次回 0歳3月1日）。毎年の手続きがあれば「次の手続き期限」も | 更新期間中だけ「更新する」 | あり |
| Suspended | 支給停止（赤オレンジ） | 手続きすると再開（止まっている分 ○か月）… | 手続きする | あり |
| Expired | 期限切れ（赤） | 0歳3月31日で締め切られました | なし | なし |
| Lost | 受給資格なし（赤） | 手続きがなかったため… | なし | なし |
| Ended | 受給終了（グレー） | 18歳3月31日で受給が終わりました | なし | なし |

### 決めたこと（2026-10-07）

- **効果チップの「毎月」**：`scheme.effects` は「申請したときに1回だけ起きること」（手間のストレスなど）なので「毎月」を付けない。「毎月」は **支給表の最初の段** の `monthlyEffects` のチップにだけ付ける。段ごとの金額の違いは支給表の行で見せる。
- **受給中の期限の行は短く**：最初は「今：月15,000（次回…）／3歳1月1日から月10,000／18歳3月31日まで」と出していたが、1行に収まらずボタンに重なった。次の段・受給の終わりは **支給表の行と同じ内容** なので、期限の行では「今の額と次回の支給日」だけにした。
- **メリット・デメリットの文字チップ**（support-apply.md 7.1 の図の緑・赤の文字）は今回は出さない。カードの高さが足りないため。出すなら制度の詳細を開く形にする（未定）。
- **絵文字は使わない**：「📅」は `JPFont` に無いので、ボタンは文字だけ。
- **主ボタンの処理**：`OnPrimaryClicked` を購読している部品がいなければ、カードが `SupportManager.Apply / Renew / Resume` を直接呼ぶ。ステップ3で確認ダイアログを作ったら、ダイアログ側が購読して差し替える（カードは変更しない）。
- **シーンの仮カード**（`card_01`〜`card_08`）はステップ3前半で削除した。カードは実行時にプレハブから並べる（8章）。

---

## 5. 使い方（コード）

```csharp
// 1枚作って表示する（ステップ3では SupportPanelController がこれをやる）
var card = Instantiate(supportCardPrefab, content).GetComponent<SupportSchemeCard>();
card.Bind(scheme);                       // 以後、状態や日付が変われば勝手に更新される

// 確認ダイアログを挟みたいとき（ステップ3）
card.OnPrimaryClicked += (scheme, action) => dialog.Open(scheme, action);

// 文字列だけ欲しいとき（カレンダーなど）
string line = SupportScheduleText.DeadlineLine(scheme);   // 「申請期限：0歳3月31日まで（あと89日）」
bool urgent = SupportScheduleText.IsUrgent(scheme, 7);    // あと7日以下か
```

---

## 6. 確認したこと（2026-10-07・Main_test2）

Play 中に `SupportManager` の4制度でカードを作って確認した。

1. 開始時：一時金・支援給付は「申請できる」、期限と「あと730日／あと89日」が出る。就学援助は「まだ対象外」「6歳4月1日から申請できます」。
2. 一時金の［申請する］を押す → お金 +500,000、バッジが「受け取り済み」、ボタンが消える。
3. 児童手当の［申請する］を押す → 「受給中」、期限の行が「今：月15,000（次回 0歳3月1日）」。
4. 0歳4月まで進める → 支援給付が「期限切れ」「0歳3月31日で締め切られました」、カレンダーボタンが消える。
5. Console にエラーなし。

---

## 7. 残っていること

| 内容 | いつ |
|---|---|
| ~~カードを制度アセットから並べる・タブ切替・仮カードの削除~~ | **完了（8章）** |
| サポートボタン（画面下）に「申請できる」数のバッジ | ステップ3後半 |
| 確認ダイアログ（`OnPrimaryClicked` を購読） | ステップ3 |
| ［カレンダーに追加］を `CalendarUI.OpenAddPlan` につなぐ | ステップ5（草尾さん K5） |
| 効果チップのお金に桁区切りが無い（「+500000」）。`EffectChipUI` は丸山さんのファイルなので、直すなら丸山さんに依頼 | 相談 |
| 制度アイコンの画像 | 素材ができたら `SupportScheme.icon` に入れるだけ |

---

## 8. サポート画面（SupportPanelController）— ステップ3前半

`Canvas/supports` に付いている。**カードを並べる係**。カード1枚の中身には触らない（それは `SupportSchemeCard`）。

### 8.1 やること

| いつ | 何をするか |
|---|---|
| 画面が開いた（`OnEnable`） | 時間を止める（`GameClock.I.PushPause()`）。選んでいるタブの制度を `SupportManager.GetByCategory` で読み、`SupportCard` プレハブを並べて `Bind` する |
| タブを押した | その種類の制度だけに並べ直す。選んだタブの色を変える |
| 制度の状態が変わった（`OnStateChanged`） | タブの数（「政府（2）」＝申請できる制度の数）だけ直す。カードは自分で更新する |
| 画面が閉じた（`OnDisable`） | 時間を戻す（`PopPause()`）。×ボタン・どこから閉じても必ず呼ばれる |

開く・閉じるのは今までどおり、下の［サポート］ボタン（`fadein`）と×ボタン（`fadeout`）の `SetActive`。コントローラーはそれに反応するだけなので、ボタン側は何も変えていない。

### 8.2 Inspector の設定（`Canvas/supports`）

| 項目 | 入れたもの |
|---|---|
| Card Prefab | `Assets/Yamaguchi/Prefabs/UI/SupportCard.prefab` |
| Content | `supports_images/scheme_scroll/Viewport/Content` |
| Scroll | `supports_images/scheme_scroll` |
| Tabs[0] | Government ／ `goverment` ボタン ／ その子の TMP |
| Tabs[1] | Municipality ／ `local_goverment` ボタン ／ その子の TMP |
| Tabs[2] | Company ／ `company` ボタン ／ その子の TMP |
| Start Category | Government |

タブを増やすとき（「アプリ」など）は、ボタンを置いて Tabs に1行足すだけ。コードは変えない。

### 8.3 外から呼ぶ窓口

```csharp
panel.Open(SupportCategory.Municipality);   // そのタブで開く
panel.Open("child_allowance");              // その制度のタブを開き、カードまでスクロール（閉じていれば開く）
```

`Open(schemeId)` はカレンダーのリマインダーの［サポートで申請］から呼ぶ（calendar-system.md 2.4、草尾さん K6）。

### 8.4 気をつけたところ

- **`PushPause` と `PopPause` は必ずペア**：自分が止めたかを `pausing` で覚え、止めた時だけ戻す。シーン開始時に `GameClock` より先に `OnEnable` が来ても、`Start` でもう一度止めにいく。
- **仮カードも含めて消してから並べる**：`Destroy` はフレームの最後に消えるので、先に `SetActive(false)` にしてレイアウトの計算から外している。
- **ボタンの onClick は実行時に配線**（`AddListener`）。シーンに保存されないので、Inspector の On Click() 欄は空のままでよい。
- `supports` はシーンでは表示された状態で保存されているので、**Play 直後はサポート画面が開いていて時間が止まっている**。×で閉じると時間が進む。最初は閉じておきたい場合は、シーンで `supports` を非表示にして保存する。

### 8.5 確認したこと（2026-10-07）

1. Play → 政府タブにカード3枚（一時金・支援給付・児童手当）、タブは「政府（3）」。
2. 地方自治体タブ → 就学援助1枚。会社タブ → 0枚（制度アセットがまだ無い）。
3. 一時金の［申請する］→ お金 +500,000、タブが「政府（2）」に減る。
4. `Open("child_allowance")` → 児童手当のカードまでスクロール。
5. ×で閉じる → 時間が戻る（停止数 0）。［サポート］で開く → 止まる（停止数 1）、カードが並び直る。
6. Console にエラー・警告なし。
