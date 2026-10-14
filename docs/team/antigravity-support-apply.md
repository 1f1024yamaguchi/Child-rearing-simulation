---
title: Antigravity への依頼文（支援制度の申請と効果の反映）
updated: 2026-10-14
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
| 3 | 申請ボタン・確認ダイアログ・通知・タブ | **完了（前半 2026-10-07、後半 2026-10-11 確認）** |
| 4 | セーブ・ロード | **完了（2026-10-13）**。セーブ依頼2で `SupportManager` と年収の記録を参加者にして実装・確認済み（[antigravity-save-system.md](antigravity-save-system.md)）。下の依頼文は不要 |
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

## ステップ3：後半（確認ダイアログ・通知・サポートボタンのバッジ）

> 前半（カードを並べる・タブ・時間停止）は完了済み（`SupportPanelController`、→ docs/code/ui-support-card.md 8章）。

```
AGENTS.md と .agents/rules/、docs/design/support-apply.md 7.3〜7.5章、docs/code/ui-support-card.md を読んでから作業してください。
ステップ3の後半として、次の3つを作ってください。既にある SupportSchemeCard / SupportPanelController / SupportManager の作りは変えず、足す形にしてください。

1. SupportApplyDialog.cs（Assets/Yamaguchi/Scripts/UI/）
   - SupportPanelController が、並べた各カードの OnPrimaryClicked を購読し、このダイアログを開く。
     （購読者がいるとカードは SupportManager を直接呼ばなくなる。ui-support-card.md 4章）
   - ダイアログには「制度名」「もらえるもの（プラスの効果）」「かかるもの（マイナスの効果・手間のストレス）」「注意（demerits）」と、Monthly なら「（来月から）」、OnApply なら「（今すぐ）」を出す。
     効果の文は丸山さんの EffectChipUI を変更せずに使ってよい。支給表がある制度は SupportScheduleText.TierLine の文も出す。
   - ボタンは［やめる］［申請する］。PrimaryAction が Renew なら［更新する］、Resume なら［手続きする］にして、OK でそれぞれ SupportManager.Apply / Renew / Resume を呼ぶ。
   - ダイアログは supports（サポート画面）の子として置く。サポート画面が開いている間は時間が止まっているので、ダイアログで PushPause はしない。
   - プレハブ化：Assets/Yamaguchi/Prefabs/UI/SupportApplyDialog.prefab

2. SupportToast.cs（Assets/Yamaguchi/Scripts/UI/）
   - 画面上部に2秒ほど短い通知を出す（重なったら順番に出す）。
   - 申請した直後：OnApply「○○を受け取りました　お金 +500,000」／Monthly「○○を申請しました　来月から毎月 +15,000」。
   - SupportManager.OnExpired（期限切れ）：「○○の申請期限が過ぎました」（1回だけ。SupportManager 側で1回に絞ってある）。
   - 毎月の支給（OnPaid）では出さない（support-apply.md 10章）。
   - サポート画面を閉じていても出るよう、Canvas の直下に置く。

3. 下部の［サポート］ボタン（Canvas/Buttons/suport）に「申請できる」制度の数のバッジ
   - 数は SupportManager の全制度のうち GetState == Available の数。0 なら非表示。
   - SupportManager.OnStateChanged で更新。小さな SupportButtonBadge.cs を作ってボタンに付ける。

守ること：
- お金は EffectApplier 経由（SupportManager を呼ぶだけでよい。自分で GameState を触らない）。
- TextMeshPro は JPFont、Overflow を Ellipsis にしない。絵文字は使わない。
- onClick は実行時に AddListener で配線する。
- 他のメンバーのフォルダのファイルは変更しない。
- Unity の MCP ツール（unityMCP）は使わないでください。コードを書き終えたら、Unity で確認してほしいこと（Console のエラー、Play での確認手順）と、Inspector で割り当てが必要な項目を箇条書きで教えてください。確認は私が Unity で行います。
```

**確認**：
1. ［申請する］→ ダイアログが出る → ［やめる］で何も起きない。
2. ［申請する］→［申請する］→ お金が増え、通知「出産育児一時金を受け取りました　お金 +500,000」。
3. 児童手当を申請 → 通知「来月から毎月 +15,000」。
4. 妊婦のための支援給付を放置して 0歳4月1日 → サポート画面を閉じていても「申請期限が過ぎました」。
5. ［サポート］ボタンのバッジが、申請できる制度の数と合っている（申請すると減る）。

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
