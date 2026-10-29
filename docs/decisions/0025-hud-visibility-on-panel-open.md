---
title: サポート画面・人物詳細パネル表示時のHUDボタン排他非表示制御
updated: 2026-10-29
status: 採用
---

# サポート画面・人物詳細パネル表示時のHUDボタン排他非表示制御

## 背景
1. **サポート画面の被り**:
   - サポート画面（`Canvas/supports`）は画面全体を覆うUIだが、下部ボタン群（`Canvas/Buttons`）や画面上部の倍速ボタン（`Canvas/GameSpeedUI`）が表示されたまま残っており、サポート画面のカードやタブ、閉じるボタンと重なって操作や視認性を妨げていた。
2. **人物詳細パネルと家計簿ボタンの競合**:
   - 右の矢印ボタンを押して家族ステータス（母親・父親・子どもカード）の詳細パネル（`Canvas/detail` / `SlidePanel`）を開いた際、画面左上に常設されている家計簿ボタン（`Canvas/Buttons/Kakeibo`）が残ったままになり、画面表示が煩雑になっていた。

## 決定
1. **`HUDVisibilityController` の導入**:
   - サポート画面および人物詳細パネルの状態を監視し、各UIの開閉に連動して不要なボタンを自動的に `SetActive(false)` / `SetActive(true)` で切り替える独立コントローラー（`Assets/Yamaguchi/Scripts/UI/HUDVisibilityController.cs`）を導入。
2. **制御ルール**:
   - **サポート画面が開いているとき**：
     - 下部ボタン群（`Canvas/Buttons`）および倍速ボタン（`Canvas/GameSpeedUI`）を非表示にする。
     - サポート画面が閉じたら、元の表示状態に復帰する。
   - **人物詳細パネルが開いているとき**：
     - 家計簿ボタン（`Canvas/Buttons/Kakeibo`）を非表示にする。
     - 人物詳細パネルが閉じたら、家計簿ボタンを再表示する。
     - サポート画面が開いている間は家計簿ボタンも非表示を維持する。
3. **`SlidePanel` へのイベント追加**:
   - `SlidePanel.cs` に `public event Action<bool> OnOpenChanged;` を追加し、パネル開閉のイベント通知をサポート。また、フォールバックとして毎フレームのステータス変化監視も併用。

## 検討した他の案と、採らなかった理由
- **案A: CanvasGroup のアルファゼロによる透過**
  - ボタンのRaycast遮断やアクティブ判定の整合性、既存の `fadein` / `fadeout` が `SetActive` 方式を採用している点から、プロジェクト全体で統一された `SetActive` 制御を採用した。
- **案B: サポート画面や詳細パネルのスクリプト内に個別ハードコードする**
  - `SupportPanelController` や `SlidePanel`、各ボタンの責務が混ざり、スパゲッティ化するため、表示制御を専門に担当する単一責務の `HUDVisibilityController` に分離した。

## 結果・影響
- サポート画面を開いた際、背後のボタン表示（下部ボタン群・倍速ボタン）が綺麗に消え、サポート画面の内容に集中できるようになった。
- 右の矢印で家族詳細を開いた際、家計簿ボタンが消えて画面がすっきり見やすくなった。
- スクリプト：[HUDVisibilityController.cs](file:///c:/Users/iniad/Documents/cs_exercise/Child-rearing-simulation/Assets/Yamaguchi/Scripts/UI/HUDVisibilityController.cs)
- シーン：`Assets/Yamaguchi/scenes/Main_test2.unity` の `Canvas` に設定完了。

