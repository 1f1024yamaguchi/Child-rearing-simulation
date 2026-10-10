---
title: セーブ機能の実装法と Antigravity への依頼文
updated: 2026-10-11
status: 確定（実装前）
related: [docs/design/save-system.md, docs/design/support-apply.md, docs/team/agreements-needed.md]
---

# セーブ機能の実装法と Antigravity への依頼文

> 仕様は [../design/save-system.md](../design/save-system.md)。この文書は **「どう作るか」と「Antigravity にどう頼むか」** を書く。
> 依頼は **3回に分けて** 頼み、1回ごとに Unity で「セーブ → 止める → もう一度 Play → ロード → 同じ状態か」を確かめてから次へ進む。

---

## 1. 決まっていること（おさらい）

| 項目 | 決定 |
|---|---|
| 保存するもの | お金・年齢、両親（ストレス・役職・バランス・積み立て中の給料・特性・状態）、子ども（ストレス・成績・体調・勉強⇔遊び・趣味・特性・状態）、支援制度、年収の記録、家出の数え |
| 自動セーブ | 月が変わったとき（給料・支給が終わったあと） |
| 手動セーブ | 画面下の［セーブ］ボタン |
| 続きから／はじめから | 今後作るタイトル画面に置く。それまでは確認用の右クリックメニューで「ロード」「セーブを消す」 |
| セーブ枠 | 1つ |
| 保存場所 | `Application.persistentDataPath/save.json`（1ファイル） |

---

## 2. 実装の全体像

### 2.1 部品と役割

```
SaveSystem（static）……… 参加者を集めて1つのファイルに保存・読み込みする係。ほかの人の型は知らない
 ├ 自分で戻すもの：年齢・お金・ゲームオーバー済みか（順番が大事なので SaveSystem が直接やる → 2.3）
 └ 参加者（ISaveParticipant）に任せるもの
     ├ FamilySaveParticipant   …… 母・父・子ども（ストレス・役職・積立・成績・特性・状態…）
     ├ IncomeHistory           …… 年収の記録（ToSave/Restore は作成済み）
     ├ SupportManager          …… 支援制度（ToSave/Restore は作成済み）
     ├ RunawayWatcher          …… 家出の数え
     ├ （有坂さん）MealSystemManager …… 食事（各自で作ってもらう）
     └ （草尾さん）カレンダー       …… メモ・予定（各自で作ってもらう）
AutoSaver（GameManager に付ける）…… 月替わりの自動セーブ
SaveDebugMenu（GameManager に付ける）… 確認用：右クリックで セーブ／ロード／セーブを消す
DefinitionCatalog（Resources）……… 特性・状態を id から探すための一覧
```

### 2.2 参加者の約束ごと

```csharp
public interface ISaveParticipant
{
    string SaveKey { get; }        // 例 "family" "support"。他と重ねない・後から変えない
    int    RestoreOrder { get; }   // 小さい順に Restore される（家族 10 → 年収 20 → 制度 30 → 家出 40 → 他の人 100〜）
    string Capture();              // 今の状態を JSON 文字列にして返す
    void   Restore(string json);   // JSON から戻す（セーブにそのキーが無ければ呼ばれない）
}
```

- 参加者は `OnEnable` で `SaveSystem.Register(this)`、`OnDisable` で `SaveSystem.Unregister(this)`。
- JSON にするのは `JsonUtility.ToJson(自分用の [Serializable] クラス)` でよい。

### 2.3 ロードの順番（一番大事）

`DayTicker` は「前の日から今日まで1日ずつ通知する」ので、年齢をいきなり戻すと **何百日分の給料・制度の判定・イベントが一度に起きる**。必ずこの順番にする。

1. `GameClock.I.PushPause()`（時間を止める）
2. `DayTicker.I.Resync(ageDays)` と `MonthTicker.I.Resync(ageDays)`（「今日はこの日」と教える。**通知は出さない**）
3. `GameState.I.LoadValues(...)` で年齢・お金を戻す
4. 参加者を `RestoreOrder` の小さい順に `Restore`
5. `GameClock.I.PopPause()`（時間を戻す）

### 2.4 特性・状態の保存

- 保存は **id** だけ（状態は id ＋ 残り日数）。
- 戻すときは `DefinitionCatalog`（`Assets/Yamaguchi/Resources/DefinitionCatalog.asset`）から id で探す。見つからない id は警告を出して飛ばす（止めない）。
- カタログはエディタのメニュー「子育て/セーブ/カタログを更新」で、プロジェクト内の `TraitDefinition` / `ConditionDefinition` を全部集めて作り直せるようにする。

### 2.5 自動セーブのタイミング

`MonthTicker.OnMonthChanged` を購読する部品は複数あり（給料・制度の支給・食事）、呼ばれる順番は決まっていない。自動セーブは **月が変わったフレームの最後（`LateUpdate`）** に1回だけ行い、給料・支給が終わったあとに保存されるようにする。

---

## 3. 依頼文（3回）

> 毎回、枠の中をそのまま Antigravity に貼る。最後の「MCP は使わない」の行は外さない（クレジット節約）。

### 依頼1：仕組み（SaveSystem・参加者・Resync）とお金・年齢

```
AGENTS.md と .agents/rules/、docs/design/save-system.md、docs/team/antigravity-save-system.md（この文書）の 2章を読んでから作業してください。
セーブ機能の土台を作ってください。置き場所は Assets/Yamaguchi/Scripts/Core/。1ファイル1クラス。

1. ISaveParticipant.cs（2.2 のとおり。SaveKey / RestoreOrder / Capture / Restore）
2. SaveData.cs：
   version（int, 1）、savedAt（現実の日時の文字列）、ageDays（float）、money（int）、isGameOver（bool）、
   sections（List<SaveSection>。SaveSection は key と json の2つの string）
3. SaveSystem.cs（今の SaveSystem.cs を作り直す。static class のまま）：
   - Register / Unregister（参加者の登録。重複しない）
   - Save()：GameState から ageDays・money・isGameOver を、登録された参加者から Capture() を集め、
     Application.persistentDataPath/save.json に書く。書くときは一時ファイルに書いてから置き換える（途中で落ちても壊れないように）
   - HasSave() / Delete()
   - Load()：2.3 の順番を必ず守る。
       ① GameClock.I.PushPause()
       ② DayTicker.I.Resync(ageDays) と MonthTicker.I.Resync(ageDays)
       ③ GameState.I.LoadValues(money, 今のストレス, ageDays, 今のバランス)
       ④ 参加者を RestoreOrder の小さい順に Restore（セーブにキーが無いものは飛ばす）
       ⑤ GameClock.I.PopPause()
     どこかで例外が出ても PopPause は必ず呼ぶ（try/finally）。
   - イベント OnSaved / OnLoaded を用意する。
   - 古い PlayerPrefs のセーブ（キー "childgame_save"）は使わない。Delete() のときに一緒に消す。
4. DayTicker.cs に public void Resync(int ageDays)：lastDay を ageDays にするだけ（通知しない）
5. MonthTicker.cs に public void Resync(float ageDays)：GameDate.FromAgeDays で lastYear / lastMonth を合わせるだけ（通知しない）
6. GameState.cs：LoadValues で isGameOver も戻せるようにする（引数を足す形でよい。今の呼び出しが壊れないようにオーバーロードで）
7. SaveDebugMenu.cs（MonoBehaviour。GameManager に付ける）：
   [ContextMenu] で「Debug/セーブ」「Debug/ロード」「Debug/セーブを消す」「Debug/セーブファイルの場所をログに出す」

守ること：
- お金や年齢は GameState の窓口メソッドで戻す。フィールドを直接書き換えない。
- 他のメンバーのフォルダ（Assets/Arisaka・Kusao・Maruyama）のファイルは変更しない。
- Unity の MCP ツール（unityMCP）は使わないでください。コードを書き終えたら、Unity で確認してほしいこと（Console のエラー、Play での確認手順）と、Inspector で必要な設定を箇条書きで教えてください。確認は私が Unity で行います。
```

**確認（依頼1）**
1. `GameManager` に `SaveDebugMenu` を付ける。
2. Play → 0歳3月ごろまで進める → `SaveDebugMenu` の右クリック「Debug/セーブ」。
3. Play を止める → もう一度 Play → すぐ「Debug/ロード」。
4. 日付とお金がセーブしたときと同じ。**ロード直後に給料がまとめて入ったり、月替わりの処理が何回も起きたりしていない**（Console のログを見る）。
5. 「Debug/セーブファイルの場所をログに出す」で出た場所に `save.json` があり、テキストで開ける。

---

### 依頼2：家族・特性と状態・年収・支援制度・家出の数え

```
docs/design/save-system.md 1.1 と、docs/team/antigravity-save-system.md 2.2〜2.4 を読んでから作業してください。
依頼1で作った ISaveParticipant を使って、山口の担当分を保存できるようにしてください。置き場所は Assets/Yamaguchi/Scripts/Core/（エディタ用は Assets/Yamaguchi/Scripts/Editor/）。

1. DefinitionCatalog.cs（ScriptableObject）：List<TraitDefinition> traits と List<ConditionDefinition> conditions。
   FindTrait(id) / FindCondition(id) を持つ。アセットは Assets/Yamaguchi/Resources/DefinitionCatalog.asset、Resources.Load で読む。
   エディタのメニュー「子育て/セーブ/カタログを更新」で、AssetDatabase.FindAssets でプロジェクト内の定義を全部集めて上書きする。
2. 値を戻すための窓口を足す（今あるメソッドの動きは変えない）：
   - PersonStatus：SetStress は既にある。特性・状態をまとめて入れ直す RestoreTraits(List<TraitDefinition>) / RestoreConditions(List<(ConditionDefinition, int 残り日数)>)
   - ParentStatus：RestorePendingWage(float)（積み立て中の給料）。役職は SetRank、バランスは SetBalance を使う
   - ChildStatus：RestoreValues(grade, health, studyPlayBalance, hobby, hobbyExp)（上限・下限は今の Clamp に合わせる）
   - RunawayWatcher：DangerDays の読み取りは既にある。RestoreDangerDays(int) と、hasRunAway の保存・復元
3. FamilySaveParticipant.cs（MonoBehaviour・GameManager に付ける。SaveKey "family"、RestoreOrder 10）：
   Household.I の母・父・子どもの値（save-system.md 1.1 の表のとおり）を保存・復元する。特性・状態は id で保存し、DefinitionCatalog で戻す。見つからない id は警告して飛ばす。
4. IncomeHistory を参加者にする（SaveKey "income"、RestoreOrder 20）：ToSave / Restore は既にあるので、それを JSON にする包み（List<int> を [Serializable] のクラスに入れる）を作る。
   IncomeHistory は MonoBehaviour ではないので、登録は FamilySaveParticipant と同じように GameManager に付ける小さな IncomeSaveParticipant.cs から行う。
5. SupportManager を参加者にする（SaveKey "support"、RestoreOrder 30）：ToSave / Restore は既にあるので、それを JSON にする包みを足す。OnEnable/OnDisable で登録。
6. RunawayWatcher を参加者にする（SaveKey "runaway"、RestoreOrder 40）。

守ること：
- 依頼1の SaveSystem は変えない（参加者を足すだけで保存されるはず）。
- 他のメンバーのフォルダのファイルは変更しない。
- Unity の MCP ツール（unityMCP）は使わないでください。終わったら、Unity で確認してほしいことと、Inspector で必要な設定（どのオブジェクトに何を付けるか、カタログの作り方）を箇条書きで教えてください。
```

**確認（依頼2）**
1. メニュー「子育て/セーブ/カタログを更新」を押し、`DefinitionCatalog` に「勉強が得意」「風邪気味」などが入ったか見る。
2. `GameManager` に `FamilySaveParticipant` と `IncomeSaveParticipant` を付けて保存（Ctrl+S）。
3. Play → 次の状態を作ってからセーブする。
   - 母のスライダーをワーク寄り、父はライフ寄り
   - 子どもに「風邪気味」を付ける（Inspector の testCondition などで）
   - 児童手当を申請し、1か月以上進めて「受給中」にする
   - 子どもの成績・体調がスタート時から変わるまで進める
4. Play を止める → もう一度 Play →「Debug/ロード」。
5. 両親のバランス・ストレス・積立、子どもの成績・体調・風邪気味の残り日数、児童手当の「受給中」と次回支給日、カードの表示が、セーブしたときと同じか見る。

---

### 依頼3：［セーブ］ボタン・月替わりの自動セーブ・ゲームが終わったときの削除

```
docs/design/save-system.md 3章と、docs/team/antigravity-save-system.md 2.5 を読んでから作業してください。

1. AutoSaver.cs（MonoBehaviour・GameManager に付ける）：
   MonthTicker.OnMonthChanged を購読し、フラグを立てるだけにする。LateUpdate でフラグが立っていたら SaveSystem.Save() を1回呼んでフラグを下ろす（給料・支給などが全部終わってから保存するため）。
   ゲームオーバー済み（GameState.IsGameOver）なら保存しない。
2. 画面下の［セーブ］ボタン（Canvas/Buttons/save）：
   押したら SaveSystem.Save() を呼び、「セーブしました」と短い通知を出す。通知は既存の SupportToast の Show(string) を使ってよい。
   onClick は実行時に AddListener で配線する（SaveButton.cs をボタンに付ける形）。
3. ゲームが終わったとき（GameState.OnGameOver・家出）にセーブを消す（SaveSystem.Delete()）。
4. 起動したときに自動でロードはしない（タイトル画面ができたら「続きから」でロードする）。

守ること：
- 他のメンバーのフォルダのファイルは変更しない。
- Unity の MCP ツール（unityMCP）は使わないでください。終わったら、Unity で確認してほしいことと、Inspector で必要な設定を箇条書きで教えてください。
```

**確認（依頼3）**
1. `GameManager` に `AutoSaver`、`Canvas/Buttons/save` に `SaveButton` を付けて保存。
2. Play → ［セーブ］→「セーブしました」が出る。`save.json` の更新日時が変わる。
3. 月が変わるたびに `save.json` が更新される（Console に「セーブしました」のログ）。
4. 月が変わった直後にロードしても、その月の給料が二重に入らない。
5. 家出・クリアでゲームが終わると `save.json` が消える。

---

## 4. 他のメンバーへの伝え方（有坂さん・草尾さん）

依頼1が終わったら、次の例を見せて「自分の分を参加者にしてほしい」と頼む（agreements-needed.md の A3・K7）。

```csharp
// 例：食事（有坂さん）の場合のひな形
public class MealSaveParticipant : MonoBehaviour, ISaveParticipant
{
    [System.Serializable]
    private class Data
    {
        public int  currentGrade;          // 今選んでいるランク
        public int  lastDeductedAgeDay;    // 最後に食費を引いた日
        public int  paidThisMonth;         // 今月引いた額
        public bool hasTriggeredInitial;   // 最初の食事選択を出したか
    }

    public string SaveKey => "meal";
    public int RestoreOrder => 100;

    private void OnEnable()  => SaveSystem.Register(this);
    private void OnDisable() => SaveSystem.Unregister(this);

    public string Capture()
    {
        var d = new Data { /* 自分の値を入れる */ };
        return JsonUtility.ToJson(d);
    }

    public void Restore(string json)
    {
        var d = JsonUtility.FromJson<Data>(json);
        /* 自分の値に戻す */
    }
}
```

- `SaveKey` は他と重ねない（食事 `"meal"`、カレンダー `"calendar"`）。
- `SaveSystem` 側は何も直さなくてよい。登録するだけで保存・ロードされる。
- 月の初めに食事選択の画面が出ている途中でセーブされることがある。ロードしたあとに選択画面をもう一度出すかどうかは有坂さんが決める。

---

## 5. 進み具合

| 依頼 | 内容 | 状態 |
|---|---|---|
| 1 | 仕組み・お金・年齢・Resync・確認用メニュー | 完了 |
| 2 | 家族・特性と状態・年収・支援制度・家出の数え | コード作成済み（Unity での確認待ち） |
| 3 | ［セーブ］ボタン・自動セーブ・終了時の削除 | 未着手 |
| メンバー | 食事（有坂さん）・カレンダー（草尾さん） | 依頼1のあとで依頼 |