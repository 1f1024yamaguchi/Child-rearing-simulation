---
title: 支援制度の足し方と項目の意味
updated: 2026-10-28
status: 実装済み
related: [docs/design/support-apply.md, docs/code/ui-support-card.md]
---

# 支援制度の足し方と項目の意味

> サポート画面に制度（カード）を1枚足す手順と、制度アセットの各項目の意味。**コードは書かない。**
> 仕様の理由は [support-apply.md](../design/support-apply.md)、カードの作りは [ui-support-card.md](ui-support-card.md)。

---

## 1. 足し方（4ステップ）

1. **アセットを作る**
   `Assets/Yamaguchi/Data/Supports/` で右クリック → **Create → 子育て → 支援制度**。
   既存のアセットを Ctrl+D で複製して書き換えると早い（複製したら **Id を必ず変える**）。
2. **Inspector で項目を入れる**（2〜4章）
3. **登録する**
   Hierarchy の `GameManager/SupportManager` → **Schemes** の「＋」で1行足し、アセットをドラッグ。リストの順番＝カードの並び順。
   シーンを保存（Ctrl+S）。
4. **確認する**
   Play → ［サポート］→ Category のタブにカードが出ていれば完了。

---

## 2. 日付の入れ方（先に読む）

ゲームの日付はすべて **「子どもの年齢」** で数える。カレンダーの西暦ではない。

| 欄 | 意味 | 入れられる範囲 |
|---|---|---|
| **Year** | 子どもの**歳** | 0〜19 |
| **Month** | 月 | 1〜12 |
| **Day** | 日 | 1〜その月の日数（2月は28日まで） |

- **0歳1月1日 ＝ 生まれた日**（ゲーム開始日）。誕生日は毎年1月1日。
- **3歳1月1日 ＝ 3歳になった日**。「3歳未満」は **2歳12月31日まで**。
- 1年は365日（うるう年なし）。ゲームでは **1日＝1秒** で進む。0歳3月31日までなら約90秒。
- 「その日まで」はその日を含む。期限が 0歳3月31日 なら、3月31日のうちは申請できて、4月1日に期限切れになる。
- 範囲の外の値（2月30日など）は、入力した時点で自動で直る。

**毎年 ○月○日（Month Day）** は Year が無く、Month と Day だけ。毎年くり返す期限に使う。

---

## 3. 制度アセットの項目

### 表示

| 項目 | 意味 | 例 |
|---|---|---|
| **Id** | プログラムが制度を見分ける名前。**必須**・英数字と _ ・ほかと重ねない・**あとから変えない**（セーブがこの名前で記録するため） | `child_allowance` |
| Title | カードの名前 | 児童手当 |
| Description | カードの説明（1行） | 高校生年代まで毎月もらえる |
| Icon | カード左のアイコン。空なら表示しない | （画像） |
| Category | どのタブに出すか。Government＝政府／Municipality＝地方自治体／Company＝会社／App＝アプリ（**App のタブは未作成**） | Government |
| Merits / Demerits | メリット・デメリットの文。**今はカードに表示していない**（将来の詳細画面用） | 申請が遅れた月の分はもらえない |

### 効果

| 項目 | 意味 |
|---|---|
| **Effect Timing** | **OnApply**＝申請したときに1回だけ効果（一時金など）。**Monthly**＝受給中は毎月効果（手当など）。毎月の分は **申請した月の翌月から**（遅れた月はさかのぼらない） |
| **Effects** | 申請したときに1回だけ起きること。OnApply なら効果の本体（お金 +500,000 など）。Monthly なら手間のストレスなど（空でよい） |

Effects の1行（Effect）の中身：

| 欄 | 意味 | 例 |
|---|---|---|
| Kind | 何が変わるか。Money＝お金／Stress＝ストレス／Health＝体調／Grades＝成績／Hobby＝趣味 など | Money |
| Target | 誰に。Household＝世帯（お金はこれ）／Mother／Father／Child／Parents＝両親／Everyone＝家族全員 | Household |
| Value | 増減の量。マイナスなら減る。お金は円 | 500000 |
| Duration | **Instant のままにする**（毎月の効果は支給表で作る。Days・ThisMonth は未対応） | Instant |
| Days | Duration が Days のときだけ使う。今は使わない | 0 |

### 申請条件

| 項目 | 意味 |
|---|---|
| **Apply Requirements** | 申請できる条件。条件アセットをドラッグして並べる。**全部満たしたら**申請できる。空なら条件なし |

満たしていない間、カードは「条件を満たしていません」になり、満たしていない条件の説明が出る。

### 毎月の支給（Effect Timing が Monthly のとき）

| 項目 | 意味 |
|---|---|
| **Monthly Tiers** | 支給表。「何歳から何歳まで、毎月何をもらうか」を1段ずつ並べる |

1段（Payment Tier）の中身：

| 欄 | 意味 | 例 |
|---|---|---|
| Label | カードに出す段の名前。空なら「0〜2歳」のように自動 | 3歳未満 |
| From Age | この日から（含む）。Year・Month・Day は2章 | 0歳1月1日 |
| To Age | この日まで（含む） | 2歳12月31日 |
| Monthly Effects | この段のあいだ毎月起きること（Effect の形は上と同じ） | お金 +15,000 / Household |
| Requirements | この段に当てはまる追加条件（空＝なし）。所得で額を変えるときに使う | （なし） |

- 毎月、**上から順に**「期間に入っていて、追加条件も満たす」最初の段で払う。当てはまる段が無い月は 0 円（状態は受給中のまま）。
- **いちばん最後の To Age を過ぎると「受給終了」**。申請していなくても受給終了になる。
- 所得で額を変える例：同じ年齢の段を2つ並べ、上に「年収○円以下」の条件つきで多い額、下に条件なしで少ない額。

### 支出の肩代わり（無償化など）

`covers` は現金を給付せず、受給中の制度が対象の月次支出を減らす設定。支出項目は `Assets/Yamaguchi/Data/Expenses/` の `ExpenseItem` アセットを指定する。

| 欄 | 意味 |
|---|---|
| Expense | 対象にする支出項目（例：保育料、給食費、高校授業料） |
| Type | Full＝全額／Rate＝割合／UpTo＝指定額まで |
| Value | Rate は 0〜1、UpTo は円。Full では使わない |
| From Age / To Age | 肩代わりの対象期間（両端を含む） |

- 対象の支出アセットに年齢別の段がある場合、その支出が発生する期間に合わせて設定する。
- 月次支出の通常額から、該当する肩代わり額を差し引く。複数制度の対象になっても、肩代わり合計は支出額を超えない。
- 制度が受給中（Active）の場合に限り有効。申請した月は対象外で、翌月分から適用する。毎年手続きが期限切れになり支給停止（Suspended）すると、再開まで肩代わりしない。
- 家計簿には、実際に払った額と肩代わりされた額を同じ支出行に記録する。全額肩代わりの場合も、支援額が見えるよう行を残す。

### 働き方連動・給与連動（会社制度）

会社制度では、支援制度アセットに働き方と給与連動給付を設定する。

| 欄 | 意味 |
|---|---|
| Changes Work Mode | 申請で親の働き方を変える制度か。育休・時短勤務で有効 |
| Work Target | 対象の親（Mother / Father） |
| Work Mode | 申請後の状態。ParentalLeave は日給0・バランス0、ShortTime は日給が通常の80%・バランス0.2 |
| Work Mode Until | その日まで働き方を維持し、翌日 Normal に戻る日。カードの［復帰する］／［時短をやめる］でも途中終了できる |
| Wage Target | WorkTarget＝指定した親、BothParents＝両親の給与を合算（出生後休業支援給付金用） |
| Wage Linked Tiers | 休業・給付開始からの日数ごとの率。fromDay / toDay は両端を含み、toDay=-1 は終了日なし |
| Rate / Basis | Rate は給付率。SalaryBeforeLeave は休業開始前の月給÷30、CurrentWage は現在の日給を日額の基礎にする |
| Requires Work Mode / Required Work Mode | 申請・受給に必要な勤務状態。時短給付では ShortTime を指定 |
| Wage Benefit Until | 給与連動の月次給付を受けられる最後の日。期限を設けない場合は0歳1月1日（通算日数0）のままにする |

- 日割り給付は日次に積み上がり、月替わりにまとめて支給される。OnApply の給与連動給付も申請時点で対象期間を計算し、次の月替わりに支給する。
- 産後の MaternityLeave は SupportManager が自動設定する。母用の ParentalLeave は産休中には申請できず、産休終了後に申請できる。
- 会社制度は `Main_test2` の `SupportManager.Schemes` に登録する。新規カードも登録後はカテゴリ別のサポート一覧に表示される。

### 申請できる期間

| 項目 | 意味 |
|---|---|
| **Start** | この日から申請できる。それより前は「まだ対象外」 |
| **Deadline Type** | 期限の種類。**None**＝期限なし／**Once**＝1回だけ（Deadline の日まで）／**Yearly**＝毎年の手続き（Yearly の日まで） |
| **Deadline** | Once のときの期限日（含む）。過ぎると「期限切れ」で、もう申請できない |
| **Yearly** | Yearly のときの毎年の期限（月・日）。受給中、毎年この日までに［更新する］が要る |
| **Lapse Years** | Yearly で、期限を過ぎて「支給停止」になってから何年手続きしないと「受給資格なし」になるか（既定 2）。停止中に［手続きする］と再開し、止まっていた月の分もまとめてもらえる |
| **Renew Window Days** | Yearly で、期限日の何日前から［更新する］を押せるか（既定 31） |

> ※ 毎年の手続きの最初の期限：申請した歳の期限まで Renew Window Days（31日）より長く残っていればその歳の期限、残りが短い・過ぎていれば次の歳の期限（2026-10-08 決定 → support-apply.md 2.3）。

---

## 4. 申請条件のアセット

`Assets/Yamaguchi/Data/Supports/Requirements/` で右クリック → **Create → 子育て → 申請条件 → …**。1つの条件アセットを複数の制度で使い回してよい。

| 種類 | 欄 | 意味 | カードの表示 |
|---|---|---|---|
| 子どもの年齢 | Min Age / Max Age | 子どもの年齢がこの間（両端を含む）。Year・Month・Day は2章 | 子どもが 3歳〜5歳 |
| 世帯年収 | Max Annual Income | この額**以下**なら満たす（円） | 年収 400万円以下 |
| 〃 | Min Annual Income | この額**以上**なら満たす（円）。0 なら下限なし | 年収 200万〜400万円 |

- 年収は **両親が実際に受け取った給料（額面）の直近12か月の合計**。ワークライフバランスで変わる。
- 新しい種類の条件（ひとり親か、など）を足すときはコードが要る → support-apply.md 4.4。

---

## 5. 例

### その場で1回もらう（出産育児一時金）

| 項目 | 値 |
|---|---|
| Id / Title | `birth_lump_sum` / 出産育児一時金 |
| Category | Government |
| Effect Timing | OnApply |
| Effects | Money / Household / 500000 |
| Start | 0歳1月1日 |
| Deadline Type / Deadline | Once / 2歳1月1日 |

### 毎月もらう・年齢で額が変わる（児童手当）

| 項目 | 値 |
|---|---|
| Id / Title | `child_allowance` / 児童手当 |
| Effect Timing | Monthly |
| Effects | （空） |
| Monthly Tiers [0] | 3歳未満 / 0歳1月1日〜2歳12月31日 / Money +15000 |
| Monthly Tiers [1] | 3歳〜高校生年代 / 3歳1月1日〜18歳3月31日 / Money +10000 |
| Start / Deadline Type | 0歳1月1日 / None |

### 条件つき・毎年手続き・支出を肩代わり（就学援助）

| 項目 | 値 |
|---|---|
| Id / Title / Category | `school_aid` / 就学援助 / Municipality |
| Effect Timing | Monthly |
| Apply Requirements | 子どもの年齢（6歳4月1日〜15歳3月31日）、世帯年収（400万以下） |
| Monthly Tiers | （空） |
| Covers [0] | 給食費 / Full / 6歳4月1日〜15歳3月31日 |
| Covers [1] | 学用品など（中学校） / Full / 12歳4月1日〜15歳3月31日 |
| Start | 6歳4月1日 |
| Deadline Type / Yearly | Yearly / 4月30日 |
| Lapse Years | 2 |

---

## 6. うまくいかないとき

| 症状 | 原因 |
|---|---|
| カードが出ない | Schemes に登録していない／Id が空（Console に警告）／Category のタブが無い（App） |
| 2枚の制度の状態が混ざる | Id が重なっている（Console に警告） |
| すぐ「受給終了」になる | Monthly Tiers の To Age が過去になっている |
| 申請しても毎月もらえない | 申請した月はもらえない（翌月から）。To Age を過ぎていないか |
| 「条件を満たしていません」のまま | Apply Requirements のどれかを満たしていない（カードに出る説明を見る） |
