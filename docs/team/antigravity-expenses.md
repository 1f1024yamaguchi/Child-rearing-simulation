---
title: 支出と家計簿の実装法と Antigravity への依頼文
updated: 2026-10-28
status: 進行中（E2 の確認待ち）
related: [docs/design/expenses.md, docs/team/agreements-needed.md, docs/design/support-apply.md, docs/design/save-system.md, docs/decisions/0015-real-money-and-expenses.md, docs/design/result-scene.md]
---

# 支出と家計簿の実装法と Antigravity への依頼文

> 仕様は [../design/expenses.md](../design/expenses.md)。この文書は **「どう作るか」と「Antigravity にどう頼むか」** を書く。
> 依頼は **6回に分けて** 頼み、1回ごとに Unity で確認してから次へ進む。
> [agreements-needed.md](agreements-needed.md) の M1・M2（丸山さん）・A1（有坂さん）は **山口が自分で行う**ことになった（2026-10-21）。依頼E1b で頼む。作業の前に2人へ一言伝え、pull してから始める。

---

## 1. 決まっていること（おさらい）

| 項目 | 決定 |
|---|---|
| 金額 | 実際の円（決定 0015） |
| 給料 | **手取り**で入る。手取り率 0.8（たたき台）。年収の判定（IncomeHistory）は額面のまま（5.5） |
| 家計簿への載せ方 | **案A**：`EffectApplier.Apply(effects, LedgerSource)` で「何のお金か」を渡すと、お金が動いたときに家計簿に載る（5.6） |
| 支出 | `ExpenseItem`（アセット1つ＝支出1つ）を年齢の段で引く。当てはまる段がなければ 0 円（3章） |
| 無償化 | お金を配るのではなく、**支出を肩代わりする**。受給中の制度だけ（4章） |
| お金が足りないとき | 0 円止めを外す。**赤字が2か月続いたら家計破綻でゲームオーバー**（5.7 案1） |
| 終わり方 | `OnGameOver` を理由付きの `OnGameEnded(GameEndReason)` に移す（5.7） |
| 費目と金額 | 6章の表は **たたき台**。メンバーの調査（[../research/expenses.md](../research/expenses.md)）で後から変える。アセットの値を変えるだけで済む形にする |

---

## 2. 実装の全体像

### 2.1 部品と役割

```
（家計簿：依頼E1）
ExpenseCategory   … 支出の種類 ＋ 入ってくるお金の種類（下の 2.3）
LedgerSource      … 「何のお金か」（名前＋種類）。EffectApplier に渡す
LedgerLine        … 家計簿の1行
Ledger            … 家計簿の本体。IncomeHistory と同じ「シーンに置かないクラス」（Ledger.I）
LedgerSaveParticipant … 家計簿のセーブ（SaveKey "ledger"）

（月の精算の順番：依頼E1）
MonthTicker.OnMonthSettled … OnMonthChanged の処理が全部終わった後に出す（赤字の判定はここ）

（支出：依頼E2）
ExpenseItem / CostTier / ExpenseRuntime / ExpenseManager

（無償化：依頼E3）
ExpenseCover（SupportScheme に持たせる）

（お金が足りないとき・終わり方：依頼E4）
GameState のマイナス許可・OnGameEnded(GameEndReason)・BankruptcyWatcher・赤字の警告

（画面：依頼E5）
家計簿の画面（月ごと・年ごと）
```

### 2.2 お金が動く場所と、家計簿への載り方

| お金が動く場所 | 今のコード | 家計簿に載せる方法 | 誰が |
|---|---|---|---|
| 給料 | `HouseholdIncome.Payday` → `GameState.AddMoney` | Payday の中で `Ledger.Record` を直接呼ぶ（額面・税金の2行） | 山口（E1） |
| 支援制度の支給 | `SupportManager` → `EffectApplier.Apply(tier.monthlyEffects)` | 呼び方に `LedgerSource` を足す | 山口（E2） |
| 支出 | これから | 最初から `LedgerSource` 付きで呼ぶ | 山口（E2） |
| 食事 | `MealSystemManager` 201行目 → `EffectApplier.Apply` | 呼び方に `LedgerSource` を足す | 山口（E1b・A1） |
| イベント | `EventChoiceButtonUI` 56行目 → `EffectApplier.Apply` | 選択肢に種類を持たせ、イベント名と一緒に渡す（2.6） | 山口（E1b・M2） |
| 状態の毎日の効果 | `PersonStatus.ApplyDailyEffect` → `GameState.AddMoney` | 今は載せない（使っている状態が無いため）。使い始めたら直す | ― |
| `EffectApplier` 本体 | 丸山さんのフォルダ | `Apply(effects, LedgerSource)` を足し、Money のときに `Ledger.Record` | 山口（E1b・M1） |

- アセンブリ定義（asmdef）が無いので、丸山さんの `Maruyama.Core` からも Core の `Ledger` をそのまま呼べる。
- **今の `Apply(effects)` は残す**（家計簿には「その他」として載る）。呼び方を変えていないところがあっても何も壊れない。
- 食事の `MealSelectionDialogUI` 155行目はストレスなどの効果（`selectEffects`）でお金は動かないので、変えなくてよい。

### 2.3 家計簿の行の種類（設計からの追加）

`expenses.md` の `ExpenseCategory` は支出の種類しかないが、家計簿には給料・税金・支援も載る。**同じ enum の後ろに足す**（家計簿の行が1種類の enum で済むように）。

```
住居, 光熱費, 通信, 食費, 保育, 学校, 塾習い事, 医療, その他,   … 支出（expenses.md 3.1）
給料, 税金社会保険, 支援制度                                    … 入ってくる側（追加）
```

**「子育てにかかったお金」に数える種類**（リザルトの合計に使う）：保育・学校・塾習い事・食費・医療。
生活の固定費（住居・光熱費・通信）は数えない（子どもがいなくてもかかるため。expenses.md 7章）。
→ `Ledger` に `IsChildCost(ExpenseCategory)` を1つ持たせ、判定をここにまとめる（後から変えやすいように）。

### 2.4 月の精算の順番（大事）

月が変わると、給料（HouseholdIncome）・支援の支給（SupportManager）・支出（ExpenseManager）・食費（有坂さん）がそれぞれ `OnMonthChanged` を購読して動く。**購読した順番は保証されない**ので、ここで赤字を判定すると、給料が入る前に「赤字」と言ってしまうことがある。

→ `MonthTicker` に **`OnMonthSettled`** を足す。`OnMonthChanged` を出し終えた直後に同じ月で出す。
赤字の判定（BankruptcyWatcher）と家計簿の月の締め（Ledger）はこちらを購読する。

### 2.5 Ledger が持つもの

| 中身 | 用途 | セーブ |
|---|---|---|
| 直近24か月の行（`LedgerLine`） | 月の家計簿の画面 | する |
| 種類ごとの **ゲーム全体の合計** | リザルトの「子育てにかかったお金」「受け取った支援の合計」 | する |
| 子どもの年齢の年ごとの合計（収入・支出・支援で助かった額） | 誕生日のまとめ | する |

24か月より古い行は消すが、合計は残る。

### 2.6 イベントのお金の載せ方（M2）

今の作り：

```
EventCardUI.Bind(EventData data)          … イベント名（data.title）を知っている
  └ ChoiceBlockUI.Setup(EventChoice)      … イベント名は渡されていない
      └ EventChoiceButtonUI.Setup(EventChoice)
          └ 押したら EffectApplier.Apply(choice.effects)   ← 56行目
```

- ボタンは **選択肢の文字（`choice.label`）しか知らない**。イベント名も、家計簿の種類も届いていない。
- 種類は **選択肢ごと** に持つ。同じ風邪のイベントでも「病院に行く」は医療、「仕事を休む」はお金が動かない、のように選択肢で変わるため。
- 家計簿の名前は「イベント名：選択肢」（例「みらいが風邪をひいた：病院に行く」）。
- お金が動くイベントは今2つ（`Event_Demerit_ChildSick`・`Event_Merit_Daycare`）。この2つのアセットに種類を入れる。

---

## 3. 依頼文

各依頼の最後に必ず「Unity の MCP ツール（unityMCP）は使わないでください」を入れる（クレジットの消費が大きいため。Unity での確認は人間が行う）。

### 依頼E1：家計簿の土台と給料の手取り（画面なし）

```
docs/design/expenses.md の 5章・5.5・5.6 と docs/team/antigravity-expenses.md の 2章 を読んでから作業してください。
置き場所は Assets/Yamaguchi/Scripts/Core/Ledger/（MonthTicker・HouseholdIncome は今の場所のまま変更）。

1. ExpenseCategory.cs：enum。expenses.md 3.1 の種類（住居, 光熱費, 通信, 食費, 保育, 学校, 塾習い事, 医療, その他）の後ろに
   給料, 税金社会保険, 支援制度 を足す。値の順番は後から変えない（セーブに数字で残るため）。
2. LedgerSource.cs：[Serializable] のクラス。string name、ExpenseCategory category。コンストラクタ (name, category)。
   static readonly の LedgerSource.Unknown（"その他", その他）を持つ。
3. LedgerLine.cs：expenses.md 5章のとおり（monthIndex, source, category, amount, covered）。
4. Ledger.cs：IncomeHistory と同じ形（シーンに置かない。Ledger.I）。
   - Record(LedgerSource source, int amount, int covered = 0)：今の月（GameState.AgeDays から歳*12+月-1）で1行足す。
     同じ月・同じ名前・同じ種類の行があれば足し合わせる（食費が毎日引かれても1行にまとまるように）。
   - 種類ごとのゲーム全体の合計、子の年齢の年ごとの合計（収入／支出／支援で助かった額）を更新する。
   - 24か月より古い行は消す（合計は残す）。
   - 読み取り：LinesOfMonth(int monthIndex)、TotalOf(ExpenseCategory)、TotalChildCost（IsChildCost の種類の支出の合計。正の数で返す）、
     TotalSupport（支援制度の入金の合計 ＋ covered の合計）、YearSummary(int age)。
   - static bool IsChildCost(ExpenseCategory c)：保育・学校・塾習い事・食費・医療 のとき true。
   - event Action<LedgerLine> OnRecorded。
   - ToSave() / Restore(...) と Clear()（最初から始めるとき用）。
5. LedgerSaveParticipant.cs：IncomeSaveParticipant と同じ形。SaveKey "ledger"、RestoreOrder 25。
6. MonthTicker.cs：event Action<int,int> OnMonthSettled を足し、OnMonthChanged を出した直後に同じ (歳, 月) で出す。
   Resync では出さない（今の OnMonthChanged と同じ）。
7. HouseholdIncome.cs：
   - [Range(0.5f, 1f)] takeHomeRate = 0.8f を足す。
   - Payday：額面 gross = 母 + 父。手取り net = RoundToInt(gross × takeHomeRate)。
     GameState.I.AddMoney(net)。IncomeHistory.I.Record(gross)（額面のまま）。
     家計簿に「給料（母）」「給料（父）」を額面で（0 円の人は書かない）、「税金・社会保険」を −(gross − net) で Record。
   - 給料日に通知を1つだけ出す：「給料が入りました（手取り +○○円）」。通知の出し方は既存の SupportToast と同じ仕組みがあればそれを使い、無ければログだけにして報告に書く。
8. 確認用 [ContextMenu]（GameManager に付く部品のどれか、または LedgerDebugMenu.cs を新しく作る）：
   「Debug/家計簿：今月と先月をログ」「Debug/家計簿：合計をログ」

守ること：
- Maruyama・Arisaka・Kusao のフォルダのファイルは変更しない（EffectApplier・イベント・食費の変更は依頼E1b で行う）。
- GameState の 0 円止めはこの依頼では外さない（依頼E4）。
- Unity の MCP ツール（unityMCP）は使わないでください。終わったら、Unity で確認してほしいこと、シーンに置くもの・Inspector の設定を箇条書きで教えてください。
```

**確認（依頼E1）**
1. Play で月をまたぐ → 所持金が額面の 0.8 倍だけ増え、「給料が入りました」が1回出る。
2. 「Debug/家計簿：今月と先月をログ」→ 給料（母）・給料（父）・税金・社会保険の3行が出る。
3. サポート画面の年収の表示が **額面** のまま（手取りになっていない）。
4. セーブ → ロードで家計簿の行と合計が残る。

---

### 依頼E1b：イベント・食事のお金を家計簿につなぐ（M1・M2・A1）

**頼む前に**：丸山さん・有坂さんに「家計簿のために EffectApplier・イベントの選択肢・食費の1行を変えます」と伝える。最新を pull してから頼み、終わったらこの変更だけでコミットする。

```
docs/design/expenses.md の 5.6 と docs/team/antigravity-expenses.md の 2.2・2.6 を読んでから作業してください。
この依頼に限り、下に挙げた丸山さん・有坂さんのファイルを変更してよい（本人の了承済み）。それ以外の他の人のファイルは変更しない。

【M1：EffectApplier】Assets/Maruyama/Scripts/Core/EffectApplier.cs
1. public static void Apply(IEnumerable<Effect> effects, LedgerSource source) を足す。
   中身は今の Apply と同じだが、Money の効果を適用したら Ledger.I.Record(source ?? LedgerSource.Unknown, RoundToInt(e.value)) を呼ぶ
   （所持金の差ではなく効果の額で記録する）。
   GameState が無いテスト環境では Record しない。
2. 今の Apply(IEnumerable<Effect> effects) は、Apply(effects, LedgerSource.Unknown) を呼ぶだけにする（動きは変えない）。
3. お金以外の効果・ログの出し方は変えない。ApplySingleEffect に source を渡す形にしてよい。

【M2：イベントの選択肢】Assets/Maruyama/Scripts/
4. Events/EventChoice.cs：public ExpenseCategory ledgerCategory = ExpenseCategory.その他; を足す
   （Inspector で選べるように。既存のアセットは その他 のままになる）。
5. UI/EventCardUI.cs：選択肢ブロックを作るとき、data.title も渡す。
6. UI/ChoiceBlockUI.cs：Setup(EventChoice choice, string eventTitle, Action onChosen) にし、ボタンへ eventTitle を渡す。
7. UI/EventChoiceButtonUI.cs：Setup で eventTitle を受け取って持っておく。56行目を
   EffectApplier.Apply(currentChoice.effects, new LedgerSource($"{eventTitle}：{currentChoice.label}", currentChoice.ledgerCategory)) にする。
   eventTitle が空なら選択肢の文字だけにする。
8. Setup の呼び出し元が他にあれば（Editor/MaruyamaTestRunner.cs など）コンパイルが通るように合わせ、報告に書く。
   古い Setup(choice, onChosen) を残す方が安全なら、残して eventTitle を空で呼ぶ形にしてよい。

【A1：食費】Assets/Arisaka/MealSystemManager.cs
9. 201行目を EffectApplier.Apply(new[] { costEffect }, new LedgerSource($"食費（{plan.title}）", ExpenseCategory.食費)) にする。
   MealSelectionDialogUI.cs 155行目はお金が動かないので変えない。

守ること：
- 上に挙げたファイル以外の、他の人のファイルは変更しない。イベントのアセット（.asset）は触らない（人間が Inspector で入れる）。
- イベント・食事の動き（お金の額・ストレス・表示）は変えない。変わるのは家計簿に載ることだけ。
- Unity の MCP ツール（unityMCP）は使わないでください。終わったら、変更したファイルと行、Unity で確認してほしいことを箇条書きで教えてください。
```

**確認（依頼E1b）**
1. `Event_Demerit_ChildSick` の「病院に行く」系の選択肢の家計簿の種類を **医療** に、`Event_Merit_Daycare` は中身に合わせて（保育など）Inspector で入れる。
2. Play → イベントで病院を選ぶ → 「Debug/家計簿：今月と先月をログ」に「みらいが風邪をひいた：病院に行く／医療」の行が出る。
3. 食事を選んで数日進める → 「食費（普通）／食費」の1行に、日割りの額がまとまって増えていく（日ごとに行が増えない）。
4. イベントのボタンの表示（金額の補完・チップ）が今までどおり。
5. 丸山さんのテスト用メニュー（MaruyamaTestRunner）がエラーなく動く。

**終わったら**：[agreements-needed.md](agreements-needed.md) の M1・M2・A1 に日付を書き、2人に「EventChoice に家計簿の種類が増えたので、お金が動く選択肢を作るときは入れてほしい」と伝える。

---

### 依頼E1b-2：Main_test2 でイベントが毎月出ない不具合の修正

E1b の確認中に見つかった（2026-10-22）。`Main_test2` で月をまたいでも、イベントが出ない。

- 原因：`EventManager` は `OnEnable` で `MonthTicker.I.OnMonthChanged` を購読しているが、`MonthTicker.Awake`（`I` を入れる）より先に `EventManager.OnEnable` が呼ばれるため、`MonthTicker.I` が null で購読が黙って飛ばされる。後から購読し直す処理も無い。
- Play 中に調べた `OnMonthChanged` の購読者は `AutoSaver`・`HouseholdIncome`・`SupportManager`・`MealSystemManager` の4つで、`EventManager` が入っていなかった。
- 他の部品（`HouseholdIncome` など）は `Start` で購読しているので起きない。丸山さんのテスト用シーンでは呼ばれる順番がたまたま合っていて表に出なかった。

```
docs/team/antigravity-expenses.md の「依頼E1b-2」を読んでから作業してください。
この依頼に限り、Assets/Maruyama/Scripts/Events/EventManager.cs を変更してよい（E1b と同じく本人の了承済み）。

【目的】
Main_test2 で、月が変わるたびにイベント（デメリット＋メリット）が出るようにする。

【変更】Assets/Maruyama/Scripts/Events/EventManager.cs
1. MonthTicker.I.OnMonthChanged の購読を OnEnable から Start に移す。
   MonthTicker.I が null なら Debug.LogError("EventManager: MonthTicker が見つかりません") を出す（HouseholdIncome.Start と同じ形）。
2. 購読の解除を OnDisable から OnDestroy に移す（MonthTicker.I が null なら何もしない）。
3. 二重に購読しないよう、購読したかを bool で持ち、解除のときに見る。
4. 確認用に [ContextMenu("Debug/今すぐイベントを出す")] を付けたメソッドを足し、TriggerEventPair() を呼ぶ。
5. HandleMonthChanged・TriggerEventPair・PickValidEvent の中身は変えない。

守ること：
- EventManager.cs 以外の、他の人のファイルは変更しない。シーン・アセットは触らない。
- Script Execution Order（実行順の設定）は変えない（他の部品に影響するため）。
- Unity の MCP ツール（unityMCP）は使わないでください。終わったら、変更した行と、Unity で確認してほしいことを箇条書きで教えてください。
```

**確認（依頼E1b-2）**
1. `Main_test2` で Play し、食事を選んで 0歳1月 → 2月をまたぐ。Console に「[EventManager] 月変更を検知 (0歳 2月)」が出て、風邪と一時預かり保育の2枚が出る。
2. 「EventManager が見つかりません」「MonthTicker が見つかりません」のエラーが出ていない。
3. `EventManager` の Inspector の ⋮ から「Debug/今すぐイベントを出す」を実行すると、すぐにイベントが出る（E1b の確認を早く回すのに使える）。
4. Play を止めて、もう一度 Play しても、イベントが1か月に1回だけ出る（2回出ない）。

**終わったら**：丸山さんに「Main_test2 で購読の順番の問題があったので、EventManager の購読を Start に移した」と伝える。

---

### 依頼E2：毎月の支出（自動でかかるもの）

**前提：依頼E1b が終わっていること**（`EffectApplier.Apply(effects, LedgerSource)` が無いとコンパイルできない）。

```
docs/design/expenses.md の 1〜3章・6.1・6.2 と docs/team/antigravity-expenses.md の 2章 を読んでから作業してください。
置き場所は Assets/Yamaguchi/Scripts/Core/Expense/（アセットは Assets/Yamaguchi/Data/Expenses/）。

1. ExpenseMode.cs（Auto, Optional）、CostTier.cs、ExpenseItem.cs（[CreateAssetMenu("子育て/支出項目")]）、ExpenseRuntime.cs：expenses.md 3.1・3.2 のとおり。
   CostTier には PaymentTier と同じ Contains(ageDays)・RequirementsSatisfied(ctx)・DisplayLabel() を持たせる。
   ExpenseItem に CostTier FindTier(GameContext ctx)（上から順に「年齢が範囲内 かつ 条件を全部満たす」最初の段。無ければ null）。
2. ExpenseManager.cs（MonoBehaviour・ISaveParticipant。SaveKey "expense"、RestoreOrder 35）：
   - Inspector：全部の支出項目のリスト。
   - MonthTicker.OnMonthChanged で、Auto の項目と、申し込み中（enrolled）の Optional の項目について
     今月の額 = FindTier の monthlyCost。0 なら何もしない。
     お金は Effect（Money, Household, −額）を作って EffectApplier.Apply(effects, new LedgerSource(項目の title, 項目の category)) で引く。
     段の monthlyEffects（お金以外）も同じ月に適用する。
   - 肩代わり（無償化）はこの依頼では入れない。ただし「今月の額を計算する」部分を1つのメソッド（CalcMonthlyCost）に分けておく（依頼E3 でここに足す）。
   - Enroll(ExpenseItem) / Withdraw(ExpenseItem) / IsEnrolled(ExpenseItem) を public で持つ（Optional 用。画面は後で）。
   - セーブは ExpenseRuntime のリスト。
3. SupportManager.cs：支援の支給（PayMonth の EffectApplier.Apply と、申請時の s.effects）を
   EffectApplier.Apply(effects, new LedgerSource(制度の名前, ExpenseCategory.支援制度)) に変える。
4. アセットを作る：expenses.md 6.1（家賃・電気代・ガス水道・通信）と 6.2（保育料・保育園の給食費・学校教育費・給食費・学用品・高校授業料・高校のその他）。
   保育料は年収の段を安い順に並べ、IncomeRequirementSO を使う（足りない種類の RequirementSO が要るなら作ってよいが、報告に書く）。
   段の年齢は「6歳4月」のように GameDay で入れる。
5. 確認用 [ContextMenu]：「Debug/今月の支出をログ（引かない）」：項目ごとに 名前・段・額 を出す。

守ること：
- お金は必ず EffectApplier を通す（GameState を直接触らない）。
- Maruyama・Arisaka・Kusao のフォルダのファイルは変更しない。
- Unity の MCP ツール（unityMCP）は使わないでください。終わったら、作ったアセットの一覧と値、シーンに置くもの、Unity で確認してほしいことを箇条書きで教えてください。
```

**確認（依頼E2）**
1. 「Debug/今月の支出をログ」で、0歳なら家賃・電気代・ガス水道・通信・保育料が出る。
2. Inspector で年齢を 6歳3月 → 4月にすると、保育料が消えて学校教育費・給食費が出る。
3. 年収を変えると保育料の段が変わる。
4. 月をまたぐと所持金が減り、家計簿に項目ごとの行が出る。児童手当の行は「支援制度」の種類で出る。
5. **0 円で止まる**のは今はまだ正しい（依頼E4 で直す）。

---

### 依頼E3：無償化（支援制度による肩代わり）

[antigravity-support-apply.md](antigravity-support-apply.md) の依頼6（無償化）はこれに置き換える。

```
docs/design/expenses.md の 4章、docs/design/support-apply.md（無償化の制度の章）を読んでから作業してください。

1. CoverType.cs（Full, Rate, UpTo）、ExpenseCover.cs：expenses.md 4章のとおり。
2. SupportScheme.cs に List<ExpenseCover> covers を足す（空なら今までどおり。既存の制度の動きは変えない）。
3. ExpenseManager.CalcMonthlyCost に肩代わりを足す：
   支払う額 = 支出の額 − 受給中の制度の covers の合計（期間内のものだけ。0 未満にしない）。
   受給中かどうかは SupportManager に聞く（受給中の制度の一覧を返す窓口が無ければ足す）。
   家計簿には 支払った額 を amount、肩代わりされた額を covered として Record する
   （EffectApplier は amount だけ扱うので、covered は ExpenseManager から Ledger.I.Record で別に入れるか、
     Ledger に covered だけ足す窓口 AddCovered(source, covered) を作る。どちらにしたか報告に書く）。
4. 幼児教育・保育の無償化（3〜5歳の保育料を全額）と、高校就学支援金（高校授業料を上限まで）の制度アセットに covers を設定する。
5. 確認用 [ContextMenu]「Debug/今月の支出をログ」に 肩代わり額 の列を足す。

守ること：
- Maruyama・Arisaka・Kusao のフォルダのファイルは変更しない。
- Unity の MCP ツール（unityMCP）は使わないでください。終わったら、Unity で確認してほしいことを箇条書きで教えてください。
```

**確認（依頼E3）**
1. 3歳の4月：無償化を申請していないと保育料を全額払う。申請して受給中になると 0 円になり、給食費は払い続ける。
2. 家計簿の保育料の行に「支援で助かった額」が出る。
3. 高校で就学支援金を受けると授業料が減る。

---

### 依頼E4：お金が足りないとき・終わり方をまとめる

```
docs/design/expenses.md の 5.7 と docs/design/result-scene.md、docs/team/antigravity-expenses.md の 2.4 を読んでから作業してください。

1. GameState.cs：
   - AddMoney と LoadValues の 0 円止め（Mathf.Max(0, …)）を外す。
   - enum GameEndReason { GoalReached, Runaway, Bankrupt } と event Action<GameEndReason> OnGameEnded を足す。
   - public void EndGame(GameEndReason reason)：まだ終わっていなければ IsGameOver = true にして OnGameEnded を出す。
     reason が GoalReached のときだけ、移行期間として今の OnGameOver も出す。
   - ゴール到達（AddAgeDays の中）は EndGame(GoalReached) を呼ぶ形にする。
2. RunawayWatcher.cs：家出したら今の OnRunaway に加えて GameState.I.EndGame(Runaway) を呼ぶ（判定は変えない）。
3. AutoSaver.cs：OnGameOver と OnRunaway の2つの購読を、OnGameEnded の1つにまとめる（どの理由でもセーブを消す）。
4. BankruptcyWatcher.cs（Core。MonoBehaviour・ISaveParticipant。SaveKey "bankrupt"、RestoreOrder 45。RunawayWatcher を真似る）：
   - MonthTicker.OnMonthSettled で所持金がマイナスなら 赤字の月 +1、プラスなら 0 に戻す。
   - 赤字の月 >= [SerializeField] 上限（既定 2）で GameState.I.EndGame(Bankrupt) と OnBankrupt。
   - 赤字の月が 1 以上になったら OnDeficitMonthsChanged(int) を出す。
5. DeficitWarningUI.cs（UI）：OnDeficitMonthsChanged で開く。
   「今月は ○○円 足りませんでした。来月も赤字だと家計が破綻します」＋［家計簿を見る］（依頼E5 までは押せないままでよい）＋［サポート画面へ］＋［閉じる］。
   開いている間は時間を止める（PushPause / PopPause）。
6. result-scene.md の EndingFlow を作っていれば（Assets/Yamaguchi/Scripts/Core/Ending/）、
   購読を GameState.OnGameEnded の1つに変え、EndingReason に 家計破綻 を足す。まだ無ければ何もしない。

守ること：
- 他の人のコードで OnGameOver を使っているところは変えずに、報告に一覧を書く（移行期間は両方出すので壊れない）。
- 有坂さんの MoneyUI がマイナスを表示できるか確認し、直さずに報告に書く（agreements-needed.md A2）。
- Unity の MCP ツール（unityMCP）は使わないでください。終わったら、シーンに置くもの・Unity で確認してほしいことを箇条書きで教えてください。
```

**確認（依頼E4）**
1. 所持金を Inspector で少なくして月をまたぐ → マイナスになり、警告が出て時間が止まる。
2. 次の月もマイナス → 家計破綻で終わる（EndingFlow があれば end シーンへ）。セーブが消えている。
3. 1か月赤字 → 次の月プラスに戻ると、数えが 0 に戻る。
4. 20歳到達・家出も今までどおり終わる。

---

### 依頼E5：家計簿の画面（月ごと・年ごと）

**2026-10-28 に書き直した**（山口の指示：TMP で作る／お金の表示の下の［家計簿］ボタンで開く／電気代・ガス代・食費・収入が月ごとに見える／年末の所得税などの税金もリアル寄りに見せる／月と年の両方でいくらかかったか見える）。
誕生日のまとめ（元の E5 の 2）は外し、後に回す（4章）。

#### 画面の決まりごと

- 文字はすべて **TextMeshPro（`TextMeshProUGUI`）**。フォントは `Resources.Load<TMP_FontAsset>("JPFont")`。ボタンも `Button` ＋ 子の `TextMeshProUGUI`（レガシーの `Text` は使わない）。
- 開くボタンは **すでにある `Canvas/Buttons/Kakeibo`**（お金の表示の下の［家計簿］）。今は文字のフォントが `LiberationSans SDF - Fallback` なので **JPFont に直す**。
- 画面は `Canvas` の子の **一番下** に置く（他の HUD より手前）。Canvas は 800×600 基準（`Main_test2`）。
- 開いている間は時間を止める（`PauseWhileOpen`。決定 0011）。
- 給料は **手取りで入る**（今の作りのまま）。家計簿では「額面」「天引き（社会保険料・所得税・住民税）」「手取り」に分けて見せる。

#### 月の画面（［月］タブ）

```
┌──────────────────────────────────────────────┐
│ 家計簿        ［月］［年］                     ［×］│
│        ‹  3歳 2月  ›                              │
│ ┌ 収入 ───────────────── ┐┌ 支出 ──────────────┐│
│ │ 給料（母）額面  300,000 ││ 住居   家賃   90,000 ││
│ │ 給料（父）額面  200,010 ││ 光熱費 電気代 13,263 ││
│ │ 社会保険料    − 75,002  ││        ガス・水道 10,000││
│ │ 所得税        − 10,830  ││ 通信   通信費 12,000 ││
│ │ 住民税        − 18,500  ││ 食費   食費（普通）90,000││
│ │ 児童手当      + 15,000  ││ 保育   保育料  0     ││
│ │                         ││      （支援 −44,500） ││
│ │                         ││ 医療   …            ││
│ └─────────────────────────┘└──────────────────────┘│
│ 手取り 411,678 ＋支援 15,000 − 支出 219,763 ＝ 差し引き +206,915 │
│ 支援で助かった額 59,500   子育てにかかったお金 94,500         │
└──────────────────────────────────────────────┘
```

- ‹ › で月を移動。直近 **24 か月** まで戻れる（`Ledger` が行を持っている範囲）。今月より先には進めない。
- 左に **収入と天引き**、右に **支出**。どちらも縦スクロール（`docs/code/ui-scroll-patterns.md`）。
- 支出は **種類ごと（住居・光熱費・通信・食費・保育・学校・塾習い事・医療・その他）に見出し＋小計**、その下に行（電気代・ガス・水道 など `LedgerLine.source` そのまま）。0 円で肩代わりもない行は出さない。
- 肩代わりがある行は「（支援 −○○円）」を小さく添える。
- 色：収入は緑、支出・天引きは赤、支援は青（色は Inspector で変えられる）。
- 一番下に **手取り／支援／支出／差し引き** と、**支援で助かった額**・**子育てにかかったお金**（`Ledger.IsChildCost` の種類の支出）。

#### 年の画面（［年］タブ）

```
┌──────────────────────────────────────────────┐
│ 家計簿        ［月］［年］                     ［×］│
│        ‹  3歳の1年（1月〜12月） ›                  │
│ 月ごとの支出  ▇▇▆▇▇▇▇▇▇▇▇█  （12本の棒・押すとその月へ）│
│ ┌ 収入と税金 ──────────────┐┌ 支出（種類ごと）──────┐│
│ │ 給料 額面（母）3,600,000 ││ 住居     1,080,000    ││
│ │ 給料 額面（父）2,400,120 ││ 光熱費     279,156    ││
│ │ 社会保険料     −900,018 ││ 食費     1,080,000    ││
│ │ 所得税（毎月） −129,960 ││ 保育       489,500    ││
│ │ 年末調整        +12,400 ││ …                    ││
│ │ 確定申告の還付        0 ││                      ││
│ │ 住民税         −222,000 ││                      ││
│ │ → 1年の税金 −1,239,578  ││                      ││
│ │ 支援制度       +180,000 ││                      ││
│ └──────────────────────────┘└──────────────────────┘│
│ 手取り ○○ ＋支援 ○○ − 支出 ○○ ＝ 1年の差し引き ○○    │
│ 子育てにかかったお金 ○○（うち支援で助かった額 ○○）     │
└──────────────────────────────────────────────┘
```

- 「1年」＝ **子が y 歳の 1 月〜12 月**（税金の1年と同じ。taxes.md 2章）。‹ › で年を移動（0 歳〜今の年）。今の年は途中までの合計。
- 上に **12 か月の支出の棒グラフ**（`Image` を 12 本並べ、高さを額に合わせる。その月の合計を TMP で上に小さく）。棒を押すと［月］タブのその月へ。
- 左は **収入と税金**。税金は行の名前ごとに1年分を足し、**「年末調整」「確定申告の還付」を別の行** にして、最後に **1年の税金の合計（社会保険料＋所得税＋年末調整＋確定申告＋住民税）** を出す。
- 右は **支出を種類ごとの1年の合計**。種類を押すと中の行（電気代・ガス・水道 など）が開く。
- 一番下に1年の差し引きと、子育てにかかったお金。

#### データ（Ledger に足すもの）

今の `Ledger` は行を **24 か月分しか持たない** ので、古い年の内訳が出せない。次を足す（セーブ対象・古いセーブは空で始まってよい）。

| 足すもの | 中身 | 使う画面 |
|---|---|---|
| 年ごとの行の合計 | 子の年齢 y ごとに「名前＋種類」→ amount・covered の合計 | 年の画面の内訳 |
| 月ごとの合計 | monthIndex ごとに 収入・支出・支援で助かった額 | 年の画面の棒グラフ |

月の数は最大でも 20 年 × 12 か月なので、全部持っても小さい。

```
docs/design/expenses.md の 5章・5.1、docs/design/taxes.md の 2章・4章、docs/code/ui-scroll-patterns.md、
docs/decisions/0011-pause-while-open-component.md、docs/team/antigravity-expenses.md の「依頼E5」を読んでから作業してください。
置き場所：Core は Assets/Yamaguchi/Scripts/Core/Ledger/、UI は Assets/Yamaguchi/Scripts/UI/Ledger/、
プレハブは Assets/Yamaguchi/Prefabs/UI/LedgerPanel.prefab。

【1. Ledger に足す（Core）】
- 年ごとの行の合計：Dictionary<int 年齢, List<LedgerLine>>（monthIndex は使わない）。Record のたびに同じ年・名前・種類の行に足す。
  読み取り：LinesOfYear(int age)（コピーを返す）。
- 月ごとの合計：Dictionary<int monthIndex, LedgerMonthSummary{monthIndex, income, expense, covered}>。
  読み取り：MonthSummary(int monthIndex)、OldestMonthIndex（行が残っている一番古い月）。
- ToSave / Restore / Clear に足す（LedgerSaveData に項目を足す。古いセーブで項目が無くても動く）。
- 今ある読み取り（LinesOfMonth・TotalOf・TotalChildCost・TotalSupport・YearSummary）の結果は変えない。

【2. 書式をまとめる（UI）】
- MoneyFormat.cs（static）：Yen(int) →「12,345円」、Signed(int) →「+12,345」「−12,345」、Man(int) →「12.3万円」。
  リザルト画面でも使えるように1か所にまとめる。

【3. 画面（UI）】すべて TextMeshProUGUI・フォントは Resources.Load<TMP_FontAsset>("JPFont")。Ellipsis は使わない。
- LedgerPanelUI.cs（画面の本体）：Open() / Close()。［月］［年］タブ。PauseWhileOpen を同じ GameObject に付ける前提。
  計算はしない。値は Ledger から受け取って並べるだけ。開いたときは「今月」の［月］タブ。
- LedgerMonthView.cs：上の「月の画面」の図のとおり。‹ › で OldestMonthIndex〜今月。
  左：給料（額面）・税金社会保険の行・支援制度の行。右：支出を種類ごとの見出し＋小計＋行。
  0 円で covered も 0 の行は出さない。covered があれば「（支援 −○○円）」。
  下：手取り（給料 ＋ 税金社会保険）／支援／支出／差し引き、支援で助かった額、子育てにかかったお金。
- LedgerYearView.cs：上の「年の画面」の図のとおり。‹ › で 0 歳〜今の年。
  12 本の棒（MonthSummary の expense。一番多い月を 100%）。棒を押すとその月の［月］タブへ。
  左：給料（額面）、税金の行（「年末調整」「確定申告の還付」で始まる行は別の行のまま）、1 年の税金の合計、支援制度。
  右：支出の種類ごとの 1 年の合計。押すと中の行を開く。
  下：1 年の差し引き、子育てにかかったお金（うち支援で助かった額）。
- LedgerRowUI.cs：1 行（名前・金額・肩代わりの注記・色）。見出し行と通常の行を兼ねる。
- 色（収入・支出・支援・見出し）は LedgerPanelUI の [SerializeField] にまとめる。

【4. プレハブとボタンをつなぐエディタメニュー】（TaxSetupBuilder・SupportUIBuilder と同じ形）
- Assets/Yamaguchi/Scripts/Editor/LedgerUIBuilder.cs：
  「子育て/家計簿/1. 家計簿の画面のプレハブを作る」… LedgerPanel.prefab を作る（全部 TMP。800×600 基準の Canvas に収まる大きさ）。
  「子育て/家計簿/2. 開いているシーンに置いて［家計簿］ボタンにつなぐ」… Canvas の子の一番下に置き（非表示で）、
   Canvas/Buttons/Kakeibo の Button.onClick に LedgerPanelUI.Open を登録し、Kakeibo の文字のフォントを JPFont にする。
   何をしたかをログに出す。シーンの保存は人間が行う。

守ること：
- 計算は Ledger 側。画面は並べるだけ。
- Maruyama・Arisaka・Kusao のフォルダのファイルは変更しない。シーンファイルを直接書き換えない（メニュー 2 で人間が実行する）。
- 他のボタン（サポート・カレンダー・セーブ・設定）の動きは変えない。
- Unity の MCP ツール（unityMCP）は使わないでください。終わったら、作ったファイル、メニューの使い方、Unity で確認してほしいことを箇条書きで教えてください。
```

**確認（依頼E5）**
1. メニュー「子育て/家計簿/1」→「2」の順に実行し、`Main_test2` を保存。［家計簿］ボタンの文字が JPFont になっている。
2. Play → ［家計簿］を押すと画面が開き、時間が止まる。［×］で閉じると時間が動く。
3. 2〜3 か月進めてから開く：［月］タブで、給料（額面）・社会保険料・所得税・住民税・電気代・ガス・水道・家賃・食費（普通）などが出る。‹ で先月に戻れる。
4. 下の「手取り」が、その月に実際に増えた給料の額と合っている。
5. 3 歳で無償化を受けている月：保育料が 0 円で「（支援 −44,500円）」と出る。
6. ［年］タブ：12 本の棒が出て、押すとその月に移る。12 月を過ぎた年は「年末調整」の行が出ていて、1 年の税金の合計が出る。
7. セーブ → ロード後も、年の画面の内訳が残っている。
8. 文字がすべて TMP（レガシーの Text が無い）で、日本語が豆腐（□）になっていない。

---

## 4. 後に回すもの

| 内容 | 理由 |
|---|---|
| 塾・習い事の申し込み画面（Optional の支出） | 画面の置き場所が未定（expenses.md 7章）。`ExpenseManager.Enroll` は E2 で用意済み |
| 学費・浪人の予備校代・私立高校 | 進路の仕組み（[antigravity-ending.md](antigravity-ending.md) 依頼3・8）ができてから |
| 猶予期間中の借金・支援金 | 家計破綻が動いてから（expenses.md 5.7） |
| 誕生日のまとめ（1 年ごとの「この1年でかかったお金」の画面） | 家計簿の［年］タブで同じことが見られるので後回し（2026-10-28） |
| 費目と金額の見直し | メンバーの調査（[../research/expenses.md](../research/expenses.md)）が終わってから。アセットの値を変えるだけ |

---

## 5. 進み具合

| 依頼 | 内容 | 状態 |
|---|---|---|
| E1 | 家計簿の土台・給料の手取り・月の精算の順番 | **完了**（2026-10-21。takeHomeRate 0.8 を確認） |
| E1b | イベント・食事のお金を家計簿につなぐ（M1・M2・A1。山口が実施） | **ほぼ完了**（2026-10-22。病院が「イベント名：選択肢」で載ることを確認）。残り：`Event_Merit_Daycare`「一時預かり保育に申し込む」の種類を 保育 に／`MaruyamaTestRunner` の確認 |
| E1b-2 | Main_test2 でイベントが毎月出ない不具合の修正（EventManager の購読を Start へ） | **完了**（2026-10-22。Main_test2 の Canvas に EventDialog を置き、CardsPanel を 1280×720・Scale 0.625 に） |
| E2 | 毎月の支出（自動） | **完了**（2026-10-28 確認。保育料のアセットの年収の条件への参照が `type: 3` で切れていたのを `type: 2` に直した → 年収の段が正しく選ばれる） |
| E3 | 無償化（肩代わり） | **完了**（2026-10-28 確認。保育の無償化：申請した翌月から保育料 0 円・給食費は払う・6歳1月で受給終了／就学支援金：翌月から授業料 0 円・肩代わり 9,900 円／児童手当は変わらない／セーブ→ロード後も肩代わりが続く） |
| E4 | お金が足りないとき・終わり方をまとめる | 未依頼 |
| E5 | 家計簿の画面（月ごと・年ごと。TMP・［家計簿］ボタンから開く・税金と年末調整も見せる） | **コード作成完了**（2026-10-28。LedgerPanel・LedgerRow・LedgerMonthView・LedgerYearView・LedgerUIBuilder を実装。メニューからプレハブ生成とシーン配置を行い確認待ち） |
