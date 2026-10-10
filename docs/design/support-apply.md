---
title: 支援制度の申請と効果の反映
updated: 2026-10-08
status: 確定（他メンバーの合意待ちの項目は docs/team/agreements-needed.md）
related: [docs/design/expenses.md, docs/design/support-systems.md, docs/design/calendar-system.md, docs/design/effects.md, docs/code/architecture.md, docs/decisions/0014-support-state-in-manager.md]
---

# 支援制度の申請と効果の反映

> サポート画面で［申請する］を押したら**実際にお金・ストレスなどが動き**、カードに**申請前は期限・条件・もらえる額、申請後は受給状況・次の手続き**が出るようにするための仕様。
> 制度データ（`SupportScheme`）の形と期限のルールは [support-systems.md](support-systems.md) 3.5 と [calendar-system.md](calendar-system.md) 4章で決定済み。ここでは**その上に乗る「担当の分け方」「状態の管理」「年齢ごとの支給額」「申請条件」「カードの表示」**を決める。数値はすべてたたき台。

---

## 0. 担当の分け方（要相談）

### 0.1 今どうなっているか

[calendar-system.md](calendar-system.md) 5章・8.3 では「制度の状態（申請済み・受給中・支給停止など）のセーブも草尾の担当」となっている。ただし、**状態そのもの（申請したら何が起きるか、毎月いくら入るか）を誰が持つかは決まっていない**。カレンダーは状態を「表示する側」なので、このままだと次のどちらかになる。

- カレンダー（草尾）が状態を持つ → サポート画面がカレンダーのコードに依存する。カレンダー画面を開いていないと状態が進まない、などの事故が起きやすい。
- サポート画面とカレンダーがそれぞれ状態を持つ → 「サポート画面では受給中なのにカレンダーでは申請できる」のような食い違いが起きる。

### 0.2 この仕様での分け方（提案）

**状態を持つ部品を1つだけ Core に作り、画面はどちらもそれを読む。**

```
                 ┌──────────────── Core（山口）────────────────┐
SupportScheme ──→│ SupportManager                               │
（アセット）      │   状態を持つ・時間で進める・申請を受け付ける     │
                 │   効果を EffectApplier に渡す                 │
                 │   ToSave() / Restore()                         │
                 └───────┬───────────────────┬───────────────┘
                         │ 読む＋Apply()を呼ぶ │ 読むだけ
              サポート画面（山口）        カレンダー（草尾）
```

| 担当 | 作るもの | 作らないもの |
|---|---|---|
| 山口（Core） | `SupportScheme` / `SupportState` / `SupportSchemeRuntime` / `SupportManager` / 申請条件の土台（4章）／`SaveSystem` 本体に `SupportManager.ToSave()` の結果を入れる | カレンダーの画面 |
| 山口（UI） | サポート画面（カード・確認ダイアログ・通知） | |
| 草尾 | カレンダーで `SupportManager` を**読んで**予定を出す。カレンダーから申請させるなら `SupportManager.I.Apply(scheme)` を呼ぶだけ。メモのセーブ | 制度の状態を自分で持つこと・状態を計算すること |

### 0.3 草尾さんから見て変わること

- calendar-system.md 8.3 の「制度の状態を保存用の形に変える部分」は**山口側に移る**（`SupportSchemeRuntime` がそのまま保存用の形なので、変換が要らない）。草尾さんのセーブ作業は**メモだけ**になる。
- カレンダーが使うのは次の窓口だけ。これが2人の**約束（インターフェース）**。ここを変えるときは相談する。

  | カレンダーが使うもの | 何に使うか |
  |---|---|
  | `SupportManager.I.Schemes` | 予定に出す制度の一覧 |
  | `GetState(scheme)` | 状態の色（グレー／オレンジ／緑／赤） |
  | `scheme.StartAgeDays` / `NextDeadlineAgeDays(scheme)` | どの日のマスに出すか（毎年の期限もここで今年分が返る） |
  | `OnStateChanged` | 状態が変わったらカレンダーを描き直す |
  | `Apply(scheme)`（任意） | カレンダーから申請させる場合 |

- **カレンダーはプレイヤーが「どの制度をいつまでに申請するか」を書き込む形**（Google カレンダー型・2026-10-05 決定 → [calendar-system.md](calendar-system.md) 2.2〜2.5）。制度を選ぶ一覧に `Schemes` を使う。制度カードには［📅 カレンダーに追加］ボタン（山口）を付け、`CalendarUI.OpenAddPlan(schemeId, ageDays, kind)`（草尾）を呼ぶ。予定の保存は草尾。
- 期限切れの判定・「1回だけ通知」も `SupportManager` 側でやるので、カレンダーで `DayTicker` を購読して期限を判定する必要はなくなる（月表示の更新に使うのは今まで通り）。

### 0.4 草尾さんに確認すること

1. 状態を `SupportManager` に持たせ、カレンダーは読むだけ、でよいか。
2. 8.3 の「制度の状態のセーブ」を山口側に移してよいか。
3. カレンダーから［申請する］を押せるようにするか（押せるなら `Apply` を呼ぶだけ）。

合意できたら calendar-system.md 5章・8.3 をこの分け方に書き換える。

---

## 1. 全体の流れ

```
SupportScheme（アセット・中身は変えない）
        │ 一覧として登録
        ▼
SupportManager（Core・シングルトン）── 制度ごとの「今の状態」を id で持つ（SupportSchemeRuntime）
   │  ├ DayTicker.OnDayPassed      → 状態を進める（申請できるようになった／期限切れ／支給停止…）
   │  ├ MonthTicker.OnMonthChanged → 受給中の制度に「今の年齢の支給額」を払う（3.2）
   │  └ Apply / Renew / Resume     → 申請・更新・再開。申請条件（4章）を満たさないと失敗
   │        効果の適用は必ず EffectApplier.Apply(...)（GameState を直接叩かない）
   │  イベント：OnStateChanged / OnPaid / OnExpired
   ▼
UI（サポート画面）／カレンダー ── 状態を読んで表示するだけ
```

---

## 2. 制度の状態

```csharp
public enum SupportState
{
    Locked,      // 申請できない：開始日より前、または申請条件を満たしていない（グレー）
    Available,   // 申請できる（オレンジ＋申請ボタン）
    Received,    // 受け取り済み：その場で1回もらう制度を申請した（緑）
    Active,      // 受給中：毎月もらう制度を申請した（緑）
    Suspended,   // 支給停止：毎年の手続きを過ぎた（赤みのオレンジ＋手続きボタン）
    Expired,     // 期限切れ：申請しないまま期限を過ぎた（赤）
    Lost,        // 受給資格なし：支給停止のまま lapseYears 経過（赤）
    Ended,       // 受給終了：支給表の最後の年齢を過ぎた（グレー）
}
```

### 2.1 状態の移り方

| 今の状態 | きっかけ | 次の状態 | そのとき起きること |
|---|---|---|---|
| Locked | 開始日になり、かつ申請条件を満たした（毎日判定） | Available | サポートボタンにバッジ |
| Available | 申請条件を満たさなくなった（例：所得が上がった） | Locked | |
| Available | ［申請する］ | Received（OnApply） | `effects` を**その場で1回**適用 |
| Available | ［申請する］ | Active（Monthly） | その場では払わない。**申請した月の翌月から**毎月払う |
| Available / Locked | 期限日の**翌日**になった | Expired | 「○○の申請期限が過ぎました」通知を**1回だけ** |
| Active | 支給表（3.2）の最後の年齢を過ぎた | Ended | 毎月の支給をやめる |
| Active（Yearly） | 毎年の期限日の翌日になった（未更新） | Suspended | 支給を止める。止まった月の額を記録し始める |
| Active（Yearly） | 更新期間中に［更新する］ | Active | 今年分の手続き済みにする |
| Suspended | ［手続きする］ | Active | 再開。**止まっていた月の分をまとめて適用**（決定済み） |
| Suspended | 期限日から `lapseYears` 年たった | Lost | 止まっていた分も受け取れなくなる |

- 期限日の**その日のうちは申請できる**。判定は「今日の通算日数 > 期限の通算日数 なら期限切れ」。
- **受給中に条件を満たさなくなったとき**（例：所得制限を超えた）は状態を変えず、その月の支給額が 0 になるだけ（3.2 の「その月に合う段がない」）。現実の児童手当・就学支援金と同じで、条件を満たせばまた払われる。

### 2.2 「翌月から」の判定

- 月の番号を `monthIndex = 歳 * 12 + (月 - 1)` で表す（`GameDate.FromAgeDays` から計算）。
- 申請したときの `monthIndex` を `appliedMonthIndex` に覚え、`OnMonthChanged` で「今の monthIndex > appliedMonthIndex」なら払う。
- → 0歳6月に申請すると7月分から。**遅れた分はさかのぼらない**（児童手当と同じ）。

### 2.3 毎年の手続きの1回目の期限（2026-10-08 決定：案C）

Yearly の制度を申請（または支給停止から再開）したとき、最初の期限をどの歳の○月○日にするか。

- **その歳の期限まで `renewWindowDays`（既定31日）より長く残っていれば → その歳の期限が最初。**
- 残りが31日以下、またはその歳の期限をもう過ぎていれば → 次の歳の期限が最初。

| 毎年8月31日の制度を申請した日 | 最初の期限 |
|---|---|
| 0歳1月10日 | 0歳8月31日 |
| 0歳7月20日 | 0歳8月31日 |
| 0歳8月20日（残り11日） | 1歳8月31日 |
| 0歳9月5日（過ぎている） | 1歳8月31日 |

- 現実（受給が決まった年も8月に現況届が要る）に近く、申請した直後にすぐ手続きを求められることもない。
- 検討した他の案：A 必ず次の歳（申請時期で次の期限まで12〜20か月とばらつく）／B 現実どおりその歳（直前に申請すると数日後にまた手続き）。
- 実装：`SupportManager.FirstRenewedYear`。

---

## 3. 支給の決め方

### 3.1 効果のタイミング（2種類）

| `effectTiming` | 使う項目 | 例 |
|---|---|---|
| `OnApply`（その場で1回） | `effects`（今まで通り） | 出産育児一時金 |
| `Monthly`（受給中は毎月） | `monthlyTiers`（支給表・3.2）＋ `effects`（申請時に1回だけ起きること。手間のストレスなど。空でよい） | 児童手当 |

### 3.2 支給表（monthlyTiers）＝「何歳から何歳まで、毎月いくら」

毎月もらう制度は、**年齢の段ごとに支給額を並べた表**で持つ。Inspector で行を足していける。

```csharp
[System.Serializable]
public class PaymentTier
{
    public string  label;                 // 表示用。例 "3歳未満"（空なら年齢から自動）
    public GameDay fromAge;               // この日から（含む）
    public GameDay toAge;                 // この日まで（含む）
    public List<Effect> monthlyEffects;   // この段の毎月の効果。例 Money +15,000 / Household
    public List<RequirementSO> requirements; // この段に当てはまる追加条件（空＝なし）。所得で額を変えるとき用
}
```

例：児童手当（たたき台・2024年10月以降の実際の額にそろえた）

| label | fromAge | toAge | monthlyEffects |
|---|---|---|---|
| 3歳未満 | 0歳1月1日 | 2歳12月31日 | Money +15,000 |
| 3歳〜高校生年代 | 3歳1月1日 | 18歳3月31日 | Money +10,000 |

**毎月の支給の決め方**（`OnMonthChanged` のたび）：

1. 今日の通算日数が `fromAge`〜`toAge` に入っている段を上から探す。
2. その段の `requirements` を**全部**満たしていれば、その段の `monthlyEffects` を適用して終わり。満たしていなければ次の段を探す。
3. 当てはまる段がなければ、その月は 0（状態は Active のまま）。
4. 今日がすべての段の `toAge` より後（`covers` があればその期間も過ぎた後）なら Ended。
5. 支給表が空で `covers` だけの制度（無償化など）は、お金は払わず、受給中のあいだ支出を肩代わりする（[expenses.md](expenses.md) 4章）。申請は Monthly と同じく**翌月分から**効く。

- こうすると「年齢で額が変わる」も「所得で額が変わる」も同じ表で書ける。
  - 例：所得で減額 → 同じ年齢の段を2つ並べ、上の段に「所得が○円以下」、下の段に条件なしで少ない額を入れる。
- 受給終了は**表の最後の `toAge`** で決まるので、前の版の `hasEndAge` / `endAge` は**作らない**（表に一本化）。
- 支給停止中は、本来払うはずだった段の `monthlyEffects` を `owedPayments` に積んでおき、再開時にまとめて適用する。
- **これは「お金が毎月入る」ので、effects.md 3章の継続効果（`ThisMonth` / `Days`、`ModifierRegistry`）は使わない。** 毎月 `Instant` の効果を1回ずつ適用するだけなので、今ある `EffectApplier` だけで作れる。

---

## 4. 申請条件（後から種類を足せる形）

### 4.1 考え方

条件は「子どもが○歳以上」「年収が○円以下」「ひとり親」など、**今後どんどん増える**。だから制度のコードに `if` を書かず、**条件を1つ1つの部品（ScriptableObject）にして、制度にリストで持たせる**。丸山さんの `EventConditionSO`（イベントの発生条件）と同じ形。

```csharp
/// 申請条件の部品。新しい条件はこれを継承したクラスを1つ足すだけ。
public abstract class RequirementSO : ScriptableObject
{
    /// 条件を満たしているか
    public abstract bool IsSatisfied(GameContext ctx);

    /// カードに出す説明。例 "年収 960万円以下"
    public abstract string Describe();
}
```

- **支出（[expenses.md](expenses.md)）でも同じ条件の部品を使う**ので、名前に Support を付けない（`RequirementSO` / `GameContext`）。
- **名前は Requirement にする。** `Condition` はすでに「状態（風邪気味など）」の意味で `ConditionDefinition` / `ConditionSystem` が使っているため（traits.md）。
- 制度側（`SupportScheme`）には `public List<RequirementSO> applyRequirements;` を足す。**全部満たしたら**申請できる（AND）。

### 4.2 条件が読む値は GameContext にまとめる

条件の部品が `GameState` や `Household` をあちこち直接読むと、値の置き場所が変わったときに全部直すことになる。**条件が読む値は1つの入れ物に集める。**

```csharp
/// 申請条件の判定に使う値。値の取り方が変わってもここだけ直せばよい。
public class GameContext
{
    public int   ChildAgeDays;      // 子どもの年齢（通算日数）← GameState.AgeDays
    public int   AnnualIncome;      // 世帯の年収 ← IncomeHistory.AnnualIncome（4.2.1・決定）
    public int   Money;             // 所持金 ← GameState.Money
    // 今後足すもの：子どもの人数、ひとり親か、住んでいる自治体、親の雇用形態…

    public static GameContext Current() { /* 今のゲームから値を集めて返す */ }
}
```

`SupportManager` が判定のたびに `GameContext.Current()` を作って条件に渡す。

#### 4.2.1 年収の決め方（2026-10-05 決定：実際に入った給料）

**年収＝直近12か月に実際に受け取った給料（母＋父）の合計。** 役職だけで決まる月給×12 ではなく、ワークライフバランスで増減した実際の額を使う。ライフ寄りにすると年収が下がり、所得条件のある制度に入る／保育料が下がる、という働き方との駆け引きが生まれる。

- 給料日（`HouseholdIncome` が `CollectWage()` で受け取るとき）に、その月の受取額を `IncomeHistory`（Core）に記録する。
- `IncomeHistory` は直近12か月ぶんを持つ（古いものから捨てる）。セーブ対象。
- 12か月たまっていない間（ゲーム開始直後）は「記録した月の平均 × 12」。1か月も無いときは「両親の `MonthlySalary` の合計 × 12」。
- 額面（税金を引く前）で数える。現実の所得制限も「所得」で判定するが、ゲームでは年収の目安に置き換える。

### 4.3 最初に作る条件（2つ）

| クラス | Inspector の項目 | 判定 | Describe の例 |
|---|---|---|---|
| `ChildAgeRequirementSO` | `GameDay minAge`, `GameDay maxAge` | 子どもの年齢が範囲内 | 「子どもが 3歳〜5歳」 |
| `IncomeRequirementSO` | `int maxAnnualIncome`（＋任意で `minAnnualIncome`） | 世帯年収が範囲内 | 「年収 910万円以下」 |

アセットは Project ウィンドウで右クリック → Create → 子育て → 申請条件 → ○○ で作り、制度の `applyRequirements` や支給表の段の `requirements` にドラッグする。同じ条件アセットを複数の制度で使い回してよい。

### 4.4 条件を1つ足す手順（今後）

1. `RequirementSO` を継承したクラスを1つ作る（`IsSatisfied` と `Describe` を書く）。
2. 読みたい値が `GameContext` に無ければ、項目を1つ足して `Current()` で値を入れる。
3. アセットを作って制度に付ける。

**`SupportManager` やカードのコードは触らない。**

### 4.5 開始日と年齢条件の違い

- 開始日・期限（support-systems.md 3.5）は「**いつからいつまで申請を受け付けるか**」＝カレンダーに載る締め切り。
- 年齢条件は「**誰が対象か**」。例えば幼保無償化は「3歳〜5歳の子」が条件で、締め切りとは別。
- 迷ったら：カレンダーに予定として出したいものは開始日・期限、出さなくてよいものは条件。

---

## 5. データのまとめ

### 5.1 SupportScheme に足す項目（support-systems.md 3.5 の形に追加）

```csharp
[Header("表示")]
public SupportCategory category;   // 政府 / 地方自治体 / 会社 / アプリ（タブ用）

[Header("申請条件")]
public List<RequirementSO> applyRequirements;   // 全部満たしたら申請できる（空＝条件なし）

[Header("毎月の支給（Monthly のとき）")]
public List<PaymentTier> monthlyTiers;   // 3.2 の支給表

[Header("支出の肩代わり（受給中のあいだ有効）")]
public List<ExpenseCover> covers;   // 無償化など。expenses.md 4章

[Header("毎年の手続き（Yearly のとき）")]
[Min(1)] public int renewWindowDays = 31;   // 期限日の何日前から［更新する］を押せるか
```

### 5.2 実行中の状態（セーブ対象）

アセットには書かない（support-systems.md 3.5）。`SupportManager` が `id` をキーにした辞書で持つ。

```csharp
[System.Serializable]
public class SupportSchemeRuntime   // そのまま SaveData に入れられる形
{
    public string       id;
    public SupportState state;
    public int  appliedAgeDays = -1;     // 申請した日（未申請は -1）
    public int  appliedMonthIndex = -1;  // 申請した月（「翌月から」用）
    public int  lastPaidMonthIndex = -1; // 最後に払った月（同じ月に2回払わないため）
    public int  lastPaidAmount = 0;      // 最後に払ったお金（カード表示用）
    public int  renewedYear = -1;        // 毎年の手続きを済ませた歳（Yearly）
    public int  suspendedAgeDays = -1;   // 支給停止になった日
    public List<int> owedTierIndexes = new List<int>(); // 支給停止中に払うはずだった段（再開時にまとめて払う）
    public bool expiredNotified = false; // 期限切れ通知を出したか
}
```

- `SaveData` に `public List<SupportSchemeRuntime> supportSchemes` を足す（セーブ本体は山口）。
- ロード時、アセットにあってセーブに無い制度は「今日の日付と条件から計算した状態」で作る（制度を後から追加しても壊れない）。
- 申請条件・支給表は**セーブしない**（アセットにあるので、毎回その場で判定する）。

---

## 6. SupportManager（Core）

置き場所：`Assets/Yamaguchi/Scripts/Core/`

| メソッド／イベント | 内容 |
|---|---|
| `static SupportManager I` | シングルトン |
| `IReadOnlyList<SupportScheme> Schemes` | 一覧（Inspector の `schemes`、並び順＝表示順） |
| `IEnumerable<SupportScheme> GetByCategory(SupportCategory c)` | タブ用 |
| `SupportState GetState(s)` / `SupportSchemeRuntime GetRuntime(s)` | 状態と詳細 |
| `IEnumerable<RequirementSO> UnmetRequirements(s)` | 満たしていない条件（カードに「条件：年収910万円以下」と出す用） |
| `PaymentTier CurrentTier(s)` / `PaymentTier NextTier(s)` | 今の段・次の段（「3歳から 月10,000」と出す用） |
| `bool CanApply(s)` / `CanRenew(s)` / `CanResume(s)` | ボタンを押せるか |
| `bool Apply(s)` / `Renew(s)` / `Resume(s)` | 申請・毎年の手続き・再開 |
| `int DaysUntilDeadline(s)` / `int NextDeadlineAgeDays(s)` | 期限まであと何日／次の期限の日（カレンダー用） |
| `event OnStateChanged(scheme, state)` / `OnPaid(scheme)` / `OnExpired(scheme)` | 通知 |
| `ToSave()` / `Restore(list)` | セーブ・ロード |

中でやること：

- `Start`：`DayTicker.I.OnDayPassed` と `MonthTicker.I.OnMonthChanged` を購読（`OnDestroy` で解除）。全制度の状態を今日で1回計算。
- `OnDayPassed`：2.1 の表のうち日付と条件で変わるものを判定。
- `OnMonthChanged`：Active の Monthly 制度に 3.2 の手順で払う。Suspended なら払うはずだった段を `owedTierIndexes` に積む。
- 効果の適用は `EffectApplier.Apply(...)` だけ。時間を自分で数えない。

---

## 7. 画面：申請前・申請後に出すもの

### 7.1 カード1枚のレイアウト

```
┌──────────────────────────────────────────────────────┐
│ [icon]  児童手当                         [状態バッジ]  │
│         高校生年代まで毎月もらえる                     │
│         (緑)毎月 お金 +15,000  (赤)申請が遅れた月はもらえない │
│         📅 3歳未満 月15,000 → 3歳〜18歳 月10,000      │
│         ⏰ 申請期限：0歳 4月30日まで（あと12日）  [申請する]│
└──────────────────────────────────────────────────────┘
```

今の card（icon / title / desc / btn_apply）に **状態バッジ・効果チップ・支給表の行・期限の行** を足す。高さは 80 → 120 程度（たたき台）。

### 7.2 状態ごとの表示

| 状態 | バッジ | 期限の行 | ボタン |
|---|---|---|---|
| Locked（開始日前） | グレー「まだ対象外」 | 「1歳 4月1日から申請できます」 | 非表示 |
| Locked（条件未達） | グレー「条件を満たしていません」 | 「条件：子どもが3歳〜5歳／年収910万円以下」（満たしていない条件だけ `Describe()` を並べる） | 非表示 |
| Available | オレンジ「申請できる」 | 「申請期限：0歳 4月30日まで（あと12日）」／期限なしは「いつでも申請できます」。**あと7日以下は赤字** | ［申請する］ |
| Received | 緑「受け取り済み」 | 「0歳 2月3日に受け取りました（+500,000円）」 | 非表示 |
| Active | 緑「受給中」 | 「今：月15,000（次回 0歳 5月1日）／3歳から 月10,000／18歳 3月31日まで」。今月が0円なら「今月は対象外（年収910万円以下）」 | Yearly の更新期間中だけ［更新する］ |
| Suspended | 赤オレンジ「支給停止」 | 「手続きすると再開（止まっている分 3か月）。資格がなくなるまで あと1年120日」 | ［手続きする］ |
| Expired | 赤「期限切れ」 | 「0歳 4月30日で締め切られました」 | 非表示 |
| Lost | 赤「受給資格なし」 | 「手続きがなかったため受給資格を失いました」 | 非表示 |
| Ended | グレー「受給終了」 | 「18歳 3月31日で受給が終わりました」 | 非表示 |

- 支給表の行は `monthlyTiers` から自動で作る（`label` があればそれ、無ければ年齢から「0〜2歳」）。手書きしない。
- 日付は `GameDate.Format` を使う。
- 効果チップは effects から自動生成（effects.md 5章）。丸山さんの `EffectChipUI` が使えるなら流用。

### 7.3 申請の確認ダイアログ

```
児童手当を申請しますか？
  もらえるもの：毎月 お金 +15,000（来月から・3歳から 月10,000）
  かかるもの　：親ストレス +3（手続きの手間）
  注意　　　　：申請が遅れた月の分はさかのぼってもらえません
        [やめる]  [申請する]
```

### 7.4 申請した直後

1. `SupportManager.Apply` → `OnStateChanged`。カードが自分の表示だけ更新（作り直さない）。
2. 短い通知（2秒）：OnApply「出産育児一時金を受け取りました　お金 +500,000」／Monthly「児童手当を申請しました　来月から毎月 +15,000」。
3. お金の表示は HUD が `OnMoneyChanged` で更新。

### 7.5 画面を閉じているとき

- `OnExpired` →「○○の申請期限が過ぎました」（1回だけ）。
- **毎月の支給は個別に通知しない**（2026-10-07 決定）。給料日に「給料が入りました」の通知を1つだけ出す（expenses.md 5.5）。支援の毎月の支給・金額が変わったこと（3歳で月10,000 になる等）は、通知ではなく**制度カードと家計簿で確認する**。
- Available の制度がある間、サポートボタンとタブに数字のバッジ。

### 7.6 時間を止める

サポート画面の `OnEnable` で `GameClock.I.PushPause()`、`OnDisable` で `PopPause()`（AGENTS.md ルール8）。

---

## 8. スクリプトの分け方

| ファイル | 置き場所 | 役割（これだけをやる） |
|---|---|---|
| `SupportScheme.cs` | Core | 制度データ（ScriptableObject）。3.5 ＋ 5.1 |
| `SupportCategory.cs` / `SupportState.cs` | Core | enum |
| `PaymentTier.cs` | Core | 支給表の1段（3.2） |
| `ExpenseCover.cs` | Core | 支出の肩代わり1件（expenses.md 4章） |
| `IncomeHistory.cs` | Core | 直近12か月の給料の記録と年収（4.2.1） |
| `SupportSchemeRuntime.cs` | Core | 1制度ぶんの実行中の状態（5.2）。セーブにもそのまま使う |
| `GameContext.cs` | Core | 条件が読む値の入れ物（4.2） |
| `RequirementSO.cs` | Core/Requirements | 申請条件の基底（4.1） |
| `ChildAgeRequirementSO.cs` / `IncomeRequirementSO.cs` | Core/Requirements | 最初の条件2つ（4.3） |
| `SupportManager.cs` | Core | 状態・申請・毎月の支給・期限判定（6章） |
| `SupportScheduleText.cs` | UI | 期限の行・支給表の行の文字列を作る static 関数だけ（7.2） |
| `SupportSchemeCard.cs` | UI | カード1枚の表示とボタン |
| `SupportPanelController.cs` | UI | タブ切替・カード生成・PushPause/PopPause |
| `SupportApplyDialog.cs` | UI | 確認ダイアログ |
| `SupportToast.cs` | UI | 申請直後・期限切れの通知（毎月の支給は通知しない） |

---

## 9. 最初に入れる制度（2026-10-05 決定の方針・数値は現実の2026年度）

方針（決定）：

- **金額は実際の円**。子育てにどれだけお金がかかるか・支援でどれだけ助かるかを学ぶため。
- **所得条件は現実どおり少なめ**（現実では児童手当も高校の就学支援金も所得制限が撤廃された）。
- **家族は父・母・子ども1人の3人**。ひとり親・子どもの人数は土台ができてから足す（`GameContext` に項目を足すだけ）。
- **無償化は「支出を減らす」形**で作る（[expenses.md](expenses.md) の支出を `covers` で肩代わりする）。お金を配る形にはしない。

### 9.1 お金をもらう制度

| id | 名前 | タイミング | 中身 | 申請できる期間 | 条件 |
|---|---|---|---|---|---|
| `birth_lump_sum` | 出産育児一時金 | OnApply | Money +500,000 | 0歳1月1日〜 1回 2歳1月1日（現実：出産の翌日から2年で時効） | なし |
| `pregnancy_support` | 妊婦のための支援給付（旧 出産・子育て応援給付金） | OnApply | Money +100,000、Stress +5 / Parents（面談の手間） | 0歳1月1日〜 1回 0歳3月31日（ゲーム用に短め） | なし |
| `child_allowance` | 児童手当 | Monthly | 支給表：0歳〜2歳12月31日 月15,000／3歳〜18歳3月31日 月10,000 | 0歳1月1日〜 期限なし（遅れた月はもらえない） | なし |

### 9.2 支出を減らす制度（covers）

| id | 名前 | 肩代わりする支出（expenses.md） | 期間 | 条件 | 現実との対応 |
|---|---|---|---|---|---|
| `preschool_free` | 幼児教育・保育の無償化 | `nursery_fee`（保育料）を全額。**給食費（`nursery_meal`）は対象外** | 3歳〜5歳 | 子どもが3〜5歳 | 給食費・延長保育は自己負担のまま、を体験させる |
| `hs_tuition_support` | 高等学校等就学支援金 | `hs_tuition`（高校授業料）を月9,900まで（公立の年118,800） | 15歳4月〜18歳3月 | 子どもが15歳4月以降 | 2026年度から所得制限なし。申請した月から（さかのぼらない） |

### 9.3 所得条件を試す制度（1つだけ）

| id | 名前 | 中身 | 条件 |
|---|---|---|---|
| `school_aid` | 就学援助（自治体） | `school_meal`（給食費）と `school_supplies`（学用品）を全額肩代わり | 子どもが6〜14歳／**年収 400万円以下**（自治体で差がある目安）。毎年4月30日までに申請し直し（Yearly） |

→ 基本給の夫婦（年収 約480万）がライフ寄りにすると年収が下がり、条件に入る。

### 9.4 確認すること

1. 出産育児一時金を申請 → その場で +500,000、「受け取り済み」。
2. 児童手当を申請 → 翌月から +15,000、3歳の月から +10,000（通知は出ない。カードと家計簿で確認）。
3. 支援給付を放置して 0歳4月1日 → 「期限切れ」、通知1回。
4. 3歳になったら無償化を申請 → 翌月から家計簿の保育料が 0 になり、給食費は残る。
5. 年収400万円を超えている間は就学援助が「条件を満たしていません（年収400万円以下）」。ライフ寄りにして年収が下がると申請できる。
6. セーブ → ロードで状態と給料の記録が戻る。

---

## 10. 決定事項のまとめ（2026-10-07 確定）

| 項目 | 決定 |
|---|---|
| 毎月の支給の通知 | 個別には出さない。給料日に「給料が入りました」だけ（7.5） |
| ボーナス | **入れない**。お金が増える仕組みは、イベントや頑張り（ワークライフバランス・昇進）を優先して作る |
| ゲーム用の期限の長さ（妊婦の支援給付 0歳3月31日 など） | **様子見**。動かしてから調整する |
| 申請条件とイベントの発生条件 | **別々のまま**（`RequirementSO` と丸山さんの `EventConditionSO` はまとめない） |
| 金額 | 現実の2026年度の値（9章） |
| 毎年の手続きの1回目の期限 | 案C（2.3）。2026-10-08 |
| タブの「申請できる」数（政府（3）） | **残す**（2026-10-08）。今のプログラムのまま |
| 会社の制度（育休・時短） | 段階的に作る → [company-support.md](company-support.md) |

**他のメンバーの合意が必要な項目**は [../team/agreements-needed.md](../team/agreements-needed.md) にまとめた。合意が取れたらそちらに日付を書き、関係する文書を直す。
