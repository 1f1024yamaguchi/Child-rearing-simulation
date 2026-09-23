---
title: カレンダー機能
updated: 2026-09-23
status: 設計中（たたき台・要合意）
related: [docs/design/support-systems.md, docs/design/events.md, docs/code/architecture.md, docs/code/ui-main-test-hud.md]
---

# カレンダー機能

> **このファイルは合意前のたたき台です。** 他の design ドキュメントと違い、話し合って決まった内容ではなく、担当（草尾）が決めるための出発点として書かれています。確定した項目から順に「たたき台」の断りを外していってください。
> 位置づけ：[support-systems.md](support-systems.md) 2.5章で整理した現実のデメリット（**①申請の手間 / ⑦手続きの継続**）を、プレイヤーが操作できる形にしたもの。

---

## 1. この機能が何のためにあるか

このゲームのテーマは「**知っていれば得をする**」。支援制度は申請しなければ1円ももらえず、現況届を出し忘れれば止まる（[support-systems.md](support-systems.md)）。

カレンダーは、その「知っている／忘れない」というプレイヤーの能力を**操作に落とし込む装置**です。

- 制度側から見ると：申請期限という締め切りが可視化され、「申請できる」状態を逃すと損をする。
- プレイヤーから見ると：実際のカレンダーアプリのように**自分でメモを書いて管理できる**。
- ゲーム体験として：時間はリアルタイムで流れ続ける（[../decisions/0001-realtime-time-model.md](../decisions/0001-realtime-time-model.md)）ので、「あの申請いつまでだっけ」と確認しに来る動機が自然に生まれる。

**やらないこと**：全部を自動で申請してくれる便利機能にはしない。それをやると「知っていれば得をする」というテーマが消える。

---

## 2. 画面の構成（たたき台）

`Main_test` 下部の「カレンダー」ボタンから開く（現在は `Debug.Log` の仮ハンドラ。→ [../code/ui-main-test-hud.md](../code/ui-main-test-hud.md)）。

```
カレンダー画面
├ ヘッダー        … 「3歳 5月」＋ 前月／翌月ボタン
├ 月グリッド      … 日付セル 7×6＝42マス固定（作り方は 6章）。セルの中に予定チップ（最大2〜3件＋「+2」）
│   └ 今日のセルを強調。期限が近い日は色を変える
├ 選択日の詳細    … その日の予定リスト（下部 or 右側）
│   └ 各予定：アイコン／タイトル／状態バッジ／[申請する] ボタン
└ メモ追加ボタン  … プレイヤーが自由に書き込む
```

- ゲーム内の暦は `GameDate` が持っている（0歳1月1日起点、1年365日、月の日数は実際の暦）。**自前で日付計算をしない**こと。→ [../code/architecture.md](../code/architecture.md) 2章
- **カレンダーを開いている間はゲーム内時間を止める**（決定済み）。ただし `SetRunning` を直接呼ばず、停止要求をカウントする共通の窓口（`PushPause` / `PopPause`）を使うこと。→ [../decisions/0004-pause-during-decisions.md](../decisions/0004-pause-during-decisions.md)

---

## 3. カレンダーに載るもの（たたき台）

| 種類 | 誰が入れる | 例 | 期限を過ぎると |
|---|---|---|---|
| 申請期限 | 自動（条件を満たすと出現） | 児童手当の申請、幼保無償化の手続き | その制度を受けられない／受給開始が遅れる |
| 定期手続き | 自動（受給を始めると毎年出現） | 現況届の提出 | 受給が停止・失効する |
| 予防接種 | 自動（子の年齢で出現） | 定期接種のスケジュール | 体調イベントの発生率が下がらないまま |
| 行事・イベント | 自動 | 運動会、学校行事 | 子の趣味・ストレスに影響 |
| プレイヤーのメモ | 手動 | 「来月は高級食にする」「保育園の見学」 | 何も起きない（自分用の備忘） |

- 自動で入る予定は、制度データ（[support-systems.md](support-systems.md)）側に「いつから／いつまで」を持たせて、カレンダーがそれを読む形にする。
- **手動メモは効果を持たない。** 効果を持たせると「メモしないと損」になり、作業感が出る。

---

## 4. 状態と締め切りの扱い（たたき台）

[support-systems.md](support-systems.md) 3章の状態表示と揃える。

| 状態 | 色 | 意味 |
|---|---|---|
| 条件未達 | グレー | まだ対象外（例：子が3歳になるまで） |
| 申請できる | オレンジ | 条件を満たした。**締め切りあり** |
| 受給中 | 緑 | 申請済みで効果が出ている |
| 期限切れ | 赤 | 逃した。損をした |

- 締め切りの数日前からセルの色を変える／バッジを出す（何日前かは**未決定**）。
- 期限切れになったら、逃したことがプレイヤーに分かるように一度だけ通知する。黙って消さない。

---

## 5. データの持ち方（実装の入口・たたき台）

```csharp
public enum CalendarEntryKind { Application, Renewal, Vaccination, Event, Memo }
public enum CalendarEntryState { Locked, Available, Active, Expired }

public class CalendarEntry
{
    public int  ageDays;        // GameDate に合わせて通算日数で持つ
    public CalendarEntryKind  kind;
    public CalendarEntryState state;
    public string title;
    public string note;         // プレイヤーのメモ
}
```

- 時間との接続：`MonthTicker.OnMonthChanged` で月表示を更新、`DayTicker.OnDayPassed` で締め切りの判定。**自分で時間を数えない。** → [../code/architecture.md](../code/architecture.md) 1章
- 作業場所は `Assets/Kusao/`。自分の UI はプレハブ化して、他のメンバーとシーンが衝突しないようにする。
- 予定リストの表示は、既存の `StatusListUI` / `StatusRowUI` と同じ「データ → 行を生成」の形にすると揃う。スクロールの組み方は [../code/ui-scroll-patterns.md](../code/ui-scroll-patterns.md)。

---

## 6. 画面の作り方（Qiita 記事の方式で作る）

> **月グリッドは、Qiita の記事「[Unity でカレンダーを作る（hire1105）](https://qiita.com/hire1105/items/b23e905bbcc062f70376)」の方式で作ることに決定。**
> ただし記事は `System.DateTime`（現実の暦）で日付を出しているので、このゲームでは日付計算を **`GameDate` に置き換える**。下の手順とコードはその置き換え済みのもの。

### 6.1 記事の方式の要点

1. 日付ボタン（`Button` ＋ 子に文字）を**プレハブ**にする。
2. `GridLayoutGroup` を付けた親オブジェクトの下に、そのボタンを **42個（7列 × 6行）** 生成する。並べるのは `GridLayoutGroup` に任せる。
3. 表示する月の **1日の曜日** を求め、その分だけ先頭のマスを空ける（＝前月の日付をグレーで入れる）。
4. 1日から月末まで日付を書き込み、余ったマスには翌月の日付をグレーで入れる。
5. 日曜は赤、土曜は青、それ以外は黒。
6. 当月の日付ボタンにだけ `onClick` を登録する（`RemoveAllListeners()` してから `AddListener`）。前月・翌月のマスは押しても何も起きない。

42マスで固定なのは、どの月でも「1日が土曜 ＋ 31日」まで収まる最大サイズだから。月ごとに行数を変えなくてよいので、作りが単純になる。

### 6.2 記事から変えるところ（なぜ変えるか）

| 記事 | このプロジェクト | 理由 |
|---|---|---|
| `DateTime.Now` / `DateTime.DaysInMonth` / `DayOfWeek` | `GameDate` を使う | ゲーム内の暦は「0歳1月1日起点・1年365日・うるう年なし」で現実の日付と無関係。自前計算も禁止（2章）。 |
| `UnityEngine.UI.Text` | `TextMeshProUGUI` | プロジェクトの他の UI（`DayUI` など）が TMP。 |
| 毎回 `GameObject.Find("GameObject")` で探す | 生成したボタンを `List` に持っておく | 名前に依存すると、他のメンバーのオブジェクト名と衝突して壊れる。毎回探すのも遅い。 |
| 曜日を `switch` で 0〜6 に変換 | マスの列番号 `i % 7` がそのまま曜日 | 1日の曜日ぶんずらして並べているので、列 0 = 日曜、列 6 = 土曜になる。 |
| 前月／翌月ボタンなし | ヘッダーに前月／翌月ボタンを付ける | 2章の画面構成に必要。記事には無いので自分で足す。 |
| `set_Date` で `Debug.Log` | 選択した日の**通算日数**をイベントで通知 | 5章のデータ（`CalendarEntry.ageDays`）と同じ単位にそろえる。 |

### 6.3 GameDate に足す関数（山口さんと要相談）

今の `GameDate` には「歳・月・日 → 通算日数」と「その月の日数」と「曜日」を出す関数が無い。カレンダー側で計算すると「自前で日付計算をしない」に反するので、`GameDate` に足してもらう（担当が山口さんなので相談してから）。

```csharp
// Assets/Yamaguchi/Scripts/Core/GameDate.cs に追加

// 生まれた日（0歳1月1日）の曜日。0=日 … 6=土。【たたき台】
public const int BirthDayOfWeek = 0;

// その月の日数（うるう年なし）
public static int DaysInMonth(int month)
{
    return MonthDays[month - 1];
}

// 歳・月・日 → 通算日数（FromAgeDays の逆）
public static int ToAgeDays(int year, int month, int day)
{
    int total = year * DaysPerYear;
    for (int i = 0; i < month - 1; i++) total += MonthDays[i];
    return total + (day - 1);
}

// 通算日数 → 曜日（0=日 … 6=土）
public static int DayOfWeek(int ageDays)
{
    return (ageDays + BirthDayOfWeek) % 7;
}
```

- ゲーム内の暦には本来「曜日」が無い。記事どおり土日を色分けするために、**生まれた日を何曜日とするか**だけ決めて 7 日周期で回す（7章の未決定事項）。

### 6.4 Unity での組み立て手順

作業場所は `Assets/Kusao/`。シーンに直接置かず、最後にプレハブ化する（5章）。

1. **日付ボタンのプレハブを作る**
   - Hierarchy で右クリック → UI → Button - TextMeshPro。名前を `DayButton` にする。
   - `Image` の Source Image は**白無地の画像**に差し替える（記事の注意点：既定の丸角画像が 42 個並ぶと見づらい）。
   - 子の `Text (TMP)` は中央寄せ、フォントサイズは適当に（セルに収まれば良い）。
   - `Assets/Kusao/Prefabs/DayButton.prefab` にドラッグして保存し、シーンからは消す。
2. **カレンダーのパネルを作る**（Canvas の下）
   ```
   CalendarPanel            … CalendarView.cs を付ける
   ├ Header                 … HorizontalLayoutGroup
   │  ├ PrevButton          … 「＜」
   │  ├ MonthLabel (TMP)    … 「3歳 5月」
   │  └ NextButton          … 「＞」
   ├ WeekdayRow             … GridLayoutGroup（7列）。日〜土の TMP を 7 個手で置く
   ├ DayGrid                … GridLayoutGroup。中身は空（スクリプトが 42 個生成する）
   └ CloseButton
   ```
3. **`DayGrid` の `GridLayoutGroup` を設定する**（数値は【たたき台】）
   - Constraint：**Fixed Column Count = 7**（これで必ず 7 列になる）
   - Cell Size：例 `100 × 80`、Spacing：例 `4 × 4`
   - Start Corner：Upper Left、Start Axis：Horizontal、Child Alignment：Upper Center
   - `WeekdayRow` も同じ Cell Size の幅・Spacing にしておくと、曜日と日付の列がそろう。
4. **`CalendarView.cs` を `CalendarPanel` に付けて、Inspector で参照を入れる**
   - Grid ← `DayGrid` / Day Button Prefab ← `DayButton` プレハブ / Month Label / Prev・Next Button
5. **開閉をつなぐ**：`Main_test` 下部の「カレンダー」ボタン（今は `Debug.Log` の仮ハンドラ）で `CalendarPanel.SetActive(true)`、`CloseButton` で `SetActive(false)`。
6. `CalendarPanel` を `Assets/Kusao/Prefabs/` にプレハブ化する。

### 6.5 スクリプト（たたき台）

```csharp
// Assets/Kusao/CalendarView.cs
// 方式：Qiita「Unity でカレンダーを作る」(hire1105) の 42 マス方式。
// 日付計算は System.DateTime ではなく GameDate を使う。
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CalendarView : MonoBehaviour
{
    private const int CellCount = 42;   // 7列 × 6行

    [Header("参照")]
    [SerializeField] private Transform grid;          // GridLayoutGroup を付けた DayGrid
    [SerializeField] private Button dayButtonPrefab;  // DayButton プレハブ
    [SerializeField] private TMP_Text monthLabel;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;

    [Header("色")]
    [SerializeField] private Color sundayColor     = Color.red;
    [SerializeField] private Color saturdayColor   = Color.blue;
    [SerializeField] private Color weekdayColor    = Color.black;
    [SerializeField] private Color otherMonthColor = Color.gray;
    [SerializeField] private Color cellColor       = Color.white;
    [SerializeField] private Color todayCellColor  = new Color(1f, 0.95f, 0.6f);

    /// <summary>日付マスが押されたとき。引数はその日の通算日数（GameState.AgeDays と同じ単位）。</summary>
    public event Action<int> OnDaySelected;

    private readonly List<Button>   cells  = new List<Button>();
    private readonly List<TMP_Text> labels = new List<TMP_Text>();

    private int viewYear;   // 表示中の歳
    private int viewMonth;  // 表示中の月（1〜12）

    private void Awake()
    {
        // 記事の Start() と同じ：ボタンを 42 個生成。ただし参照をリストに持っておく
        for (int i = 0; i < CellCount; i++)
        {
            Button cell = Instantiate(dayButtonPrefab, grid);
            cells.Add(cell);
            labels.Add(cell.GetComponentInChildren<TMP_Text>());
        }

        prevButton.onClick.AddListener(() => ShiftMonth(-1));
        nextButton.onClick.AddListener(() => ShiftMonth(+1));
    }

    private void OnEnable()
    {
        if (GameState.I == null) return;

        // 開いたら「今日」の月を表示する
        int day;
        GameDate.FromAgeDays(GameState.I.AgeDays, out viewYear, out viewMonth, out day);
        Refresh();

        // TODO: 時間を止める。PushPause / PopPause ができたら OnEnable / OnDisable で呼ぶ（0004）
    }

    private void ShiftMonth(int delta)
    {
        viewMonth += delta;
        if (viewMonth < 1)  { viewMonth = 12; viewYear--; }
        if (viewMonth > 12) { viewMonth = 1;  viewYear++; }
        if (viewYear < 0)   { viewYear = 0;   viewMonth = 1; }   // 生まれる前には戻れない
        Refresh();
    }

    // 記事の CalendarController() に相当
    private void Refresh()
    {
        monthLabel.text = viewYear + "歳 " + viewMonth + "月";

        int firstAgeDays = GameDate.ToAgeDays(viewYear, viewMonth, 1);
        int startday     = GameDate.DayOfWeek(firstAgeDays);          // 1日の曜日 = 先頭の空きマス数
        int monthEnd     = GameDate.DaysInMonth(viewMonth);
        int prevMonthEnd = GameDate.DaysInMonth(viewMonth == 1 ? 12 : viewMonth - 1);
        bool isFirstMonth = (viewYear == 0 && viewMonth == 1);         // 前月＝生まれる前
        int today        = Mathf.FloorToInt(GameState.I.AgeDays);

        for (int i = 0; i < CellCount; i++)
        {
            Button   cell  = cells[i];
            TMP_Text label = labels[i];
            cell.onClick.RemoveAllListeners();
            cell.image.color = cellColor;

            int dayNum = i - startday + 1;   // このマスが当月の何日か（範囲外なら前月・翌月）

            if (dayNum < 1)
            {
                // 前月の日付（グレー・押せない）
                label.text  = isFirstMonth ? "" : (prevMonthEnd + dayNum).ToString();
                label.color = otherMonthColor;
                cell.interactable = false;
            }
            else if (dayNum > monthEnd)
            {
                // 翌月の日付（グレー・押せない）
                label.text  = (dayNum - monthEnd).ToString();
                label.color = otherMonthColor;
                cell.interactable = false;
            }
            else
            {
                // 当月の日付
                int ageDays = firstAgeDays + (dayNum - 1);   // ラムダに渡すのでループ内のローカル変数にする
                int column  = i % 7;                          // 0=日 … 6=土

                label.text  = dayNum.ToString();
                label.color = column == 0 ? sundayColor
                            : column == 6 ? saturdayColor
                            : weekdayColor;
                if (ageDays == today) cell.image.color = todayCellColor;

                cell.interactable = true;
                cell.onClick.AddListener(() => OnDaySelected?.Invoke(ageDays));
            }
        }
    }
}
```

- `ageDays` をループ内のローカル変数にしているのは、ラムダがループ変数をそのまま掴むと全部のボタンが同じ日を返してしまうため（記事でも `tmp` に一度コピーしている）。
- 開いている間は時間が止まる前提（2章）なので、表示中に日付が進んだときの再描画は入れていない。止める仕組みが入る前に試すと「今日」の強調がずれるだけで、壊れはしない。
- 次の段階：`Refresh()` の当月マスで、その日の `CalendarEntry`（5章）を探して予定チップを出す。`OnDaySelected` を「選択日の詳細」パネルが購読して予定リストを出す。

---

## 7. 決めないといけないこと

- 締め切りの何日前から警告を出すか。
- 期限切れをどこまで厳しくするか（完全に受けられない／遅れて受給開始 など）。
- 自動で入る予定をどこまで最初から見せるか。全部見せると「知っていれば得をする」の驚きが減り、見せなさすぎると理不尽になる。
- プレイヤーのメモをセーブ対象にするか（現在の `SaveSystem` は4項目しか保存していない。→ [../code/architecture.md](../code/architecture.md) 5章）。
- 生まれた日（0歳1月1日）を何曜日とするか（`GameDate.BirthDayOfWeek`。6.3）。そもそも土日の色分けが要るか。
- `GameDate` への関数追加（6.3）を山口さんと合意する。
