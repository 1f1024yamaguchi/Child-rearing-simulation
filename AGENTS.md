# 子ども育成シミュレーションゲーム

Unity 製の子育てシミュレーションゲーム。**このファイルは AI エージェントとチームメンバーの入口です。作業を始める前に必ず読んでください。**

- エンジン：Unity（URP / 新 Input System / TextMeshPro）
- 共通の土台：`Assets/Yamaguchi/Scripts/Core/`
- 各メンバーの作業場所：`Assets/<自分の名前>/`（Arisaka / Kusao / Maruyama / Yamaguchi）

---

## エージェント向けルール

1. **仕様を推測しない。** ゲームの挙動・数値・ルールに関わる判断は、必ず `docs/design/` の該当ファイルを読んでから行う。書かれていなければ「未定」として人間に確認する。
2. **数値はすべて「たたき台」。** `docs/design/` の表にある数値は調整前の仮値。勝手に「正式な仕様」として扱わない。数値も効果の種類も今後変わる前提で、**後から足せる形**に作る。
3. **効果はデータで持つ。** 食事・イベント・制度の効果は `List<Effect>` として持ち、適用は共通の `EffectApplier` に任せる。各機能から `GameState` を直接叩かない。→ [docs/design/effects.md](docs/design/effects.md)
4. **`GameState` を経由する。** お金・ストレス・年齢を自前のフィールドで持たず、`GameState.I` の窓口メソッドを呼ぶ。詳細は [docs/code/architecture.md](docs/code/architecture.md)。
5. **他のメンバーの担当領域に踏み込まない。** 作業は `Assets/<担当者名>/` の中で完結させ、同じシーンファイルを複数人で同時に編集しない（自分の UI はプレハブ化する）。担当は下の表。
6. **仕様やコードを変えたら、同じ PR で該当ドキュメントも更新する。** 更新したファイルの frontmatter の `updated` を当日の日付にする。
7. **設計判断をしたら `docs/decisions/` に 1 ファイル追加する。**（テンプレート：`docs/decisions/_template.md`）
8. **判断画面を作るときは時間を止める。** 画面の `OnEnable` で `GameClock.I.PushPause()`、`OnDisable` で `PopPause()` を呼ぶ（`CalendarUI` と同じ形）。`GameClock.SetRunning` は画面の開閉に使わない。→ [docs/decisions/0004-pause-during-decisions.md](docs/decisions/0004-pause-during-decisions.md)
9. **コーディング規約は `.agents/rules/` を読む。** 特に Unity / TextMeshPro 固有のハマりどころが書いてある。
10. **「シュミレーション」は誤記。** 文中の表記は「シミュレーション」に統一する（既存の固有名詞はそのまま）。

---

## チームと担当

| 担当 | 機能 | 作業フォルダ | 仕様書 |
|---|---|---|---|
| 有坂 | 食事のランク選択と効果 | `Assets/Arisaka/` | [food-system.md](docs/design/food-system.md) |
| 草尾 | カレンダー機能（申請期限の管理・メモ）。実装前に作るもの：`GameDate` の関数3つ／時間を止める仕組み／メモと制度の状態のセーブ（→ calendar-system.md 8章） | `Assets/Kusao/` | [calendar-system.md](docs/design/calendar-system.md) |
| 丸山 | メリット／デメリットイベントの発生 | `Assets/Maruyama/` | [events.md](docs/design/events.md) |
| 山口 | 共通の土台（Core）・HUD・セーブ機能（続きから遊べるようにする） | `Assets/Yamaguchi/` | [architecture.md](docs/code/architecture.md) |

実装の進め方は [docs/team/handout.md](docs/team/handout.md)。
※ ハンドアウトのサンプルコードは担当が変わる前のもの。担当の対応はこの表が正。

---

## ドキュメント索引

### ゲーム仕様 — `docs/design/`

| ファイル | 内容 | こんなときに読む |
|---|---|---|
| [concept.md](docs/design/concept.md) | コンセプト、親子UI、決まっていること／未決定の一覧 | 企画の全体像を掴みたい |
| [rules-and-stats.md](docs/design/rules-and-stats.md) | 時間の流れ、ゴール／ゲームオーバー条件、全ステータス定義と数値 | ゲームロジックを実装する |
| [food-system.md](docs/design/food-system.md) | 食事グレードの効果、安い／高い食事のリアルな影響 | 食事まわりを実装する（有坂） |
| [events.md](docs/design/events.md) | メリット／デメリットイベントの一覧と発生条件 | イベントの発生を実装する（丸山） |
| [calendar-system.md](docs/design/calendar-system.md) | カレンダー画面、申請期限の管理、予定の種類と状態 | カレンダー機能を実装する（草尾） |
| [effects.md](docs/design/effects.md) | 効果（Effect）の共通モデル。食事・イベント・制度が共通で使う | **3機能のどれを実装するときも必ず** |
| [child-status.md](docs/design/child-status.md) | 子どものステータス（成績・趣味・体調・勉強⇔遊び）と子どもカードのUI | 子どもの値やカードを実装する |
| [traits.md](docs/design/traits.md) | 特性（永続）と状態（一時）。風邪気味・特殊能力の持ち方 | イベントで特性・状態を付ける |
| [support-systems.md](docs/design/support-systems.md) | 子育て支援制度のカテゴリ、メリットとデメリットの型、制度一覧画面の設計 | 制度データを扱う（草尾・丸山の共通の土台） |

### 実装メモ — `docs/code/`

| ファイル | 内容 | こんなときに読む |
|---|---|---|
| [architecture.md](docs/code/architecture.md) | 時間の流れ（GameClock→DayTicker/MonthTicker）、GameState の API、給与・親ストレスの計算、未実装の一覧 | **ゲームロジックに触る前に必ず** |
| [ui-main-test-hud.md](docs/code/ui-main-test-hud.md) | Main_test シーンの HUD 構成と `MainTestHUD` の動作方式 | HUD を触る |
| [ui-scroll-patterns.md](docs/code/ui-scroll-patterns.md) | uGUI スクロールの作り方、ステータス行プレハブ、TMP のハマりどころ | 一覧・スクロール UI を作る |

### チーム — `docs/team/`

| ファイル | 内容 |
|---|---|
| [handout.md](docs/team/handout.md) | 3機能の実装ハンドアウト（担当・サンプルコード・組み込み手順） |
| [antigravity-child-status.md](docs/team/antigravity-child-status.md) | 子どもステータス・特性・状態を AI に実装依頼するための段階別プロンプト |

### 設計判断の履歴 — `docs/decisions/`

「なぜそうなっているか」の記録。過去の判断を覆す提案をする前に必ず目を通すこと。

- [0001-realtime-time-model.md](docs/decisions/0001-realtime-time-model.md) — ターン制ではなくリアルタイム進行にした理由
- [0002-support-systems-have-drawbacks.md](docs/decisions/0002-support-systems-have-drawbacks.md) — 支援制度にデメリットを持たせる理由
- [0007-traits-and-conditions.md](docs/decisions/0007-traits-and-conditions.md) — 特殊能力を「永続の特性」と「一時の状態」に分けた理由
- [0004-pause-during-decisions.md](docs/decisions/0004-pause-during-decisions.md) — 判断画面で時間を止める方法（`OnEnable` / `OnDisable` で `PushPause` / `PopPause`）
- [0013-child-stress-per-day-and-badge-removal.md](docs/decisions/0013-child-stress-per-day-and-badge-removal.md) — 子どものストレス計算を1日あたりに変更し、カードのストレス段階バッジを廃止した理由

---

## 未決定のまま残っている論点

実装で判断に迷ったらここを見る。勝手に決めず、人間に確認すること。

**まず土台側で決める・作るもの（3人が実装に入る前に）**

- **Effect の土台を誰が作るか**：[docs/design/effects.md](docs/design/effects.md) の `Effect` / `EffectApplier` はまだ存在しない。名前の確定も含めて先に決めたい。
- **ストレスの持ち方の整理**：仕様は人ごと（母親・父親・子ども）だが、コードには `GameState.Stress`（世帯にひとつ・旧仕様）が残っており、子ども用の入れ物も無い。設計案は [docs/design/child-status.md](docs/design/child-status.md) 1章（`PersonStatus` にまとめる）。→ [docs/code/architecture.md](docs/code/architecture.md) 3章
- **特性・状態の土台を誰が作るか**：`StatModifier` / `ModifierOp` と、補正を集める仕組み。`Effect` の土台と同じ場所なので、同じ人が作るのが自然。→ [docs/design/traits.md](docs/design/traits.md)

**仕様として未決定**

- **子どものステータス**：仕様は [docs/design/child-status.md](docs/design/child-status.md) にまとめた。残るは **テスト・受験の時期**（節目テスト＋本番／毎年／20歳の1回のみ）と、「進路が狭まる」の具体的な判定。
- **家出までの日数**：子のストレスが限界のまま何日続いたらゲームオーバーか。仮で30日。→ [docs/design/child-status.md](docs/design/child-status.md) 4章
- **カレンダー機能の仕様**：主要な仕様は決定済み（[docs/decisions/0010-calendar-follows-reality.md](docs/decisions/0010-calendar-follows-reality.md)）。残りは状態の色と画面サイズ（たたき台）だけ。→ [docs/design/calendar-system.md](docs/design/calendar-system.md) 7章
- **家族構成**：親2人＋子1人？ プレイヤー＋子2人？ → [docs/design/concept.md](docs/design/concept.md) 6章
- **テンポ**：20歳到達まで約2時間。判断画面で止まるぶんさらに長くなる。速度設定でどう調整するか。
