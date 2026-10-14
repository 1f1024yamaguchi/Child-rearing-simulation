---
title: 有坂さんへの依頼：食事のセーブ・ロード対応
updated: 2026-10-13
status: 確定（依頼前）
related: [docs/design/save-system.md, docs/design/food-system.md, docs/team/antigravity-save-system.md, docs/team/agreements-needed.md]
---

# 有坂さんへの依頼：食事のセーブ・ロード対応

> **3行まとめ**
> 1. セーブ機能ができたが、食事（`MealSystemManager`）の状態はまだ保存されていない。そのため **ロードすると食費が二重に引かれる** などのずれが出る。
> 2. 直し方は、`MealSystemManager` 自身に `ISaveParticipant` を付けて、8つの値を保存・復元するだけ。`SaveSystem` 側は何も直さなくてよい。
> 3. ロードしたときの食事画面は「**今月まだ選んでいないときだけ出す**」に決定済み（3章）。5章の依頼文を Antigravity に貼るか、4章のコードを自分で入れる。

agreements-needed.md の **A3** に当たる依頼。対象は `Assets/Arisaka/MealSystemManager.cs` だけで、山口は有坂さんのフォルダを触らない。

---

## 1. 今なにが起きているか

セーブは「お金・年齢・家族・支援制度…」を `save.json` に書き、ロードでそれを戻す。食事の状態は保存されていないので、ロードした瞬間の `MealSystemManager` は **Play を押したばかりの状態** のまま。そこから次のずれが出る。

| # | 起きること | 原因（保存されていない値） | 確認 |
|---|---|---|---|
| A | **ロードした翌日に、月初めからの食費がまとめて引かれる（二重払い）**。例：3月12日でセーブ → ロード → 翌日に「3月12日分 34,839円」（本来は 2,903円前後）。3/1〜3/11 はセーブ前に払っている | `paidThisMonth`・`currentMonthKey` | 2026-10-13 に Unity で再現 |
| B | ロード後の食事のランクが「普通」に戻る（高級・最安値を選んでいても） | `currentGrade` | コードから |
| C | 来月の食事画面で「先月に選択」の青枠が出ない | `lastChosenGrade` | コードから |
| D | 同じプレイ中に **前の月のセーブ** に戻ると、次の月初めに食事画面が出ない | `lastTriggeredMonthKey` | コードから |
| E | Play 直後（0歳1月1日）に出た最初の食事画面が、ロードしたあとも残る | ロード時に画面を閉じていない | 2026-10-11 に Unity で確認 |
| F | 月初めの自動セーブ（依頼3で作る予定）でセーブすると、「今月の画面は出したがまだ選んでいない」状態で保存される。ロードするとその月のプランを選べないまま進む | 「まだ選んでいない」という情報が無い | 自動セーブの作りから |

F について：自動セーブは「月が変わったフレームの最後（LateUpdate）」に走る。食事画面は `ProcessMonthlyRoutine` で **1フレーム待ってから** 出るので、自動セーブの時点では `lastTriggeredMonthKey` だけが今月になっていて、まだ選ばれていない。

---

## 2. 保存する値（8つ）

| 値 | 意味 | 保存しないとどうなるか |
|---|---|---|
| `currentGrade` | 今月の食事ランク | B |
| `lastChosenGrade` | 先月選んだランク（null = まだ一度も選んでいない） | C |
| `lastTriggeredMonthKey` | 食事画面を最後に出した月（`"歳_月"`） | D |
| `hasTriggeredInitial` | 最初の食事画面を出したか | Play 直後の画面が二重に出ることがある |
| `lastDeductedAgeDay` | 最後に食費を引いた日 | 同じ日を2回引くことがある |
| `currentMonthKey` | 今月の累計がどの月のものか | A |
| `paidThisMonth` | 今月すでに引いた食費の合計 | A |
| `selectionPending`（**新しく足す**） | 今月の画面は出したが、まだ選ばれていない | F |

`lastChosenGrade` は `MealGrade?`（null あり）なので、JsonUtility で保存するときは `int` にして **-1 = null** とする。

---

## 3. 決めたこと：ロードしたときの食事画面（2026-10-13 山口決定）

**今月の選択がまだのときだけ、食事画面を出し直す**（`selectionPending` が true のとき）。

| 検討した案 | 採らなかった理由 |
|---|---|
| ロードしたら毎回画面を出す | プランを選ぶとストレスなどの「選択時効果」がかかるので、ロードのたびに効果が二重にかかる |
| ロードでは一切出さない | 1章の F が残る（その月は前の月のランクのまま進む） |

すでに今月のプランを選んだ月にロードしたときは、画面は出ない。

---|---|---|---|
| **1（おすすめ）** | **今月の選択がまだのときだけ** 画面を出し直す（`selectionPending` が true のとき） | F が直る。選んだ月にロードしても画面は出ない | 特になし |
| 2 | ロードしたら毎回画面を出す | 作りが簡単 | プランを選ぶとストレスなどの「選択時効果」がかかるので、ロードのたびに効果が二重にかかる |
| 3 | ロードでは一切出さない | 一番簡単 | F が残る（その月は前の月のランクのまま進む） |


---

## 4. 作り方（`MealSystemManager.cs` への追加）

`MealSystemManager` の値は private なので、別のクラス（`MealSaveParticipant`）からは読み書きできない。**`MealSystemManager` 自身を参加者にする**（山口の `SupportManager` や `RunawayWatcher` と同じやり方）。

### 4.1 クラス宣言と登録

```csharp
public class MealSystemManager : MonoBehaviour, ISaveParticipant   // ← ISaveParticipant を足す
{
    // …既存のフィールド…
    private bool selectionPending = false;   // 今月の食事画面を出したが、まだ選ばれていない

    public string SaveKey => "meal";
    public int RestoreOrder => 100;          // 山口の分（10〜40）より後

    private void OnEnable()  => SaveSystem.Register(this);
    private void OnDisable() => SaveSystem.Unregister(this);
```

### 4.2 `selectionPending` を立てる・下ろす（既存メソッドに1行ずつ）

```csharp
// CheckAndTriggerInitialDialog の中、StartCoroutine の直前
selectionPending = true;

// TriggerMonthlyMealProcess の中、StartCoroutine の直前
selectionPending = true;

// OnPlanSelected の中（どこでもよい）
selectionPending = false;
```

### 4.3 Capture / Restore

```csharp
[System.Serializable]
private class MealSaveData
{
    public int    currentGrade;
    public int    lastChosenGrade = -1;      // -1 = null（まだ一度も選んでいない）
    public string lastTriggeredMonthKey = "";
    public bool   hasTriggeredInitial;
    public int    lastDeductedAgeDay = -1;
    public int    currentMonthKey = -1;
    public int    paidThisMonth;
    public bool   selectionPending;
}

public string Capture()
{
    var d = new MealSaveData
    {
        currentGrade          = (int)currentGrade,
        lastChosenGrade       = lastChosenGrade.HasValue ? (int)lastChosenGrade.Value : -1,
        lastTriggeredMonthKey = lastTriggeredMonthKey,
        hasTriggeredInitial   = hasTriggeredInitial,
        lastDeductedAgeDay    = lastDeductedAgeDay,
        currentMonthKey       = currentMonthKey,
        paidThisMonth         = paidThisMonth,
        selectionPending      = selectionPending
    };
    return JsonUtility.ToJson(d);
}

public void Restore(string json)
{
    if (string.IsNullOrEmpty(json)) return;
    var d = JsonUtility.FromJson<MealSaveData>(json);
    if (d == null) return;

    // ① Play 直後に予約・表示された食事画面を止めて閉じる（閉じると時間停止も解除される）
    StopAllCoroutines();
    var open = FindObjectOfType<MealSelectionDialogUI>();   // 表示中のものだけ見つかる
    if (open != null) open.gameObject.SetActive(false);

    // ② 値を戻す
    currentGrade          = (MealGrade)d.currentGrade;
    lastChosenGrade       = d.lastChosenGrade >= 0 ? (MealGrade?)d.lastChosenGrade : null;
    lastTriggeredMonthKey = d.lastTriggeredMonthKey ?? "";
    hasTriggeredInitial   = d.hasTriggeredInitial;
    lastDeductedAgeDay    = d.lastDeductedAgeDay;
    currentMonthKey       = d.currentMonthKey;
    paidThisMonth         = d.paidThisMonth;
    selectionPending      = d.selectionPending;

    // ③ 今月の選択がまだなら、画面を出し直す（3章の決定）
    if (selectionPending && GameState.I != null)
    {
        GameDate.FromAgeDays(GameState.I.AgeDays, out int y, out int m, out int _);
        StartCoroutine(ProcessMonthlyRoutine(y, m, isInitial: false));   // lastChosenGrade が null なら青枠なし
    }
}
```

**注意**
- `ISaveParticipant`・`SaveSystem` はグローバル名前空間にある（`Arisaka` 名前空間からそのまま使える）。
- ロードは `GameClock` を止めた状態で呼ばれるので、Restore の中で食費を引いたり画面の選択効果をかけたりしない。
- `SaveSystem` 側（山口）は何も直さない。登録するだけで `save.json` の `sections` に `"meal"` が入る。

---

## 5. Antigravity への依頼文（そのまま貼る）

```
AGENTS.md と .agents/rules/、docs/design/save-system.md、docs/team/request-arisaka-meal-save.md（この文書）を読んでから作業してください。

Assets/Arisaka/MealSystemManager.cs を、セーブ・ロードに対応させてください。
- MealSystemManager 自身に ISaveParticipant を実装する（別クラスは作らない）。SaveKey は "meal"、RestoreOrder は 100。
- OnEnable で SaveSystem.Register(this)、OnDisable で SaveSystem.Unregister(this)。
- request-arisaka-meal-save.md の 2章の8つの値を保存・復元する。selectionPending は新しく足し、4.2 のとおり立てる・下ろす。
- Restore は 4.3 のとおり：①予約・表示中の食事画面を止めて閉じる → ②値を戻す → ③selectionPending が true なら今月の食事画面を出し直す。
- lastChosenGrade（MealGrade?）は int で保存し、-1 を null とする。
- Assets/Arisaka 以外のファイルは変更しない（SaveSystem・ISaveParticipant も変更しない）。
- 終わったら、変更したファイルと、どの値をどう保存したかを短く報告してください。

Unity の MCP ツール（unityMCP）は使わないでください。Unity での確認は私が手で行います。
```

---

## 6. 確認手順（Unity）

セーブ・ロードは、シーンの `GameManager` に付いている `SaveDebugMenu` の右クリックメニュー（「Debug/セーブ」「Debug/ロード」）で行う。

1. Console にコンパイルエラーが無いことを確かめる。
2. Play → 最初の食事画面で **高級** を選ぶ → 3月の半ばくらいまで進める（2月・3月の画面でも高級を選ぶ）。
3. 「Debug/セーブ」→ Play を止める。
4. もう一度 Play → 最初の食事画面が出たら **選ばずに** そのまま「Debug/ロード」。
   - 食事画面が **消える**（E が直っている）。時間が止まったままにならない。
5. 1日進めて Console を見る。
   - 引かれる食費が **1日分（高級の日割り）** だけ。月初めからの分がまとめて引かれない（A が直っている）。
   - ランクが高級のまま（B）。
6. 次の月の1日まで進める。
   - 食事画面が出て、**高級に「先月に選択」の青枠** が付いている（C）。
7. 月初めに画面が出た状態で（まだ選ばずに）「Debug/セーブ」→ 止める → Play → 「Debug/ロード」。
   - 今月の食事画面が **もう一度出る**（F が直っている）。
   - 逆に、今月のプランを選んだあとでセーブ → ロードしたときは、画面が出ない。
8. `save.json`（場所は「Debug/セーブ」のログに出る）の `sections` に `"meal"` が入っている。

全部通ったら、agreements-needed.md の A3 を「済」にし、`docs/team/antigravity-save-system.md` 5章の「メンバー」の行を更新する（山口に連絡でもよい）。

---

## 7. 参考：山口側の状況

- セーブの仕組み（依頼1）と、家族・支援制度・年収・家出の保存（依頼2）は完了・確認済み（2026-10-13）。
- 依頼3（［セーブ］ボタン・月替わりの自動セーブ・ゲーム終了時の削除）はこれから。自動セーブが入ると 1章の F が起きるようになるので、**依頼3より先か同じころに** この対応を入れてもらえると助かる。
- カレンダー（草尾さん）にも同じ形で頼む予定（K7、SaveKey `"calendar"`）。
