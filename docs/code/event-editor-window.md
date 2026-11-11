---
title: イベントの編集ウィンドウ（エディタ拡張・段階②）
updated: 2026-11-11
status: 実装済み（山口）／Unity での動作確認待ち
related: [docs/design/event-editor.md, docs/code/support-editor.md, docs/design/ending.md]
---

# イベントの編集ウィンドウ（段階②）

メリット／デメリットイベント（`EventData`）の編集ウィンドウ、カードレイアウト（画像なし・選択肢1〜4）、新しい条件部品（8種）の実装。
設計：[../design/event-editor.md](../design/event-editor.md)。
開き方：メニュー **子育て → イベント → 編集ウィンドウ**、または `EventData` の Inspector の「編集ウィンドウで開く」。

---

## 1. 段階②で追加・変更したもの

### Assets/Maruyama/ の変更
- `EventData.cs`：`[TextArea] string imageNote`（絵の指示メモ）を追加。
- `EventManager.cs`：`public void Enqueue(EventData e)` を追加（カテゴリに合わせて片側に入れて pendingDialogs に積み、TryShowNext を呼ぶ）。
- `EventCardUI.cs`：
  - 画像が無いときは `iconImage.gameObject.SetActive(false)`（VerticalLayoutGroup で自動で詰まる）。
  - `showPlaceholderInGame` が ON かつ `imageNote` がある場合、差し替え枠＋指示メモを表示。
  - 選択肢4つのときは 2×2用コンテナ（`gridChoicesContainer`）、1〜3つのときは縦1列用（`choicesContainer`）を使い分け。5つ以上のときは先頭4つのみ表示し警告をログ出力。
- `ChoiceBlockUI.cs`：選択肢の数に応じた高さ（1〜2個: 90、3個: 72、4個: 90）を `LayoutElement.preferredHeight` に適用。

### 新しい条件の部品（`Assets/Yamaguchi/Scripts/Story/Conditions/`）
すべて `Maruyama.Events.EventConditionSO` を継承し、`IDescribable` を実装。

| クラス | 判定内容 | 説明文の例 |
|---|---|---|
| `DateRangeCondition` | 日付の範囲（両端を含む） | 6歳4月1日〜6歳7月31日 |
| `MonthCondition` | 指定した月のリスト（季節名自動判定） | 冬（12・1・2月） |
| `SchoolGradeCondition` | 4月1日時点の満年齢に基づく学年範囲 | 小3〜小6 |
| `ChildStatCondition` | 成績／体調／趣味Lv（範囲）、趣味の種類 | 成績 60〜100、趣味「スポーツ」 |
| `MoneyCondition` | 所持金が指定額以上／以下 | 所持金が 100,000円以下 |
| `SupportCondition` | 支援制度の受給中／受けていない | 制度「予防接種の補助」を受けていない |
| `TraitCondition` | 指定対象が特性／状態を持っている／いない | 子が「風邪気味」を持っている |
| `RequirementCondition` | 支援制度の申請条件（`RequirementSO`）を流用 | 年収 960万円以下 |

※ テストシーン等で `GameState.I` / `Household.I` / `SupportManager.I` が無い環境では、既存の条件と同様に `true` を返します。

### エディタ拡張（`Assets/Yamaguchi/Scripts/Editor/Event/`）
- `DateRangeConditionEditor.cs`：`SupportEditorGUI.DrawGameDay` を使用して埋め込み編集。
- `MonthConditionEditor.cs`：`SupportEditorGUI.DrawMonthToggles` を使用して1〜12月をトグルで選択。
- `ChildStatConditionEditor.cs` / `MoneyConditionEditor.cs`：項目に合わせた入力UIを提供。
- `EventBasicSection.cs`：`imageNote` の入力欄を追加。
- `EventDescriber.cs`：`IDescribable.Describe()` に対応し、各条件の説明文・型名を追加。
- `EventPlayModeSection.cs`：［今すぐ出す］を `EventManager.Instance.Enqueue(e)` に差し替え。
- `EventValidator.cs`：日付範囲の開始>終了チェック、指示メモを考慮した画像なし警告の判定を追加。
