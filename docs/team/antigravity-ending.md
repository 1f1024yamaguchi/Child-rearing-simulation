---
title: 進路と巣立ちエンディングの実装法と Antigravity への依頼文
updated: 2026-10-19
status: 依頼前
related: [docs/design/ending.md, docs/design/child-status.md, docs/design/support-apply.md, docs/design/save-system.md, docs/decisions/0020-ending-on-leaving-home.md]
---

# 進路と巣立ちエンディングの実装法と Antigravity への依頼文

> 仕様は [../design/ending.md](../design/ending.md)。この文書は **「どう作るか」と「Antigravity にどう頼むか」** を書く。
> 依頼は **7回に分けて** 頼み、1回ごとに Unity で確認してから次へ進む。
> 依頼1〜5 は画面なし（Inspector の右クリックメニューとログで確認）、依頼6 で画面、依頼7 で奨学金。学費と授業料の減免は支出の仕組み（expenses.md）ができてから（依頼8・保留）。

---

## 1. 決まっていること（おさらい）

| 項目 | 決定 |
|---|---|
| 終わり方 | 子どもが巣立った日（高卒 18歳3月末／専門卒 20歳3月末／大卒 22歳3月末。浪人・留年で延びる） |
| 年齢の起点 | 0歳1月1日。学年は4月始まり（小1＝6歳4月、中1＝12歳4月、高1＝15歳4月） |
| テスト | 小学校は通知表だけ（7・12・3月）。中高は学期末テスト（7・12・3月）を合格ライン60で自動判定 |
| 受験 | 中3の2月に高校受験（公立か私立か）。高3の7月に進路決定、11〜12月に推薦、2月に一般入試 |
| 合否 | 進路ごとの合格ライン。`合格率 = Clamp(50% + (成績 − ライン) × 4%, 5%, 95%)`。模試判定 A〜E で見せる |
| 不合格 | 浪人（上限なし）／就職／専門 から選ぶ。希望と違う進路は子ストレス ＋30 |
| 在学中 | 学年末（3月）に平均がラインを下回ったら留年。巣立ちが1年延び、学費が1年分増える |
| 職業 | 巣立ちの日に、進路ごとの候補から点数が一番高いもの。条件はプレイヤーに見せない |
| 将来の夢 | 中1で1回、点数が一番高い職業。名前だけ子カードに出す |
| 記録 | 学期末テストの不合格回数は職業判定に使う。受験の記録はリザルト・エピローグにだけ使う |
| 奨学金 | サポート画面のカード。申請ダイアログで月額を選ぶ。借りた額はリザルトに出すだけ |

数値はすべて **たたき台**。全部 Inspector（ScriptableObject）から変えられるようにする。

---

## 2. 実装の全体像

### 2.1 部品と役割

```
（時間）
GameDate          … 学年の計算を足す（SchoolYear / SchoolStage）
GameState         … ゴールの日数を変えられるようにする（SetGoalAgeDays / OnGoalChanged / GoalLabel）

（学校の成績）
AcademicRecord    … 学期末テストの記録の入れ物。評定と不合格回数はここから計算する（データ）
TermTestSystem    … 7・12・3月に通知表／学期末テスト／在学中のテストを行う（MonoBehaviour・参加者 "academic"）

（進路）
PathDefinition    … 進路1つ（ScriptableObject）。在学年数・合格ライン・合格率の式の値・推薦の条件・職業の候補
AdmissionCalc     … 合格率と模試判定を出すだけの純粋なクラス
ChildPath         … いまの進路・学年・浪人／留年の回数・希望・受験の記録（データ）
PathSystem        … 高校受験・進路決定・推薦・一般入試・不合格の選び直し・留年・ゴールの更新（MonoBehaviour・参加者 "path"）

（職業とエンディング）
CareerDefinition  … 職業1つ（ScriptableObject）。条件・点数の重み・エピローグ文
CareerJudge       … 職業の判定だけの純粋なクラス（夢と巣立ちの両方で使う）
DreamSystem       … 中1で夢を決めて持つ（MonoBehaviour・参加者 "dream"）
GraduationWatcher … ゴール到達で職業を判定し、EndingResult を作って OnGraduated を出す
EndingResult      … リザルトに出す値の入れ物（データ）

（画面：依頼6）
PathDecisionUI / RetryChoiceUI / ResultScreenUI / 子カードの行の追加

（奨学金：依頼7）
SupportScheme に「選べる金額」、SupportApplyDialog に金額の欄、LoanManager（借りた額・変更待ち）
```

### 2.2 ゴールの日数とロードの順番（大事）

- 今の `GameState.GoalAgeDays` は `goalAgeYears × 365`（7300）で固定。これを **値として持ち、窓口メソッドで変える** 形にする。
- 既定値は **高校卒業の日の翌日＝18歳4月1日（`GameDate.ToAgeDays(18,4,1)` ＝ 6660）**。進路が決まったら `PathSystem` が巣立ちの日に変える。
- `GameState.LoadValues` は中で `IsGameOver = ageDays >= GoalAgeDays` を計算している。**ゴールを戻す前に年齢を戻すと、20歳より後のセーブがゲームオーバー扱いになる**。
  → ゴールの日数は参加者ではなく **`SaveData` に直接持たせ**、`SaveSystem.Load` の③で **`LoadValues` より先に** 戻す。

### 2.3 学年の数え方

```
年度   = (月 >= 4) ? 歳 : 歳 − 1        … 4月に1つ上がる
小学校 = 年度 6〜11（小1〜小6）
中学校 = 年度 12〜14（中1〜中3）
高校   = 年度 15〜17（高1〜高3）
年度 18〜 = 進路しだい（ChildPath が持つ）
```

学期末の月（7・12・3月）の「3月」は前の年度に入る（年度の数え方どおり）。

### 2.4 純粋なクラスに分ける理由

`AdmissionCalc` と `CareerJudge` は **画面・セーブ・時間に触らない**。入力（成績・ライン・ステータス）を渡すと結果を返すだけにする。
理由：右クリックメニューやテスト用の値で何度でも確かめられる。夢（中1）と巣立ちで同じ判定を使い回せる。

---

## 3. 依頼文

> 毎回、枠の中をそのまま Antigravity に貼る。最後の「MCP は使わない」の行は外さない（クレジット節約）。

### 依頼1：学年の計算とゴールの日数

```
AGENTS.md と .agents/rules/、docs/design/ending.md（0章・1章・6章）、docs/team/antigravity-ending.md（この文書）の 2.2・2.3 を読んでから作業してください。
置き場所は Assets/Yamaguchi/Scripts/Core/。1ファイル1クラス。

1. GameDate.cs に static メソッドを足す（今のメソッドは変えない）：
   - int SchoolYear(float ageDays)：年度を返す（月が4以上なら歳、そうでなければ歳−1）
   - SchoolStage Stage(float ageDays)：未就学／小学校／中学校／高校／高校卒業後 を返す（enum SchoolStage を新しく作る）
   - int GradeInStage(float ageDays)：その学校の何年生か（小1なら1）。未就学・高校卒業後は 0
   - bool IsTermEndMonth(int month)：7・12・3 のとき true
2. GameState.cs：
   - goalAgeDays（int）を持つ。既定値は GameDate.ToAgeDays(18, 4, 1)（＝ 6660。高校卒業の翌日）。Inspector で変えられるようにする
   - GoalAgeDays はこの値を返す（goalAgeYears は使わなくなるので消してよい）
   - goalLabel（string、既定 "高校卒業"）と GoalLabel プロパティ
   - SetGoal(int days, string label)：値を変えて OnGoalChanged(int days, string label) を出す。すでに年齢がゴール以上なら AddAgeDays と同じくゲームオーバーにする
   - LoadValues の今の動きは変えない
3. SaveData.cs に goalAgeDays（int、既定 -1）と goalLabel（string）を足す。
   SaveSystem.cs：Save で GameState の値を入れる。Load の③で、LoadValues を呼ぶ「前に」SetGoal で戻す（-1 なら戻さない＝古いセーブ）。
   ※ LoadValues は IsGameOver を年齢とゴールから計算するため、順番を逆にしないこと。
4. 成長ゲージ（UI/MainTestHUD.cs）：右端の文字を GameState.GoalLabel にし、OnGoalChanged で更新する。ゲージの計算は今のまま GoalAgeDays を使う。
5. SupportScheme.cs の Fix()：年の上限が 19 になっているので 29 に広げる（大学生の間の制度を作れるように）。
6. 確認用に GameState か新しい GoalDebugMenu.cs（GameManager に付ける）に [ContextMenu]：
   「Debug/ゴールを22歳3月末にする」「Debug/今の学年をログに出す」

守ること：
- お金・年齢・ゴールは GameState の窓口メソッドで変える。フィールドを直接書き換えない。
- 他のメンバーのフォルダ（Assets/Arisaka・Kusao・Maruyama）のファイルは変更しない。
- 数値はハードコードせず [SerializeField] にする。
- Unity の MCP ツール（unityMCP）は使わないでください。コードを書き終えたら、Unity で確認してほしいこと（Console のエラー、Play での確認手順）と、Inspector で必要な設定を箇条書きで教えてください。確認は私が Unity で行います。
```

**確認（依頼1）**
1. Play → ゲージの右端が「高校卒業」。
2. 「Debug/今の学年をログに出す」：0歳では未就学。GameState の ageDays を 6歳4月1日（2280）にして Play →「小学校 1年」、12歳4月（4470）→「中学校 1年」、17歳3月（6264）→「高校 2年」。
3. 「Debug/ゴールを22歳3月末にする」→ ゲージが縮む（右端が遠くなる）。
4. 手動セーブ → Play を止める → もう一度 Play → ロード → ゴールが22歳3月末のまま。**20歳を過ぎた日付でセーブしてロードしても、すぐゲームオーバーにならない**。

---

### 依頼2：学期末テスト・通知表・評定

```
docs/design/ending.md 2.5章 と docs/design/child-status.md 5章、docs/team/antigravity-ending.md 2.3 を読んでから作業してください。
置き場所は Assets/Yamaguchi/Scripts/Core/。

1. TermTestRecord.cs（[Serializable]）：schoolYear（年度）、month（7/12/3）、stage（SchoolStage）、grade（int・画面に出ていた成績）、passed（bool）
2. AcademicRecord.cs（[Serializable] のデータクラス）：
   - List<TermTestRecord> records
   - int FailCount：中学・高校の不合格の数（在学中のテストは含めない）
   - float HighSchoolAverage：高校の9回の平均（まだ無ければ今ある分の平均。0回なら -1）
   - int Hyotei：HighSchoolAverage を 5段階にする（0–19=1、20–39=2、40–59=3、60–79=4、80–100=5。境目は Inspector の配列で持つ）。高校のテストが無ければ 0
   - 在学中（大学・専門）用に AverageOfYear(int schoolYear) も用意（依頼4で留年に使う）
3. TermTestSystem.cs（MonoBehaviour・GameManager に付ける・ISaveParticipant。SaveKey "academic"、RestoreOrder 50）：
   - MonthTicker.OnMonthChanged を購読。月が 7・12・3 のときだけ動く
   - 小学校：通知表。SupportToast.Show で「通知表：成績 ○○」を出すだけ。記録しない
   - 中学校・高校：学期末テスト。ChildStatus.IsAbovePassLine で判定し、TermTestRecord を足す。
     不合格なら子ストレス＋（量は Inspector。たたき台 +5）を ChildStatus.AddStress で入れ、「学期末テスト：合格ラインに届かなかった」と通知
   - 高校卒業後（在学中）の判定は依頼4で足すので、ここでは何もしない
   - イベント OnTermTest(TermTestRecord) を出す
   - Capture / Restore で records を保存・復元する
4. 確認用 [ContextMenu]：「Debug/記録をログに出す（評定・不合格回数）」「Debug/学期末テストを今すぐ行う」

守ること：
- 判定は画面に出ている丸めた整数（ChildStatus.Grade）で行う。float で比べない。
- 子どものストレスは ChildStatus の窓口（AddStress）で変える。
- 他のメンバーのフォルダのファイルは変更しない。
- Unity の MCP ツール（unityMCP）は使わないでください。終わったら、Unity で確認してほしいことと、Inspector で必要な設定を箇条書きで教えてください。
```

**確認（依頼2）**
1. ageDays を 11歳12月（4350ごろ）にして Play。12月に通知表の通知だけ出る（記録は増えない）。
2. 12歳4月以降、7・12・3月に学期末テストの通知が出る。ChildStatus の成績を 50 にしておくと不合格になり、子どものストレスが上がる。
3. 「Debug/記録をログに出す」で回数と FailCount が合っている。高校に入ってから評定が出る。
4. セーブ → ロード → 記録が残っている。

---

### 依頼3：進路のデータ・合格率・進路決定（画面なし）

```
docs/design/ending.md 2章・3章（3.1〜3.5）と docs/team/antigravity-ending.md 2.1 を読んでから作業してください。
置き場所は Assets/Yamaguchi/Scripts/Core/（データのアセットは Assets/Yamaguchi/Data/Paths/）。

1. PathKind（enum）：未定 / 就職 / 専門学校 / 大学 / 推薦
2. PathDefinition.cs（ScriptableObject、[CreateAssetMenu("子育て/進路")]）：
   id、表示名、kind、在学年数（就職は0）、
   合格ライン（int）、基準（float 0.5）、傾き（float 0.04 = 1点あたり）、下限（0.05）、上限（0.95）、
   推薦用：必要な趣味の種類（HobbyKind、なし＝問わない）・必要な趣味Lv・必要な評定（たたき台 3）、
   在学中の合格ライン（int、たたき台 40）、
   入学条件 List<RequirementSO>（お金など。依頼8で使う。今は空でよい）
   ※ 職業の候補と既定の職業は依頼5で足す
   ※ 公立高校の受験用にも1つ作る（kind は使わず、ラインと式だけ使う）
3. AdmissionCalc.cs（static・純粋）：
   - float PassRate(int grade, PathDefinition p)：Clamp(基準 + (grade − ライン) × 傾き, 下限, 上限)
   - string MockRank(float rate)：A ≥0.8、B ≥0.6、C ≥0.4、D ≥0.2、それ未満 E（境目は static の配列で持つ）
   - bool CanApplyRecommendation(ChildStatus c, AcademicRecord r, PathDefinition p)：趣味の種類とLv、評定
4. ExamRecord.cs（[Serializable]）：kind（高校受験／推薦／一般）、schoolYear、pathId、wasWished（本人の希望だったか）、mockRank、passed
5. ChildPath.cs（[Serializable] のデータ）：
   currentPathId、enrolledSchoolYear（入学した年度）、yearInSchool（今何年生か）、roninCount、repeatCount（留年の回数）、
   wishPathId（子どもの希望）、decidedByParent（希望と違う進路を親が選んだか）、isPrivateHighSchool、List<ExamRecord> exams
6. PathSystem.cs（MonoBehaviour・GameManager に付ける・ISaveParticipant。SaveKey "path"、RestoreOrder 60）：
   - Inspector：進路のアセット一覧（就職・専門・大学・推薦）、公立高校のアセット、希望と違うときのストレス（たたき台 +30）
   - Wish：子どもの希望を返す。趣味Lv が推薦の条件を満たせば推薦、成績が大学のライン以上なら大学、趣味があれば専門、それ以外は就職（たたき台。依頼5の夢ができたら夢から決めるよう差し替える）
   - Decide(PathDefinition p)：進路を決める。希望と違えば子ストレス＋30 と decidedByParent = true。
     就職なら巣立ちの日＝高校卒業の翌日（ゴールはそのまま、ラベルを「巣立ち」に）。
     専門・大学・推薦は、合格するまでゴールを変えない（依頼4で合格したときに変える）
   - イベント OnPathDecisionNeeded（高3の7月に出す。画面は依頼6）、OnPathChanged
   - 高3（年度17）の7月になったら OnPathDecisionNeeded を出し、まだ決めていなければ時間を止めずに待つ（画面ができるまでは右クリックで決める）
   - Capture / Restore
7. 確認用 [ContextMenu]：「Debug/希望と各進路の判定をログに出す」「Debug/就職に決める」「Debug/大学に決める」「Debug/専門に決める」「Debug/推薦に決める」

守ること：
- 数値は全部アセットか [SerializeField] に置く。
- ゴールの変更は GameState.SetGoal で行う。
- 他のメンバーのフォルダのファイルは変更しない。
- Unity の MCP ツール（unityMCP）は使わないでください。終わったら、Unity で確認してほしいこと、作るアセット（進路4つ＋公立高校）と設定値の一覧を箇条書きで教えてください。
```

**確認（依頼3）**
1. 進路のアセットを5つ作り、`PathSystem` に入れる（値は ending.md 3.4 の表：大学65・専門40・推薦50＋評定3・公立高校は仮に55）。
2. 「Debug/希望と各進路の判定をログに出す」：成績 75 で大学が A（90%）、65 で C（50%）、55 で E（10%）。
3. 子どもの成績が高い状態で「Debug/就職に決める」→ 子どものストレスが +30、ゲージの右端が「巣立ち」。
4. ageDays を 17歳7月の直前にして Play → 7月に「進路を決めてください」のログ（OnPathDecisionNeeded）。
5. セーブ → ロード → 進路と希望が残っている。

---

### 依頼4：受験の合否・浪人・留年

```
docs/design/ending.md 2.5章・3.4・3.5・4章（留年）を読んでから作業してください。依頼3の PathSystem に足す形で作ってください。

1. 高校受験（中3＝年度14 の2月）：公立高校のアセットで PassRate を出し、抽選（UnityEngine.Random）。
   不合格なら isPrivateHighSchool = true、子ストレス＋（たたき台 +10）。ExamRecord を残し、通知を出す。
   ※ 私立の学費は依頼8。ここでは記録するだけ。
2. 推薦（高3の11月）：推薦に決めていて、CanApplyRecommendation を満たしていれば抽選。合格なら合格処理（下の4）。
   条件を満たしていない・不合格なら、一般入試（大学）に切り替える。
3. 一般入試（高3の2月。浪人中は毎年2月）：大学か専門に決めていれば、その進路で抽選。合格なら合格処理。
   不合格なら OnRetryChoiceNeeded を出す（画面は依頼6）。選択は Retry(RetryChoice)：
     - 浪人：roninCount+1、翌年2月にもう一度。ゴールは1年延ばす。子ストレス＋（たたき台 +5）。浪人の上限はなし
     - 就職：希望が大学なら子ストレス +30。巣立ちは今年の3月末（＝4月1日がゴール）
     - 専門：専門のラインで、その場でもう一度抽選（専門の2次募集のイメージ）。希望が大学なら +30
   ※ 合否の乱数はセーブに入れない（ロードで引き直せてよい。ending.md 0章で決定）
4. 合格処理：currentPathId を決め、enrolledSchoolYear と yearInSchool=1、
   巣立ちの日 = 入学年度 + 在学年数 の年の4月1日 で GameState.SetGoal(…, "巣立ち")。ExamRecord を残す。
5. 在学中の学年末（3月）：その年度の学期末テスト（TermTestSystem に在学中も 7・12・3月に記録させる。stage は高校卒業後）の平均が
   在学中の合格ラインを下回ったら留年：repeatCount+1、ゴールを1年延ばす、子ストレス＋（たたき台 +10）、通知。
   上回ったら yearInSchool+1。
6. 浪人中は「高校卒業後」だが在学していないので、学期末テストはしない。
7. イベント：OnExamResult(ExamRecord)、OnRetryChoiceNeeded、OnRepeatYear
8. 確認用 [ContextMenu]：「Debug/今すぐ一般入試」「Debug/浪人」「Debug/就職」「Debug/専門」「Debug/今すぐ学年末判定」

守ること：
- 判定は丸めた整数の成績で行う。
- ゴールの変更は GameState.SetGoal だけで行う。
- 他のメンバーのフォルダのファイルは変更しない。
- Unity の MCP ツール（unityMCP）は使わないでください。終わったら、Unity で確認してほしいことを箇条書きで教えてください。
```

**確認（依頼4）**
1. 14歳2月の直前から Play。成績を下げておくと高校受験に落ち、「私立高校」の記録が残る。
2. 大学に決めて、成績 30 で「Debug/今すぐ一般入試」→ ほぼ不合格 → OnRetryChoiceNeeded のログ。「Debug/浪人」→ ゴールが1年延びる。
3. 成績 90 で一般入試 → 合格 → ゴールが22歳3月末（浪人していれば23歳）。ゲージの右端が「巣立ち」。
4. 大学1年の成績を 30 にして「Debug/今すぐ学年末判定」→ 留年、ゴールがさらに1年延びる。
5. 何度かセーブ → ロードして、浪人・留年の回数とゴールが合っている。

---

### 依頼5：職業の判定・将来の夢・巣立ち（ログまで）

> [antigravity-result-scene.md](antigravity-result-scene.md) の依頼R1 を先に済ませた場合、4. の EndingResult は作らずに職業まわりの項目を足すだけにし、GraduationWatcher は巣立ちの日に `GameState.EndGame(GameEndReason.GoalReached)` を呼ぶ（EndingFlow は `OnGameEnded` を見ているので付け替え不要）。

```
docs/design/ending.md 5章・6.2・7章 を読んでから作業してください。置き場所は Assets/Yamaguchi/Scripts/Core/（職業のアセットは Assets/Yamaguchi/Data/Careers/）。

0. PathDefinition.cs に 職業の候補 List<CareerDefinition> と 既定の職業 CareerDefinition を足す。
1. CareerDefinition.cs（ScriptableObject、[CreateAssetMenu("子育て/職業")]）：
   id、表示名、イラスト（Sprite）、
   条件：必要な最低成績・必要な趣味の種類と最低Lv・不合格回数の上限（-1 = 問わない）、
   重み：成績 a、趣味Lv b（趣味の種類が合うときだけ）、体調 c、
   特性の加点 List<(TraitDefinition, float)>（Serializable なクラスで）、
   エピローグ文（string。例「○年後、博士に」。空でよい）
2. CareerJudge.cs（static・純粋）：
   - float Score(CareerDefinition c, ChildStatus child, AcademicRecord r)：条件を満たさなければ float.NegativeInfinity
   - CareerDefinition Judge(IEnumerable<CareerDefinition> candidates, CareerDefinition fallback, …)：最高点。同点は候補リストの先の方。全部ダメなら fallback
   - 子どものストレスは点数に入れない（ending.md 5.1 で決定）
3. DreamSystem.cs（MonoBehaviour・ISaveParticipant。SaveKey "dream"、RestoreOrder 70）：
   - Inspector：全部の職業のリスト
   - 中1（年度12）の4月になったら1回だけ、全部の職業で CareerJudge.Judge して夢を決め、OnDreamDecided を出し、「○○になりたい！」と通知
   - DecideDreamNow() を public にしておく（丸山さんのイベントから呼べるように）
   - PathSystem の Wish（依頼3）を、夢があれば「その職業が候補に入っている進路」を返すよう差し替える
4. EndingResult.cs（[Serializable]）：職業id・職業名、巣立ちの年齢（歳と月）、進路のあゆみ（ChildPath の写し）、
   受験の記録、子育てにかかったお金（今は 0。家計簿ができたら入れる）、受け取った支援の合計（SupportManager から取れなければ 0）、
   親が稼いだお金の合計（IncomeHistory から取れる範囲）、奨学金の合計（依頼7まで 0）、最終の成績・趣味・体調・特性、エピローグ文
5. GraduationWatcher.cs（MonoBehaviour）：
   - GameState.OnGameOver を購読。RunawayWatcher.HasRunAway なら何もしない（家出が優先）
   - 一度だけ：時間を止める（GameClock.SetRunning(false)）→ 進路の候補で CareerJudge.Judge → EndingResult を作る → OnGraduated(EndingResult) を出す → ログに中身を全部出す
   - AutoSaver はすでに OnGameOver でセーブを消すので、そこは触らない
6. 確認用 [ContextMenu]：「Debug/今のステータスで職業を判定してログ」「Debug/夢を今すぐ決める」「Debug/今すぐ巣立ち」

守ること：
- CareerJudge は画面・セーブ・時間に触らない。
- 他のメンバーのフォルダのファイルは変更しない。
- Unity の MCP ツール（unityMCP）は使わないでください。終わったら、Unity で確認してほしいことと、作る職業アセットの例（ending.md 5.2 の表から）を箇条書きで教えてください。
```

**確認（依頼5）**
1. ending.md 5.2 の表から職業アセットを作り、進路のアセットの候補に入れる。
2. 「Debug/今のステータスで職業を判定してログ」：成績を高くすると研究者の卵・エンジニア、スポーツLvを高くすると選手になる。
3. 12歳3月の直前から Play → 4月に「○○になりたい！」。セーブ → ロードで夢が残る。
4. 「Debug/今すぐ巣立ち」→ 時間が止まり、EndingResult の中身がログに出る。
5. 家出した状態でゴールに着いても、職業の判定は出ない。

---

### 依頼6：画面（進路決定・選び直し・リザルト・子カード）

> 3. の ResultScreenUI は [antigravity-result-scene.md](antigravity-result-scene.md) の依頼R2 で end シーンに作る。済んでいれば、ここでは進路のあゆみ・職業のイラストの表示を足すだけにする。

```
docs/design/ending.md 3.1・3.4・3.5・5.3・7章 と、docs/code/ui-support-card.md、docs/code/ui-scroll-patterns.md を読んでから作業してください。
置き場所は Assets/Yamaguchi/Scripts/UI/。UI はプレハブにする（シーンを直接いじらない）。

1. PathDecisionUI.cs：PathSystem.OnPathDecisionNeeded で開く。
   進路ごとに1行：名前・模試判定（A〜E）・選べるか（選べないなら理由）。子どもの希望を上に出す（「○○に進みたい」）。
   希望と違う進路を押したら「○○は△△に進みたがっています。決めますか？」の確認を出す。
   開いている間は時間を止める（OnEnable で GameClock.I.PushPause、OnDisable で PopPause。AGENTS.md ルール8）。
2. RetryChoiceUI.cs：OnRetryChoiceNeeded で開く。［浪人する］［就職する］［専門学校に行く］。時間を止める。
3. ResultScreenUI.cs：GraduationWatcher.OnGraduated で開く。ending.md 7章の順に上から流す（スキップボタンあり）。
   - 「○○は △△ になりました」＋ イラスト。夢と違っても「叶わなかった」とは出さない
   - 進路のあゆみ（浪人・留年・親が決めた進路なら一言）
   - 数字のまとめ（EndingResult の値。0 の項目は出さない）
   - 最後に［タイトルへ］
4. 子カード（ChildCardUI.cs）：
   - 年齢の行を「15歳・高校1年」の形にする（GameDate.Stage / GradeInStage、高校卒業後は ChildPath の学年）
   - ステータス欄に「将来の夢　○○」（夢が決まってから）、「志望：大学 B判定」（高3の7月〜合否まで、毎月更新）を足す
5. 計算はしない。値は PathSystem / AdmissionCalc / DreamSystem / EndingResult から受け取るだけ。

守ること：
- 他のメンバーのフォルダのファイルは変更しない。
- Unity の MCP ツール（unityMCP）は使わないでください。終わったら、作るプレハブの階層、Inspector で必要な設定、確認手順を箇条書きで教えてください。
```

**確認（依頼6）**
1. 17歳6月の終わりから Play → 7月に進路決定の画面が出て、時間が止まる。判定と希望が出ている。
2. 希望と違う進路を選ぶと確認が出る。
3. 大学に落ちると選び直しの画面が出る。
4. 「Debug/今すぐ巣立ち」でリザルトが流れ、［タイトルへ］で戻れる。
5. 子カードに学年・将来の夢・志望の判定が出る。

---

### 依頼7：奨学金（貸与型・修学支援の給付）

```
docs/design/ending.md 3.3 と docs/design/support-apply.md（5章・7.3）、docs/code/ui-support-card.md、docs/code/how-to-add-support-scheme.md を読んでから作業してください。

1. SupportScheme.cs に足す（既存の制度の動きは変えない）：
   - List<int> amountOptions（空なら今までどおり）、bool amountIsRange、int amountMin / amountMax / amountStep（第二種は 20000〜120000、10000 刻み）
   - List<int> lumpSumOptions（入学時特別増額。空なら出さない）
   - bool isLoan（借りるお金か）
2. LoanManager.cs（MonoBehaviour・ISaveParticipant。SaveKey "loan"、RestoreOrder 35）：
   制度ごとに：今の月額、変更待ちの月額と反映する月、その年度に変更したか、借りた合計（LoanTotal）。
   - Apply(scheme, monthly, lumpSum)：翌月から毎月 お金 +monthly。lumpSum があれば今 +lumpSum。合計に足す
   - ChangeAmount(scheme, newMonthly)：2か月後の月から反映（月数は Inspector、たたき台 2）。1年度に1回まで
   - Stop(scheme)：翌月から止める。SupportManager の状態を申請前に戻す
   - 毎月の振り込みは MonthTicker.OnMonthChanged で行い、お金は GameState.AddMoney で入れる
   - 借りた合計を EndingResult の奨学金の合計に入れる（GraduationWatcher から読む）
   ※ SupportManager の monthlyTiers の仕組みには乗せず、借りるお金は LoanManager が払う。SupportManager には「申請済み」の状態だけ持たせる
3. SupportApplyDialog.cs：amountOptions か amountIsRange がある制度のときだけ、
   - 金額を選ぶ欄（選択肢ならボタン、範囲なら［−］金額［＋］）
   - lumpSumOptions があり、1年生の4月なら「入学時に追加で借りる」のボタン
   - isLoan なら注意欄に赤字で「卒業後に返さなくてはいけないお金です（借金）」と「卒業までに借りる合計：約○○円」（月額 × 残りの在学月数 ＋ 増額。選ぶたびに更新）
   - ［申請する］で LoanManager.Apply
4. SupportSchemeCard.cs：isLoan で申請済みなら［金額を変える］［借りるのをやめる］。
   変更待ちがあれば「○月から 月○○円に変更」、今年度もう変えたなら［金額を変える］を押せなくして理由を出す。
   ［借りるのをやめる］は確認ダイアログ（「やめると来月から振り込みが止まります。もう一度借りるには申請し直しが必要です」）。
5. 申請できる期間の条件：大学・専門に在学中だけ（PathSystem を見る RequirementSO を1つ作る：EnrolledRequirementSO）。
   第一種は評定 3.5 以上（HyoteiRequirementSO）。
6. 制度アセットを3つ作る（値は ending.md 3.3）：第一種（大学 20000/30000/40000/54000）、第二種（範囲）、修学支援の給付型（Monthly・第Ⅰ〜Ⅲ区分。区分の年収の境目は未確認なので、まず第Ⅰ区分の 38,300 だけ・年収条件はたたき台）

守ること：
- 児童手当など、金額を選ばない制度の見た目と動きは変えない。
- お金は GameState.AddMoney で入れる。
- 他のメンバーのフォルダのファイルは変更しない。
- Unity の MCP ツール（unityMCP）は使わないでください。終わったら、Unity で確認してほしいこと、作るアセットと設定値を箇条書きで教えてください。
```

**確認（依頼7）**
1. 大学に合格させてからサポート画面を開く。第二種のカードで［申請する］→ 金額の欄と赤字の注意が出る。金額を変えると「卒業までに借りる合計」が変わる。
2. 申請 → 翌月からお金が増える。
3. ［金額を変える］→ 2か月後から額が変わる。同じ年度にもう一度は押せない。
4. ［借りるのをやめる］→ 翌月から止まる。カードが申請前に戻る。
5. 巣立ちのリザルトに奨学金の合計が出る。セーブ → ロードで借りた合計・変更待ちが残る。
6. 児童手当のダイアログは今までどおり。

---

### 依頼8（保留）：学費・授業料の減免・私立高校

支出の仕組み（[expenses.md](../design/expenses.md) の `ExpenseItem` と家計簿）がまだコードに無いので、それができてから頼む。中身：

- 進路・私立高校の学費の `ExpenseItem`（入学金・授業料。在学中・留年中は毎月）
- 修学支援の授業料等減免（`covers` で肩代わり。私立大学 入学金 約26万・授業料 約70万、専門 約16万・約59万）
- 高校就学支援金の上限を公立／私立で切り替える（support-apply.md 9.2）
- 浪人中の予備校代
- `PathDefinition` の入学条件（入学金＋初年度を払えるか）を有効にする
- EndingResult の「子育てにかかったお金」を家計簿の合計から入れる

---

## 4. 他のメンバーへの伝え方

- **丸山さん（イベント）**：将来の夢は中1で自動で決まる。イベントから夢を決め直したい・夢のイベントの演出を作りたいときは `DreamSystem.DecideDreamNow()` と `OnDreamDecided` を使ってもらう。
- **草尾さん（カレンダー）**：進路決定（高3の7月）・推薦（11月）・一般入試（2月）・高校受験（中3の2月）を、プレイヤーが予定として書き込めるとよい。カレンダーは「プレイヤーが書いた予定だけ」を出す方針（ADR 0016）なので、自動では載せない。

---

## 5. 進み具合

| 依頼 | 内容 | 状態 |
|---|---|---|
| 1 | 学年の計算・ゴールの日数・成長ゲージの表記 | 未依頼 |
| 2 | 学期末テスト・通知表・評定 | 未依頼 |
| 3 | 進路のデータ・合格率・進路決定（画面なし） | 未依頼 |
| 4 | 受験の合否・浪人・留年 | 未依頼 |
| 5 | 職業の判定・将来の夢・巣立ち（ログ） | 未依頼 |
| 6 | 画面（進路決定・選び直し・リザルト・子カード） | 未依頼 |
| 7 | 奨学金（貸与型・給付型） | 未依頼 |
| 8 | 学費・減免・私立高校 | 保留（expenses.md の実装待ち） |
