---
title: 丸山さんへの依頼：イベントで選択肢を選んだことを外に知らせる通知
updated: 2026-11-09
status: 確定（依頼前）
related: [docs/design/review-records.md, docs/design/events.md, docs/team/agreements-needed.md]
---

# 丸山さんへの依頼：イベントで選択肢を選んだことを外に知らせる通知

> **3行まとめ**
> 1. リザルトの「振り返り」で **起きたイベントの一覧** を出す（[review-records.md](../design/review-records.md) 5章）。そのために、イベントで選択肢が選ばれたことを外から知る必要がある。
> 2. 今は選択肢を選んでも `Debug.Log` が出るだけで、外のスクリプトが受け取れる通知が無い。
> 3. `EventManager` に **static な通知 `OnEventResolved`** を1つ足し、`EventCardUI` から「どのイベントの、どの選択肢か」を渡して発火する。触るのは丸山さんのフォルダの2ファイルだけ。6章の依頼文を Antigravity に貼るか、4章のコードを自分で入れる。

agreements-needed.md の **M4** に当たる依頼。山口は丸山さんのフォルダを触らない。受け取る側（`LifeRecorder`）は山口が作る。

---

## 1. 今の流れ

```
EventManager.ShowDialog(demerit, merit)
  └ EventDialogUI.Show(...)
       ├ EventCardUI.Bind(demerit, 完了したら) ── 選択肢ごとに ChoiceBlockUI → EventChoiceButtonUI
       └ EventCardUI.Bind(merit,   完了したら)
                         ▼ ボタンを押す
EventChoiceButtonUI.OnButtonClicked
  ① EffectApplier.Apply(choice.effects, …)   ← ここで値が動く
  ② onChosen()  → EventCardUI.HandleChoiceSelected()（引数なし）
                    → EventDialogUI の「カードが終わった」→ 2枚とも終われば閉じる
```

- `EventCardUI.HandleChoiceSelected` は **どの選択肢が押されたかを知らない**（引数なしの `Action` で呼ばれる）。
- `EventDialogUI` も「終わった」ことしか知らない。
- だから外から「何のイベントで、何を選んだか」を受け取る方法が無い。

## 2. 方針

| 決めたこと | 理由 |
|---|---|
| 通知は `EventManager` に **static event** で置く | 受け取る側（`LifeRecorder`）は `EventManager.Instance` の有無やシーンの順番を気にせず購読できる。シーンをまたいでも購読が残らないよう、受け取る側が OnDisable で解除する |
| 発火するのは **`EventCardUI`**（選択肢が押されたとき） | イベントと選択肢の両方を知っているのはここだけ。`EventChoiceButtonUI`・`ChoiceBlockUI` のメソッドの形は変えずに済む |
| 発火のタイミングは **Effect を適用したあと** | 受け取る側が「選んだ結果の値」を見られるように。今の順番（①適用 → ②onChosen）のままで満たせる |
| 渡すのは **データの入れ物1つ**（`EventResolvedInfo`） | 後で項目を足しても、購読している側のコードを直さずに済む |
| 1枚のカードにつき **1回だけ** 発火 | 今の `isCompleted` の判定の後ろで呼ぶので、連打しても二重にならない |

### 渡す中身（EventResolvedInfo）

| 項目 | 型 | 内容 |
|---|---|---|
| `eventId` | string | `EventData.id` |
| `title` | string | `EventData.title`（記録の表示に使う） |
| `category` | `EventCategory` | 悪い／良い |
| `isFixed` | bool | 固定イベント（誕生日など）か |
| `choiceLabel` | string | 選んだ選択肢の `label`（「(−3,000円)」などの自動の付け足しは含めない） |
| `choiceIndex` | int | 何番目の選択肢か（0始まり） |
| `money` | int | 選択肢の `Money` の Effect の合計（助成を引く前の値。表示の目安） |
| `ageDays` | int | 選んだ日の年齢（`GameState.I.AgeDays` を切り捨て） |

> 医療費の助成で実際に引かれた額は Ledger が正しく持っている。`money` は「どんな選択だったか」を見せるための目安。

## 3. 変えないこと

- 抽選・予約・固定イベント・セーブ（`Capture`/`Restore`）・ダイアログの順番待ちの仕組み
- `EventChoiceButtonUI`・`ChoiceBlockUI` のメソッドの形（引数）
- `DebugDialogResolver`（山口）は今までどおりボタンを押すだけで、通知も出る

## 4. コード（参考）

### 4.1 EventResolvedInfo（新規）`Assets/Maruyama/Scripts/Events/EventResolvedInfo.cs`

```csharp
namespace Maruyama.Events
{
    /// <summary>イベントで選択肢が選ばれたときに外へ渡す情報。値だけの入れ物。</summary>
    public readonly struct EventResolvedInfo
    {
        public readonly string eventId;
        public readonly string title;
        public readonly EventCategory category;
        public readonly bool isFixed;
        public readonly string choiceLabel;
        public readonly int choiceIndex;
        public readonly int money;
        public readonly int ageDays;

        public EventResolvedInfo(string eventId, string title, EventCategory category, bool isFixed,
                                 string choiceLabel, int choiceIndex, int money, int ageDays)
        {
            this.eventId = eventId; this.title = title; this.category = category; this.isFixed = isFixed;
            this.choiceLabel = choiceLabel; this.choiceIndex = choiceIndex; this.money = money; this.ageDays = ageDays;
        }
    }
}
```

### 4.2 EventManager（追加のみ）

```csharp
/// <summary>
/// イベントで選択肢が選ばれ、その Effect を適用し終えたときに1回呼ばれる。
/// 振り返りの「できごとの記録」（Yamaguchi の LifeRecorder）が購読する。
/// static なので、購読した側は OnDisable で必ず解除すること。
/// </summary>
public static event System.Action<EventResolvedInfo> OnEventResolved;

/// <summary>EventCardUI から呼ぶ。EventData と選んだ選択肢から情報を組み立てて通知する。</summary>
public static void NotifyResolved(EventData data, EventChoice choice, int choiceIndex)
{
    if (data == null || choice == null) return;

    int money = 0;
    if (choice.effects != null)
        foreach (var e in choice.effects)
            if (e != null && e.kind == EffectKind.Money) money += Mathf.RoundToInt(e.value);

    int ageDays = GameState.I != null ? Mathf.FloorToInt(GameState.I.AgeDays) : 0;

    var info = new EventResolvedInfo(data.id, data.title, data.category, data.IsFixed,
                                     choice.label, choiceIndex, money, ageDays);
    OnEventResolved?.Invoke(info);
}
```

※ `EffectKind` は `Maruyama.Core`、`GameState` はグローバル名前空間（既存の using を確認して足す）。

### 4.3 EventCardUI（変更）

```csharp
private EventData boundData;   // 追加

public void Bind(EventData data, Action onCompleted)
{
    isCompleted = false;
    onCardCompletedCallback = onCompleted;
    boundData = data;          // 追加
    ...
    // 選択肢ブロックの動的生成
    for (int i = 0; i < data.choices.Count; i++)        // foreach → for に変える
    {
        var choice = data.choices[i];
        if (choice == null) continue;
        int index = i;                                    // ラムダで捕まえる用（ループ変数を直接使わない）
        var blockInstance = Instantiate(choiceBlockPrefab, choicesContainer);
        blockInstance.Setup(choice, data.title, () => HandleChoiceSelected(choice, index));
    }
}

private void HandleChoiceSelected(EventChoice choice, int index)   // 引数を足す
{
    if (isCompleted) return;
    isCompleted = true;
    ...（ボタンを非活性にする今の処理はそのまま）

    EventManager.NotifyResolved(boundData, choice, index);   // 追加：完了通知の前に呼ぶ
    onCardCompletedCallback?.Invoke();
}
```

- `EventChoiceButtonUI.OnButtonClicked` は「Effect を適用 → onChosen」の順なので、`NotifyResolved` は適用のあとに呼ばれる。
- 通知を **カードの完了通知より先** に呼ぶ。ダイアログが閉じると次のダイアログが出たり時間が動いたりするので、その前に記録させるため。

## 5. 確認

1. Play → `EventManager` の右クリック「今すぐ発生」系で悪い・良いを出す → 選択肢を押す。
2. 確認用に、どこかの MonoBehaviour で `EventManager.OnEventResolved += i => Debug.Log($"{i.title}／{i.choiceLabel}／{i.money}円／{i.ageDays}日");` を購読（確認が終わったら消す）。
3. カード1枚につきログが **1回だけ** 出る。2枚出たら2回。ボタンを連打しても増えない。
4. お金が動く選択肢で `money` がマイナスになり、所持金も今までどおり動く。
5. 固定イベント（誕生日など）で `isFixed = true`。
6. デバッグツールの「全部オート」でも同じログが出る。
7. コンソールにエラー・警告が無い。

---

## 6. Antigravity に貼る依頼文

```
あなたは Unity 製の子育てシミュレーションゲームのイベント機能の担当です。

【必ず最初に読むもの】
1. リポジトリ直下の AGENTS.md
2. docs/team/request-maruyama-event-notify.md（この依頼の仕様。2〜4章）
3. Assets/Maruyama/Scripts/Events/EventManager.cs、EventData.cs、EventChoice.cs
4. Assets/Maruyama/Scripts/UI/EventCardUI.cs、ChoiceBlockUI.cs、EventChoiceButtonUI.cs

【目的】
イベントで選択肢が選ばれ、Effect を適用し終えたことを、外のスクリプトが受け取れる通知を作る。
振り返り画面の「できごとの記録」に使う。受け取る側は別の担当（山口）が作るので、ここでは作らない。

【作るもの】
Assets/Maruyama/Scripts/Events/EventResolvedInfo.cs
- readonly struct EventResolvedInfo。項目は仕様書 2章の表のとおり
  （eventId / title / category / isFixed / choiceLabel / choiceIndex / money / ageDays）

【変更するもの】
Assets/Maruyama/Scripts/Events/EventManager.cs（追加のみ。既存の処理は変えない）
- public static event Action<EventResolvedInfo> OnEventResolved を追加
- public static void NotifyResolved(EventData data, EventChoice choice, int choiceIndex) を追加
  - data か choice が null なら何もしない
  - money は choice.effects のうち EffectKind.Money の value の合計（四捨五入して int）
  - ageDays は GameState.I.AgeDays を切り捨て（GameState.I が null なら 0）
  - choiceLabel は choice.label そのまま（ボタンに付け足している「(−3,000円)」などは含めない）
- XML コメントに「static なので購読した側は OnDisable で解除すること」と書く

Assets/Maruyama/Scripts/UI/EventCardUI.cs
- Bind で受け取った EventData をフィールド（boundData）に持つ
- 選択肢ブロックを作るループを for にし、各ブロックに
  () => HandleChoiceSelected(choice, index) を渡す（index はループの外に出したローカル変数で捕まえる）
- HandleChoiceSelected(EventChoice choice, int index) に引数を足す
- isCompleted の判定とボタンの非活性化は今のまま。そのあと、
  onCardCompletedCallback を呼ぶ「前」に EventManager.NotifyResolved(boundData, choice, index) を呼ぶ

【変えないもの】
- ChoiceBlockUI.cs と EventChoiceButtonUI.cs（メソッドの引数を変えない）
- 抽選・予約・固定イベント・ダイアログの順番待ち・セーブ（Capture / Restore）
- Assets/Yamaguchi/ Assets/Arisaka/ Assets/Kusao/ のファイル
- シーンファイル・プレハブ

【守ること】
- 通知はカード1枚につき1回だけ。isCompleted で止まったあとに呼ばない
- Effect の適用（EventChoiceButtonUI）より後に通知されること（今の呼び出し順のままで満たせる）
- コメントは日本語。既存コードの書き方に合わせる
- Unity の MCP ツール（unityMCP）は使わない

【終わったら報告すること】
- 変更・追加したファイルと、それぞれ何をしたか1行ずつ
- コンパイルが通ったこと
- 仕様書 5章の確認手順を人間が行えるよう、確認用の購読コードをどこに一時的に書けばよいか
```
