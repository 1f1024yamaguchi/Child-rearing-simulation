---
title: 効果（Effect）の共通モデル
updated: 2026-09-21
status: 設計中（たたき台・要合意）
related: [docs/design/food-system.md, docs/design/events.md, docs/design/support-systems.md, docs/code/architecture.md]
---

# 効果（Effect）の共通モデル

> **数値も効果の種類も今後変わる前提**なので、「何が起きるか」を1つの共通の形で表し、後から種類を足せるようにする。
> 食事（有坂）・イベント（丸山）・制度（草尾）の3機能すべてが同じ形を使う。
> → 経緯は [../decisions/0006-effect-model.md](../decisions/0006-effect-model.md)

---

## 1. 考え方

食事・イベント・制度は、見た目も出るタイミングも違うが、やっていることは同じ。

> **プレイヤーが選択肢を1つ選ぶと、いくつかのステータスが動く。**

だからそれぞれが自分で `GameState.I.AddStress(+5)` のような呼び出しを書かず、**「何がどれだけ動くか」をデータとして持ち、適用は1箇所にまとめる**。

```
食事グレード（最安値/普通/高級）  ┐
イベントの選択肢（病院に行く など）├→ Effect のリスト →  EffectApplier  →  GameState ほか
制度（児童手当 など）             ┘                        （適用はここだけ）
```

こうすると、**効果の種類を足すときに触るのは `EffectApplier` の1箇所だけ**で、3人の機能すべてが新しい効果を使えるようになる。

---

## 2. Effect の形（たたき台）

```csharp
public enum EffectKind
{
    Money,          // お金の増減
    Stress,         // ストレスの増減
    Health,         // 体調
    Grades,         // 成績
    Hobby,          // 趣味
    GoodEventRate,  // Goodイベントの発生率補正（％）
    BadEventRate,   // Badイベントの発生率補正（％）
    // ここに足していく（例：Fatigue, Relationship, JobRankProgress …）
}

public enum EffectDuration
{
    Instant,        // その場で1回だけ
    ThisMonth,      // 今月のあいだ継続
    Days,           // days 日のあいだ継続
}

public enum EffectTarget
{
    Household,      // 世帯（お金・イベント発生率など、人に紐づかないもの）
    Mother,
    Father,
    Child,
    Parents,        // 母親と父親の両方に同じ量
    Everyone,       // 家族全員に同じ量
}

[Serializable]
public class Effect
{
    public EffectKind    kind;
    public EffectTarget  target = EffectTarget.Household;
    public float         value;      // 増減量。％のものは％ポイント
    public EffectDuration duration = EffectDuration.Instant;
    public int           days;       // duration == Days のときだけ使う
}
```

**選択肢はこれのリストを持つだけ。**

```csharp
[Serializable]
public class Choice
{
    public string label;          // 「病院に行く」
    public List<Effect> effects;  // お金 -8000 / 体調 +15 …
}
```

---

## 2.5 対象（target）の考え方

**ストレス・体調・成績・趣味は人ごとに持つ**（母親・父親・子ども）。だから効果には必ず対象が要る。→ [../decisions/0005-per-person-stress.md](../decisions/0005-per-person-stress.md)

| 効果 | target の例 |
|---|---|
| 食事（家族で同じものを食べる） | `Everyone` … 全員に**同じ量**が入る |
| 看病で親が疲れる | `Parents` または `Mother` / `Father` |
| 子どもの成功体験 | `Child` |
| お金・イベント発生率 | `Household`（人に紐づかない） |

`Everyone` を1行書けば全員に同じ量が入るので、食事のデータに3人ぶんの数値を並べる必要はない。「同じものを食べているのに人によってストレスの増減量が違う」という不自然さもここで防いでいる。

---

## 3. 適用するところ（1箇所にまとめる）

```csharp
public static class EffectApplier
{
    public static void Apply(IEnumerable<Effect> effects)
    {
        foreach (var e in effects)
        {
            switch (e.kind)
            {
                case EffectKind.Money:
                    GameState.I.AddMoney(Mathf.RoundToInt(e.value));   // 世帯のお金
                    break;
                case EffectKind.Stress:
                    foreach (var person in Resolve(e.target))          // 対象の人ぶん回す
                        person.AddStress(e.value);
                    break;
                // … 種類を足すときはここに1行足すだけ
            }
        }
    }
}
```

- 継続する効果（`ThisMonth` / `Days`）は、その場で足さずに**補正として登録し、期限が来たら外す**。登録の置き場所も1箇所にまとめる（`ModifierRegistry` など。名前は要相談）。
- イベントの発生率を決めるときは、登録されている `GoodEventRate` / `BadEventRate` の合計を足して使う。

---

## 4. 効果の種類を足す手順

これが「拡張性に配慮する」の中身。**新しい効果を足すときにやることは2つだけ**にする。

1. `EffectKind` に値を足す
2. `EffectApplier` に case を1つ足す

（対象の解決 `Resolve(target)` は共通なので、効果ごとに書く必要はない）

これで、食事のグレードにも、イベントの選択肢にも、制度にも、その効果を**データを書き換えるだけで**付けられる。各機能のコードには一切手を入れない。

> 逆に言うと、**各機能が `GameState` を直接叩き始めると、この利点が消える。** 効果はデータで持つこと。

---

## 5. 表示（チップ）も効果から作る

モックにある「体調 −15」「子ストレス +8」のようなチップは、**Effect のリストから自動で生成**する。手書きしない。

- 表示名（「体調」）と符号の色（プラス＝緑／マイナス＝赤、ただし**ストレスだけは逆**）は `EffectKind` ごとの表の1箇所で決める。
- 対象が特定の人なら「親ストレス −10」「子ストレス −5」のように出し、`Everyone` なら「ストレス −5」の1つにまとめる。
- こうすると、効果を足したときにチップも自動で増える。書き忘れて「効果はあるのに表示されない」事故が起きない。

---

## 6. 決めないといけないこと

- 名前（`Effect` / `EffectKind` / `EffectTarget` / `EffectApplier` / `Choice`）をこれで確定してよいか。3人が同じ名前を使う前提の土台なので、先に決めたい。
- データの持ち方：ScriptableObject にするか、コード内のリストにするか。バランス調整の頻度を考えると ScriptableObject が有利。
- 継続効果の置き場所と、セーブ対象にするか（現在の `SaveSystem` は4項目しか保存していない。→ [../code/architecture.md](../code/architecture.md) 5章）。
- 誰がこの土台を作るか。3機能の共通部分なので Core 側（山口）が自然。
