using System.Collections.Generic;
using System.Reflection;
using Arisaka;
using Maruyama.UI;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// デバッグ用。時間を止める判断画面（食事・イベント・税金・赤字警告・ミニゲーム・チュートリアル）を、プレイヤーの代わりに処理して閉じる。
/// 他メンバーのスクリプトは変更しない。公開メソッド／ボタンの onClick を呼び、無いものだけリフレクションで既存の処理を呼ぶ。
/// カレンダー・家計簿・サポート・設定など、プレイヤーが自分で開く画面には触らない。
/// </summary>
public class DebugDialogResolver
{
    public enum ChoicePolicy { 最初の選択肢, ランダム }

    /// イベントの選択肢の選び方。
    public ChoicePolicy EventChoice = ChoicePolicy.最初の選択肢;

    /// 画面が開いてから処理するまでの待ち（実時間・秒）。0 でも最低1フレームは待つ。
    public float WaitSeconds = 0.1f;

    /// 選択肢ボタンが見つからないイベント画面を、そのまま閉じるまでの待ち（実時間・秒）。
    public float StuckCloseSeconds = 1.5f;

    private struct Seen { public float time; public int frame; }

    // 画面ごとに「開いているのを最初に見た時刻とフレーム」。
    // 開いた直後のフレームは、作り直し前の古いボタン（Destroy 待ち）が残っているので押さない。
    private readonly Dictionary<int, Seen> firstSeen = new Dictionary<int, Seen>();
    private readonly HashSet<int> seenThisFrame = new HashSet<int>();
    private readonly List<int> toRemove = new List<int>();
    private readonly HashSet<string> warnedMissing = new HashSet<string>();

    /// 開いている判断画面をまとめて処理する。1つでも処理したら true。
    public bool ResolveAll()
    {
        seenThisFrame.Clear();

        bool any = false;
        any |= TryMeal();
        any |= TryEvent();
        any |= TryTax();
        any |= TryDeficit();
        any |= TryMiniGame();
        any |= TryTutorial();

        // 閉じた画面の記録を消す（次に開いたときは待ち直す）
        toRemove.Clear();
        foreach (var id in firstSeen.Keys) if (!seenThisFrame.Contains(id)) toRemove.Add(id);
        foreach (var id in toRemove) firstSeen.Remove(id);

        return any;
    }

    // ------------------------------------------------------------
    // 画面ごとの処理
    // ------------------------------------------------------------

    /// 食事の選択：画面自身のオート選択（先月と同じ／初回は「普通」）をすぐに実行する。
    private bool TryMeal()
    {
        bool any = false;
        foreach (var d in Object.FindObjectsByType<MealSelectionDialogUI>(FindObjectsSortMode.None))
        {
            if (!Ready(d)) continue;
            if (InvokePrivate(d, "ChooseAutoPlan")) any = true;
        }
        return any;
    }

    /// イベント：まだ選んでいないカードの選択肢ボタンを押す。ボタンが無いまま止まっていたら閉じる。
    private bool TryEvent()
    {
        bool any = false;
        foreach (var dlg in Object.FindObjectsByType<EventDialogUI>(FindObjectsSortMode.None))
        {
            if (!Ready(dlg)) continue;

            bool clicked = false;
            foreach (var card in dlg.GetComponentsInChildren<EventCardUI>(false))
            {
                if (card.IsCompleted) continue;
                var btn = PickChoiceButton(card);
                if (btn == null) continue;
                btn.onClick.Invoke();
                clicked = true;
            }

            if (clicked)
            {
                any = true;
            }
            else if (WaitedSeconds(dlg) >= StuckCloseSeconds && dlg.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[DebugAutoPilot] 押せる選択肢が無いイベント画面を閉じました。");
                dlg.Close();
                any = true;
            }
        }
        return any;
    }

    private Button PickChoiceButton(EventCardUI card)
    {
        var buttons = new List<Button>();
        foreach (var choice in card.GetComponentsInChildren<EventChoiceButtonUI>(false))
        {
            var b = choice.GetComponentInChildren<Button>();
            if (b != null && b.interactable && b.gameObject.activeInHierarchy) buttons.Add(b);
        }
        if (buttons.Count == 0) return null;
        return EventChoice == ChoicePolicy.ランダム ? buttons[Random.Range(0, buttons.Count)] : buttons[0];
    }

    /// 年末調整・確定申告：押せるなら「提出する／申告する」（チェックは初期状態のまま）、押せなければ「出さない／あとで」。
    private bool TryTax()
    {
        bool any = false;
        foreach (var d in Object.FindObjectsByType<TaxFilingDialog>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!d.IsOpen || !Ready(d)) continue;
            var ok = GetPrivateField<Button>(d, "okButton");
            var cancel = GetPrivateField<Button>(d, "cancelButton");
            if (ok != null && ok.interactable) ok.onClick.Invoke();
            else if (cancel != null) cancel.onClick.Invoke();
            else d.Close();
            any = true;
        }
        return any;
    }

    /// 赤字の警告：閉じるだけ。
    private bool TryDeficit()
    {
        bool any = false;
        foreach (var d in Object.FindObjectsByType<DeficitWarningUI>(FindObjectsSortMode.None))
        {
            if (!Ready(d)) continue;
            d.Close();
            any = true;
        }
        return any;
    }

    /// ミニゲーム：遊ばずに閉じる（効果は無し）。
    private bool TryMiniGame()
    {
        var m = MiniGameManager.I;
        if (m == null || !m.IsPlaying || !Ready(m)) return false;
        return InvokePrivate(m, "CloseGame");
    }

    /// チュートリアル：最後のページまで読んだことにして閉じる。
    private bool TryTutorial()
    {
        bool any = false;
        foreach (var p in Object.FindObjectsByType<TutorialPanelUI>(FindObjectsSortMode.None))
        {
            if (!p.IsOpen || !Ready(p)) continue;
            p.Close();
            any = true;
        }
        return any;
    }

    // ------------------------------------------------------------
    // 補助
    // ------------------------------------------------------------

    /// 開いているのを見たことを記録し、待ち時間が過ぎていれば true。
    private bool Ready(Object o)
    {
        int id = o.GetInstanceID();
        seenThisFrame.Add(id);
        if (!firstSeen.TryGetValue(id, out var s))
        {
            firstSeen[id] = new Seen { time = Time.unscaledTime, frame = Time.frameCount };
            return false;
        }
        return Time.frameCount > s.frame && Time.unscaledTime - s.time >= WaitSeconds;
    }

    private float WaitedSeconds(Object o)
    {
        return firstSeen.TryGetValue(o.GetInstanceID(), out var s) ? Time.unscaledTime - s.time : 0f;
    }

    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private bool InvokePrivate(object target, string method)
    {
        var m = target.GetType().GetMethod(method, Flags, null, System.Type.EmptyTypes, null);
        if (m == null)
        {
            WarnMissing(target.GetType().Name + "." + method + "()");
            return false;
        }
        m.Invoke(target, null);
        return true;
    }

    private T GetPrivateField<T>(object target, string field) where T : class
    {
        var f = target.GetType().GetField(field, Flags);
        if (f == null)
        {
            WarnMissing(target.GetType().Name + "." + field);
            return null;
        }
        return f.GetValue(target) as T;
    }

    // 名前が変わってリフレクションで見つからなくなったときに気づけるよう、1回だけ警告する
    private void WarnMissing(string name)
    {
        if (warnedMissing.Add(name))
            Debug.LogWarning($"[DebugAutoPilot] {name} が見つかりません（名前が変わった可能性）。この画面はオートで処理できません。");
    }
}
