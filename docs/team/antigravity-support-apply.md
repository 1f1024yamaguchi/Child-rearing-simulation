---
title: Antigravity への依頼文（支援制度の申請と効果の反映）
updated: 2026-10-07
status: 確定
related: [docs/design/support-apply.md, docs/design/expenses.md, docs/design/calendar-system.md, docs/team/agreements-needed.md]
---

# Antigravity への依頼文（支援制度の申請と効果の反映）

> 下の枠をそのまま Antigravity に貼る。**一度に全部ではなく、ステップごとに1つずつ**頼み、Unity で「確認」の項目を試してから次へ進む。
> 仕様の本体は [../design/support-apply.md](../design/support-apply.md)。依頼文は「どの章を読んで、何を作るか」だけを書いている。

## 進め方の全体

| ステップ | 作るもの | 他のメンバー待ち |
|---|---|---|
| 1 | データと状態の土台（Core）・条件の部品・年収の記録 | **完了（2026-10-07）** |
| 2 | 制度カードの表示（申請前・申請後） | **完了（2026-10-07）→ [ui-support-card.md](../code/ui-support-card.md)** |
| 3 | 申請ボタン・確認ダイアログ・通知・タブ | **前半（並べる・タブ・時間停止）完了（2026-10-07）**。後半（確認ダイアログ・通知・サポートボタンのバッジ）は未着手 |
| 4 | セーブ・ロード | セーブ本体（山口）ができてから |
| 5 | カレンダーとのつなぎ | 草尾さん K5・K6 の合意後（[agreements-needed.md](agreements-needed.md)） |
| 6 | 無償化（支出の肩代わり） | 支出の仕組み（[expenses.md](../design/expenses.md)）ができてから |

ステップ1〜3だけで「申請したらお金が入る・カードに期限や受給状況が出る」は動く。

**毎回の依頼文の最後に付ける1行（クレジット節約）**

```
Unity の MCP ツール（unityMCP）は使わないでください。コードを書き終えたら、Unity で確認してほしいこと（Console のエラー、Play での確認手順）を箇条書きで教えてください。確認は私が Unity で行います。
```

- コンパイルや Play での確認は自分で Unity を開いて行う（Unity に切り替えると自動で再読み込み・コンパイルされる）。
- エラーが出たら、Console の文をそのまま Antigravity に貼って直してもらう。

---

## ステップ1：データと状態の土台

```
AGENTS.md と .agents/rules/ を読んでから作業してください。
仕様は docs/design/support-apply.md です（0〜6章・10章）。docs/design/support-systems.md 3.5章、docs/design/effects.md も読んでください。

support-apply.md 8章の表のうち、Core のファイルを作ってください。UI はまだ作りません。

- SupportScheme.cs（support-systems.md 3.5 の形 ＋ support-apply.md 5.1 の追加項目。ただし covers はステップ6で足すので今は作らない）
- SupportCategory.cs / SupportState.cs / PaymentTier.cs / SupportSchemeRuntime.cs
- GameContext.cs（4.2。AnnualIncome は IncomeHistory から取る）
- IncomeHistory.cs（4.2.1。HouseholdIncome の給料日に額面の受取額を記録し、直近12か月の合計を返す。12か月未満は平均×12）
- Requirements/RequirementSO.cs（抽象クラス）と ChildAgeRequirementSO.cs / IncomeRequirementSO.cs（4.1〜4.3）
- SupportManager.cs（6章の窓口をすべて。状態の移り方は 2.1、翌月判定は 2.2、毎月の支給額の決め方は 3.2 の手順どおり）

守ること：
- 効果の適用は Maruyama.Core.EffectApplier.Apply(...) だけ。GameState を直接触らない。
- 条件の判定は GameContext.Current() を渡して RequirementSO.IsSatisfied で行う。SupportManager に条件ごとの if を書かない。
- 「Condition」という名前は使わない（ConditionDefinition と衝突するため Requirement にする）。
- 時間は DayTicker.OnDayPassed / MonthTicker.OnMonthChanged を購読して判定し、自分で数えない。OnDestroy で購読を解除する。
- ScriptableObject（SupportScheme）に実行中の状態を書かない。状態は SupportSchemeRuntime に id をキーにして持つ。
- 置き場所は Assets/Yamaguchi/Scripts/Core/（条件は Core/Requirements/）。1ファイル1クラス。
- 他のメンバーのフォルダ（Assets/Maruyama など）のファイルは変更しない。
- 動作確認用に、状態の変化と支給（どの段でいくら払ったか）を Debug.Log に出してください。
```

**確認**：support-apply.md 9.1・9.3 の制度アセット（出産育児一時金・妊婦のための支援給付・児童手当・就学援助）と条件アセットを作り、`SupportManager` に登録して Play。Inspector か仮のボタンから `Apply` を呼び、Console に「Available」「児童手当 段:3歳未満 +15,000」などが出るか。

---

## ステップ2：制度カードの表示（申請前・申請後）

```
docs/design/support-apply.md 7.1〜7.2章のとおり、制度カードを作ってください。

- SupportScheduleText.cs：状態と日付から「期限の行」と「支給表の行」の文字列を返す static 関数だけを持つクラス。7.2 の表の全状態に対応。条件未達のときは SupportManager.UnmetRequirements の Describe() を並べる。日付は GameDate.Format を使い、自前で日付計算しない。
- SupportSchemeCard.cs：Bind(SupportScheme) で表示。SupportManager.OnStateChanged を購読して自分の表示（バッジ・期限の行・支給表の行・ボタン）だけ更新する。カードを作り直さない。期限まであと7日以下は赤字。
- 効果チップは scheme.effects から自動生成（effects.md 5章）。Monthly は「毎月」を付ける。Assets/Maruyama の EffectChipUI が使えるなら、変更せずに参照して使う。
- シーン Assets/Yamaguchi/scenes/Main_test2.unity の supports → scheme_scroll → Content にある card_01 をもとに、プレハブ Assets/Yamaguchi/Prefabs/UI/SupportCard.prefab を作る。状態バッジ・効果チップの行・支給表の行・期限の行を追加し、LayoutElement.preferredHeight を 120 程度にする。
- 制度カードには［📅 カレンダーに追加］ボタンも置くが、押したときの処理はステップ5まで Debug.Log だけにする。
- TextMeshPro の Overflow は Ellipsis にしない（.agents/rules を参照）。フォントは Resources の JPFont。
```

**確認**：Play して時間を進め、カードが「まだ対象外 → 申請できる → 受給中」と変わるか。期限の行の日付と「あと○日」が合っているか。

---

## ステップ3：申請ボタン・確認ダイアログ・通知・タブ

```
docs/design/support-apply.md 7.3〜7.6章のとおりに作ってください。

- SupportPanelController.cs：タブ（政府／地方自治体／会社）を押したら GetByCategory で Content のカードを作り直す。タブと下部のサポートボタンに「申請できる」数のバッジ。OnEnable で GameClock.I.PushPause()、OnDisable で PopPause()。Open(string schemeId) で、その制度のタブを開いてカードまでスクロールする窓口も作る（カレンダーから呼ぶ）。
- SupportApplyDialog.cs：［申請する］で確認を出し、OK なら SupportManager.Apply。Yearly の更新は Renew、支給停止は Resume。もらえるもの／かかるもの／注意（demerits）を分けて表示。
- SupportToast.cs：申請直後（7.4）と期限切れ（OnExpired・1回だけ）の短い通知。毎月の支給は通知しない（10章）。
- GameClock.SetRunning は画面の開閉に使わない（AGENTS.md ルール8）。
```

**確認**：support-apply.md 9.4 のテスト手順 1・2・3・5（4 は無償化なのでステップ6）。お金の表示（HUD）が申請や毎月の支給で増えるか。

---

## ステップ4：セーブ・ロード（セーブ本体ができてから）

```
docs/design/support-apply.md 5.2 と 4.2.1 のとおり、SaveData に
- List<SupportSchemeRuntime> supportSchemes
- IncomeHistory の直近12か月の記録
を足し、SupportManager.ToSave / Restore と IncomeHistory の保存・復元を作ってください。
セーブにない制度（後から追加したアセット）は、今日の日付と条件から状態を計算して作ってください。
```

**確認**：児童手当を申請 → セーブ → 止めて再開 → ロードで「受給中」と次回の支給日が戻るか。

---

## ステップ5：カレンダーとのつなぎ（草尾さん K5・K6 の合意後）

```
docs/design/calendar-system.md 2.3〜2.4 のとおり、
- SupportSchemeCard の［📅 カレンダーに追加］で CalendarUI.OpenAddPlan(scheme.id, 締め切りの通算日数, PlanKind.Apply) を呼ぶ（締め切りがない制度は今日の日付、毎年の手続きは今年の期限日と PlanKind.Renew）。
- GameClock に public bool IsPauseRequested => pauseRequests > 0; を追加する（Core）。
CalendarUI 本体（Assets/Kusao）は変更しないでください。
```

---

## ステップ6：無償化（支出の仕組みができてから）

```
docs/design/expenses.md 4章と docs/design/support-apply.md 3.2 の 5 のとおり、
- ExpenseCover.cs を作り、SupportScheme に List<ExpenseCover> covers を足す。
- 受給中の制度の covers を ExpenseManager が支出から差し引けるよう、SupportManager に「今月この支出をいくら肩代わりするか」を返す窓口を作る。
- support-apply.md 9.2 の幼保無償化・高校の就学支援金のアセットを作る。
```

**確認**：support-apply.md 9.4 の 4（3歳で無償化を申請 → 翌月から保育料が 0、給食費は残る）。
