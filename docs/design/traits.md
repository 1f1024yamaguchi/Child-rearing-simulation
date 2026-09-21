---
title: 特性（Trait）と状態（Condition）
updated: 2026-09-21
status: 設計中（たたき台・要合意）
related: [docs/design/effects.md, docs/design/child-status.md, docs/design/events.md, docs/code/architecture.md]
---

# 特性（Trait）と状態（Condition）

> 「勉強が得意」「ストレスに強い」のような **特殊能力**と、「風邪気味」「絶好調」のような **一時的な状態**の持ち方を決める。
> どちらも母親・父親・子どもの **誰にでも付く**。付けるのは **イベント**（丸山担当）。
> → 経緯は [../decisions/0007-traits-and-conditions.md](../decisions/0007-traits-and-conditions.md)

---

## 1. 考え方

特殊能力も状態も、やっていることは同じ。

> **その人のステータスの動き方を、恒常的にずらす。**

食事やイベントの `Effect` が「その場で1回だけ値を動かす」のに対して、こちらは **掛かりっぱなしの補正**。
なので中身は同じ形にして、**違いは寿命だけ**にする。

| | Trait（特性・特殊能力） | Condition（状態） |
|---|---|---|
| 例 | 勉強が得意、ストレスに強い、要領がいい | 風邪気味、絶好調、寝不足 |
| 寿命 | **永続**（外れない） | **一時**（残り日数が 0 で外れる） |
| 付ける | イベント | イベント |
| UI | ステータス欄の「特性」行 | ステータス欄の「状態」行 |
| セーブ | 付いているものだけ | 付いているもの＋残り日数 |

**永続と一時は別のリストとして持つ。** 1本にまとめて「永続フラグ」で分けると、UI・日数処理・セーブのそれぞれで同じ分岐を書くことになるため。

---

## 2. 補正（StatModifier）の形

`effects.md` の `EffectKind` をそのまま使い、**加算か倍率かの区別**だけを足す。

```csharp
public enum ModifierOp
{
    Add,        // 加算。値をそのまま足す
    Multiply,   // 倍率。+0.05 で 5% 増し、-0.3 で 3割減
}

[Serializable]
public class StatModifier
{
    public EffectKind kind;   // Stress / Money / Grades / Hobby / Health / BadEventRate …
    public ModifierOp op = ModifierOp.Multiply;
    public float      value;
}
```

Trait も Condition も、持ち物は `List<StatModifier>` ひとつ。データとしての違いは **残り日数を持つかどうか**だけ。

### 書ける例

| 名前 | 種別 | 中身 |
|---|---|---|
| 勉強が得意 | 特性 | `Grades × +0.2` |
| ストレスに強い | 特性 | `Stress × −0.3`（上がる分だけ7割に） |
| 要領がいい | 特性 | `Money × +0.05` |
| 病気になりにくい | 特性 | `BadEventRate + −10`（%ポイント） |
| 風邪気味 | 状態（7日） | `Health + −1`（毎日）、`Grades × −0.5` |
| 絶好調 | 状態（3日） | `Grades × +0.5`、`Hobby × +0.5` |

---

## 3. どこで掛かるか（ここが肝）

補正は **ステータスの書き込み窓口の中**で掛ける。呼ぶ側は補正の存在を知らなくていい。

| 補正 | 掛ける場所 |
|---|---|
| Stress | `PersonStatus.AddStress()` の中。**`delta > 0` のときだけ**掛ける（上がりにくさであって、下がりにくさではない） |
| Grades | `ChildStatus.AddGrade()` の中。同じくプラスのときだけ |
| Hobby | `ChildStatus.AddHobbyExp()` の中 |
| Health | `ChildStatus.AddHealth()` の中 |
| Money | `ParentStatus.DailyWage` の計算の最後 |
| GoodEventRate / BadEventRate | イベント抽選のときに、家族全員ぶんを合算して使う |

計算の順番は **加算 → 倍率**。

```
最終値 = (元の値 + Add の合計) × (1 + Multiply の合計)
```

補正を集めるのは `PersonStatus` の1メソッド（Traits と Conditions の両方をなめて合計する）。
`effects.md` 3章にある継続効果の登録先（`ModifierRegistry` など）と同じ仕組みに乗せる。**Trait は「期限が来ない継続効果」でしかない。**

> **この窓口を迂回して値を直接書き換えると、特殊能力が黙って効かなくなる。**
> 各機能から `AddStress` などを必ず通すこと。AGENTS.md ルール4と同じ理由。

---

## 4. 付け方（イベントから）

特性も状態も、入手経路は **イベント** に一本化する。

`effects.md` の `EffectKind` に2つ足す。

```csharp
public enum EffectKind
{
    // 既存 …
    AddTrait,       // 特性を付ける
    AddCondition,   // 状態を付ける（days で日数を指定）
}
```

対象は既存の `EffectTarget`（`Mother` / `Father` / `Child` / `Parents` / `Everyone`）がそのまま使える。
どの特性・状態を付けるかは、Effect に **定義アセットへの参照**を1つ持たせて指定する。

```
「部活の大会で優勝した」 : [ AddTrait 自信がついた → Child, Stress -10 → Child ]
「家族で風邪をもらった」 : [ AddCondition 風邪気味(7日) → Everyone ]
「資格を取った」         : [ AddTrait 要領がいい → Mother, Money -50000 ]
```

残り日数を毎日1つ減らして、0 になったら外す処理は **1箇所にまとめる**（家族全員の Conditions をなめる）。Traits はなめる必要がない。

---

## 5. データの持ち方

`TraitDefinition` / `ConditionDefinition` を **ScriptableObject** にする。アセットを1つ増やすだけで新しい特性・状態が足せる。

| 項目 | Trait | Condition |
|---|---|---|
| id（プログラム用） | ○ | ○ |
| 表示名 | ○ | ○ |
| 説明 | ○ | ○ |
| 補正のリスト | ○ | ○ |
| 既定の持続日数 | — | ○ |
| バッジ色 | — | ○（将来カードに出す場合） |

**数値効果は定義アセットが持ち、`EffectApplier` 側には書かない。** 効果の種類を足すときに触るのは `EffectKind` と `EffectApplier` の1箇所だけ、という `effects.md` 4章の方針を崩さないため。

---

## 6. UI

画面での表示名は **「特性」**とする（「メリット」ではない。デメリットの特性も付けられるため）。

親カードの月給・日給・積立と同じ **スクロール欄**（`status_scroll` / `StatusListUI`）に入れる。
ゲームが進むにつれて特性・状態は増えるので、固定の枠にはしない。

### 行の書式

**1件につき1行。左に名前、右に効果（状態は残り日数）。**

```
月給       300,000      ← 既存の行
日給        10,000
積立        40,000
勉強が得意   成績+20%   ← 特性
要領がいい   給料+5%
風邪気味     あと53日    ← 状態
```

- 行のキーは `trait_<id>` / `condition_<id>` のように1件ごとに分ける。
  `StatusListUI.SetStatus` で追加し、外れたら `RemoveStatus` で消す。
- 0件のときは行を出さない（「なし」という行も出さない）。
- 右に出す効果の文字列は、補正のリストから組み立てる。倍率は `+20%`、加算は `+15` の形。
  補正が複数ある特性は代表の1つを出し、残りは `他+1` のように添える。

`effects.md` 5章の「チップは Effect から自動生成」と同じ考え方で、効果の文字列を作る処理は1箇所にまとめる。

---

## 7. 決めないといけないこと

- 名前を `Trait` / `Condition` / `StatModifier` / `ModifierOp` で確定してよいか。`effects.md` 6章の名前決めと一緒に。
- **バランス**：特性は強すぎると他の要素が意味を失う。倍率の上限（例：±30%まで）を決めるか。
- 同じ特性が重複して付いたときの扱い（重ねる／無視する）。
- セーブ対象にするか。現在の `SaveSystem` は4項目しか保存していない。→ [../code/architecture.md](../code/architecture.md) 5章
- ゲーム開始時に特性を持たせるか（いまは「イベントからのみ」で確定）。
