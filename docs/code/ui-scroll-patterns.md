---
title: uGUI スクロールとステータス行の実装パターン
updated: 2026-09-19
status: 実装済み
related: [docs/design/support-systems.md, docs/code/architecture.md]
---

# uGUI スクロールとステータス行の実装パターン

> シーン `Assets/Yamaguchi/scenes/Main_test2.unity` で実装したスクロール UI の記録。
> **一覧やスクロールを作るときは、まずこのファイルの3階層構成に従うこと。自己流で組むとまず動かない。**
> このシーンは手置き UI（`MainTestHUD` は不使用）。

---

## 1. スクロールの作り方（uGUI の定石）

3階層セットで作る。**この形以外だとほぼ動かない。**

1. **ScrollView**（`ScrollRect`）… 見える窓の外枠。`horizontal=false` / `vertical=true`、`movementType=Elastic`、`scrollSensitivity=25`。
2. **Viewport**（`RectMask2D` ＋ α0 の `Image`）… ここで切り抜く。**`Image` が無いとドラッグを拾えない**ので α0 で置く。pivot は (0,1)。
3. **Content**（`VerticalLayoutGroup` ＋ `ContentSizeFitter`）… 中身。anchor は上辺ストレッチ、pivot (0.5,1)。`ContentSizeFitter.verticalFit = PreferredSize` にすると、カードを追加した分だけ自動で縦に伸びる ＝ **Content の高さ > Viewport の高さ になった時だけスクロールする**。
4. `ScrollRect` の `content` と `viewport` に 2・3 を割り当てる。スクロールバーを使うなら `verticalScrollbar` も。

- 子要素の高さは `LayoutElement.preferredHeight` で指定（`VerticalLayoutGroup.childControlHeight=true` とセット）。

---

## 2. 制度一覧のスクロール（`supports`）

子育て支援制度の申請画面（全画面オーバーレイ）。設計は [../design/support-systems.md](../design/support-systems.md)。

```
Canvas (ScaleWithScreenSize / ref 800x600 / match=0)
├ money（お金表示）
├ detail（SlidePanel＝右からスライドするステータス枠）
├ Buttons（suport / calendar / save / settings）
└ supports          ← 子育て支援制度の申請画面（全画面オーバーレイ）
   ├ background     （黒60%の暗幕）
   ├ supports_images（クリーム色のパネル 750x400）
   │  └ scheme_scroll ← 制度カードの縦スクロール
   │     ├ Viewport（Image α0 + RectMask2D）
   │     │  └ Content（VerticalLayoutGroup + ContentSizeFitter=PreferredSize）
   │     │     └ card_01 … card_08（Image + LayoutElement / icon・title・desc・btn_apply）
   │     └ Scrollbar_V（Sliding Area → Handle）
   ├ Text (TMP)（「子育て支援制度選択」）
   └ goverment（カテゴリタブ。将来 自治体／会社・仕事／アプリ を追加）
```

- 現状の実測：Viewport 682x295 / Content 682x758（カード8枚）→ スクロールする。
- **現状はカードの枠だけ。**中身は未実装。

### 今後
- カードの中身を [../design/support-systems.md](../design/support-systems.md) 準拠に（メリット緑チップ／デメリット赤チップ／状態バッジ 受給中・申請できる・条件未達）。
- カードをプレハブ化 → 制度データ（ScriptableObject か POCO のリスト）から動的生成。スクリプトは機能ごとに分ける想定：`SupportScheme`（データ）／`SupportSchemeCard`（カード表示）／`SupportPanelController`（タブ切替＋リスト生成）。
- `goverment` の横に 自治体／会社・仕事／アプリ のタブを並べ、押したら Content を作り直す。

---

## 3. 母親カードのステータス欄スクロール（`status_scroll`）

`Canvas/detail/mother` の「月給」表示を、**行を増やせる縦スクロール**に置き換えた。

```
Canvas/detail/mother
├ mother_back（ベージュの枠 200x250）
├ work life balance（Slider）
├ Strees（Slider + StressUI）
├ Image（区切り線 150x1）
└ status_scroll    ← 旧 monthly_salary を改造。anchoredPos (-505,-87) / size 160x126
   ├ Image（白背景。raycastTarget=true ＝ ドラッグを拾う）
   ├ ScrollRect（vertical のみ / Elastic / sensitivity 15 / バー表示=Permanent）
   ├ StatusListUI（行の追加・更新）
   ├ Viewport（α0 Image + RectMask2D / offset L4 R-12 T-4 B4 / pivot (0,1)）
   │  └ Content（VerticalLayoutGroup padding6 spacing4 + ContentSizeFitter=PreferredSize）
   │     └ row_salary（StatusRow プレハブのインスタンス。key="salary" 月給）
   └ Scrollbar_V（幅6 / Sliding Area → Handle）
```

- 旧「月給:」テキスト（`monthly_salary/Text (TMP)`）は行プレハブに置き換えたので削除済み。参照しているスクリプトは無かった。

### 行プレハブとスクリプト（機能ごとに分割）

| ファイル | 責務 |
|---|---|
| `Assets/Yamaguchi/Prefabs/UI/StatusRow.prefab` | `LayoutElement(preferredHeight=26)` ＋ 左 `label`（幅55%・左寄せ）／右 `value`（幅45%・右寄せ）。JPFont / 16pt / 黒 |
| `Assets/Yamaguchi/Scripts/UI/StatusRowUI.cs` | 1行の見た目だけ。`key`（シリアライズ）/ `Set(key,label,value)` / `SetLabel` / `SetValue` |
| `Assets/Yamaguchi/Scripts/UI/StatusListUI.cs` | 一覧の管理だけ。`SetStatus(key,label,value)`（未登録なら行を追加、登録済みなら更新）/ `SetValue(key,value)` / `RemoveStatus(key)` / `Clear()` / `ScrollToTop()`。シーンに最初から置いた行は `key` で自動的に辞書へ取り込む |

使い方：

```csharp
statusList.SetValue("salary", "18万円");            // 既存行の値だけ更新
statusList.SetStatus("health", "健康", "良好");      // 無ければ行を追加
```

### 今後
- 表示する項目（地位・子ストレス・成績など）が決まったら `StatusListUI.SetStatus` で追加するだけ。項目の定義は [../design/rules-and-stats.md](../design/rules-and-stats.md) 2章。
- 父親・子どものカードにも同じ `status_scroll` を複製すれば流用できる。

---

## 4. ハマりどころ（TextMeshPro）

- **`TextOverflowModes.Ellipsis` にすると文字が1文字も描画されない**（テキスト幅が枠を超えた時）。`Overflow`（既定）に戻すこと。メッシュもマスクも正常なのに真っ白になるので原因が分かりにくい。
- プレハブから複製した直後に TMP のメッシュが出ないことがあるため、`StatusRowUI.OnEnable` で `SetAllDirty()` ＋ `ForceMeshUpdate()` を呼んでいる。
- 検証は Play モードで `detail` を開き、位置 (350,80) に置いて実施（`SlidePanel.manualOpenPos`）。
