---
title: 設計判断の履歴�E�EDR�E�E
updated: 2026-10-30
status: 運用中
---

# 設計判断の履歴�E�EDR�E�E

「なぜそぁE��ってぁE��か」を1判断1ファイルで残す場所。後から参加した人・AI が、E��去の判断を知らずにひっくり返すのを防ぐため�Eも�E、E

## 書き方

- ファイル名：`0003-短ぁE��語�E要紁Emd`�E�連番 + ハイフン区刁E���E�E
- チE��プレート：[_template.md](_template.md)
- **一度書ぁE�� ADR は書き換えなぁE��E* 判断を要E��とき�E新しい ADR を追加し、古ぁE��の frontmatter の `status` めE`廁E��` にして、新しい ADR へのリンクを追記する、E

## 一覧

| # | タイトル | 状慁E|
|---|---|---|
| [0001](0001-realtime-time-model.md) | ターン制ではなくリアルタイム進行にする | 採用 |
| [0002](0002-support-systems-have-drawbacks.md) | 支援制度に忁E��チE��リチE��を持たせめE| 採用 |
| [0003](0003-pause-while-calendar-open.md) | カレンダーを開ぁE��ぁE��間�Eゲームの進行を止める | 廁E���E�E004に統合！E|
| [0004](0004-pause-during-decisions.md) | プレイヤーが判断する画面を開ぁE��ぁE��間�E時間を止める | 採用 |
| [0005](0005-per-person-stress.md) | ストレスは人ごとに持ち、�E員に及�E効果�E一律に適用する | 採用 |
| [0006](0006-effect-model.md) | 効果�E共通�E Effect リストで表現する | 採用 |
| [0007](0007-traits-and-conditions.md) | 特殊�E力�E「永続�E特性」と「一時�E状態」に刁E��めE| 採用 |
| [0008](0008-calendar-ui-implementation.md) | カレンダー画面のUI構�Eと時間停止連動�E実裁E| 採用 |
| [0009](0009-real-calendar-months.md) | 1ヶ月�E実際の暦の日数�E�E8、E1日�E�とし、E年365日で回す | 採用 |
| [0010](0010-calendar-follows-reality.md) | カレンダーの予定と期限刁E��は現実�E制度と同じ扱ぁE��する | 採用 |
| [0011](0011-pause-while-open-component.md) | 時間を止めるのは画面が表示されてぁE��間とし、画面に PauseWhileOpen を付けるだけにする | 廁E���E�E004 の実裁E��おり�E�E|
| [0012](0012-birth-day-is-monday.md) | 生まれた日�E�E歳1朁E日�E�を月曜日とし、E日周期で曜日を回ぁE| 廁E���E�曜日は使わなぁE��E|
| [0013](0013-child-stress-per-day-and-badge-removal.md) | 子ども�Eストレス計算を1日あたりに変更し、カード�Eストレス段階バチE��を廁E��する | 採用 |
| [0019](0019-two-save-slots.md) | セーブ�Eオートと手動の2枠にし、「最初から」ではセーブを消さなぁE| 採用 |
| [0020](0020-ending-on-leaving-home.md) | ゲームの終わりを、E0歳」から「子ども�E巣立ち」に変え、E��路で巣立つ年齢を変えめE| 採用 |
| [0021](0021-yearly-income-tax.md) | 給料�E天引きを一律�E手取り率から、年単位�E税��に置き換える | 採用 |
| [0024](0024-game-speed-control.md) | 倍速機�E�E�通常・2x・3x�E��E導�Eと日次処琁E�E完�E実行モチE�� | 採用 |
| [0024](0024-game-speed-control.md) | 倍速機能（通常・2x・3x）の導入と日次処理の完全実行モデル | 採用 |
| [0025](0025-hud-visibility-on-panel-open.md) | サポート画面・人物詳細パネル表示時のHUDボタン排他非表示制御 | 採用 |
| [0026](0026-tokyo-018-support-payout-schedule.md) | 018サポートを4・8・12月にまとめて支給する | 採用 |
