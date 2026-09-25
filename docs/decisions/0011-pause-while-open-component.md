---
title: 時間を止めるのは「画面が表示されている間」とし、画面に PauseWhileOpen を付けるだけにする
updated: 2026-09-25
status: 廃止
---

# 時間を止めるのは「画面が表示されている間」とし、画面に PauseWhileOpen を付けるだけにする

> ⚠ **廃止（2026-09-25）。** 時間停止は `GameClock.PushPause()` / `PopPause()` で実装され、`CalendarUI` の `OnEnable` / `OnDisable` から呼ぶ形なら閉じ方による呼び忘れも起きないため、実装どおりに確定した。現在有効なのは [0004-pause-during-decisions.md](0004-pause-during-decisions.md)。

> [0004-pause-during-decisions.md](0004-pause-during-decisions.md) の**実装方法（`PushPause` / `PopPause`）だけ**を置き換えるもの。「判断画面を開いている間は時間を止める」という 0004 の決定そのものは変わらない。

## 背景
0004 では、各画面が開くときに `PushPause()`、閉じるときに `PopPause()` をペアで呼ぶ方式にしていた。
しかし閉じるルートが複数ある画面（×ボタン／決定ボタン／背景タップ）では `PopPause` を1か所でも呼び忘れると、**ゲームが止まったまま戻らない**。各担当が自分の画面で毎回気をつける必要があり、もっとシンプルにしたかった。

## 決定
**画面のゲームオブジェクトが表示されている（active）間は時間を止める。** 止めたい画面に `PauseWhileOpen` コンポーネントを付けるだけで、画面側にコードは書かない。

```csharp
// Assets/Yamaguchi/Scripts/Core/PauseWhileOpen.cs
// 付けたオブジェクトが表示されている間、ゲーム内時間を止める
public class PauseWhileOpen : MonoBehaviour
{
    public static int OpenCount { get; private set; }   // 今開いている画面の数
    public static bool IsPaused => OpenCount > 0;

    private void OnEnable()  { OpenCount++; }   // SetActive(true) で呼ばれる
    private void OnDisable() { OpenCount--; }   // SetActive(false)・Destroy で呼ばれる

    // Enter Play Mode 設定でドメインリロードを切っていても、再生のたびに 0 に戻す
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCount() { OpenCount = 0; }
}
```

```csharp
// GameClock.Update() の先頭を変更
if (!running || PauseWhileOpen.IsPaused) return;
```

### 使い方
1. 止めたい画面のルート（例：`CalendarPanel`）に `PauseWhileOpen` を Add Component する。
2. 開くときは `SetActive(true)`、閉じるときは `SetActive(false)`。これだけ。
3. **その画面はゲーム開始時は非表示にしておく。** 表示したまま始めると、最初から時間が止まる。

- 対象の画面は 0004 と同じ（カレンダー／制度一覧／食事のランク選択／イベントの行動選択）。今後の判断画面も同じ扱い。
- 画面を開閉するのに `GameClock.SetRunning` は使わない。`SetRunning(false)` は**ゲームオーバー（20歳到達・家出）で完全に止めるとき専用**として残す。

## 検討した他の案と、採らなかった理由
- **`PushPause` / `PopPause` をペアで呼ぶ（0004 の案）** … 閉じるルートが複数あると呼び忘れが起き、止まったまま戻らない。
- **`GameClock` がカレンダーの GameObject を直接見る** … カレンダー専用になり、食事選択やイベントの画面が増えるたびに `GameClock` を書き換えることになる。
- **画面ごとに `SetRunning` を直接呼ぶ** … 画面が2つ重なって開いたとき、片方を閉じた瞬間に動き出してしまう。

## 結果・影響
- 画面は開閉するだけでよくなり、呼び忘れのバグが起きなくなる。画面が重なっても、開いている数を数えているので正しく止まる。
- 画面を**親ごと**非表示にしても `OnDisable` が呼ばれるので止まりっぱなしにはならない。逆に、親が非表示のまま子の画面を `SetActive(true)` しても、実際に表示されるまで時間は止まらない（Unity の `OnEnable` の仕様どおり）。
- 担当は草尾（[../design/calendar-system.md](../design/calendar-system.md) 8.2）。`GameClock.cs` は山口さんの Core にあるので、1行足したことを伝える。
