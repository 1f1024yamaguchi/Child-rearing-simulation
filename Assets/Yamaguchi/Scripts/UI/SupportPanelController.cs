using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// サポート画面（制度一覧）の管理（support-apply.md 7.5〜7.6、ステップ3前半）。
/// - 開いたら選んでいるタブの制度を SupportManager から読み、SupportCard プレハブを並べる
/// - タブ（政府／地方自治体／会社）を押したら、その種類の制度だけに並べ替える
/// - タブに「申請できる」制度の数を出す
/// - 開いている間はゲーム内時間を止める（OnEnable で PushPause、OnDisable で PopPause）
/// カード1枚の表示・更新は SupportSchemeCard に任せる（ここではカードを作るだけ）。
/// </summary>
public class SupportPanelController : MonoBehaviour
{
    [Serializable]
    public class Tab
    {
        public SupportCategory category;
        public Button button;
        public TMP_Text label;          // 「政府（2）」のように数を出す
        [HideInInspector] public string baseText;
    }

    [Header("カード")]
    [SerializeField] private SupportSchemeCard cardPrefab;    // Assets/Yamaguchi/Prefabs/UI/SupportCard.prefab
    [SerializeField] private RectTransform content;           // scheme_scroll/Viewport/Content
    [SerializeField] private ScrollRect scroll;

    [Header("タブ")]
    [SerializeField] private List<Tab> tabs = new List<Tab>();
    [SerializeField] private SupportCategory startCategory = SupportCategory.Government;
    [SerializeField] private Color tabSelectedColor = new Color(1.00f, 0.85f, 0.55f);
    [SerializeField] private Color tabNormalColor   = Color.white;

    private readonly List<SupportSchemeCard> cards = new List<SupportSchemeCard>();
    private SupportCategory current;
    private bool pausing;      // 自分が PushPause したか（PopPause をペアで呼ぶため）
    private bool subscribed;
    private bool tabsWired;

    // ------------------------------------------------------------
    // ライフサイクル
    // ------------------------------------------------------------

    private void Awake()
    {
        current = startCategory;
        WireTabs();
    }

    private void OnEnable()
    {
        TryPause();
        if (SupportManager.I != null) Open(current);   // SupportManager がまだ無ければ Start でやる
    }

    private void Start()
    {
        TryPause();          // シーン開始時に GameClock より先に OnEnable が来た場合
        if (cards.Count == 0) Open(current);
    }

    private void OnDisable()
    {
        if (pausing && GameClock.I != null) GameClock.I.PopPause();
        pausing = false;
        Unsubscribe();
    }

    private void OnDestroy()
    {
        foreach (var t in tabs)
            if (t != null && t.button != null) t.button.onClick.RemoveAllListeners();
    }

    private void TryPause()
    {
        if (pausing || GameClock.I == null) return;
        GameClock.I.PushPause();
        pausing = true;
    }

    // ------------------------------------------------------------
    // 公開窓口
    // ------------------------------------------------------------

    /// <summary>タブを切り替えてカードを並べ直す。</summary>
    public void Open(SupportCategory category)
    {
        current = category;
        Subscribe();
        Rebuild();
        UpdateTabs();
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;   // 一番上から
    }

    /// <summary>
    /// 指定した制度のタブを開き、そのカードまでスクロールする（カレンダーのリマインダーから呼ぶ → calendar-system.md 2.4）。
    /// 画面が閉じていれば開く。
    /// </summary>
    public void Open(string schemeId)
    {
        var scheme = FindScheme(schemeId);
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (scheme == null)
        {
            Debug.LogWarning($"SupportPanelController: 制度が見つかりません：{schemeId}");
            return;
        }
        Open(scheme.category);
        ScrollTo(scheme);
    }

    // ------------------------------------------------------------
    // カードを並べる
    // ------------------------------------------------------------

    private void Rebuild()
    {
        if (content == null || cardPrefab == null || SupportManager.I == null) return;

        // 前のカード（シーンに置いた仮カードも含む）を消す
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            var child = content.GetChild(i).gameObject;
            child.SetActive(false);   // Destroy はフレームの最後なので、レイアウト計算から先に外す
            Destroy(child);
        }
        cards.Clear();

        foreach (var s in SupportManager.I.GetByCategory(current))
        {
            var card = Instantiate(cardPrefab, content);
            card.name = "card_" + s.id;
            card.Bind(s);
            cards.Add(card);
        }
    }

    private void ScrollTo(SupportScheme scheme)
    {
        if (scroll == null || content == null) return;
        int index = cards.FindIndex(c => c.Scheme == scheme);
        if (index < 0 || cards.Count <= 1) return;

        Canvas.ForceUpdateCanvases();
        var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
        float overflow = content.rect.height - viewport.rect.height;
        if (overflow <= 0f) return;

        var cardRt = (RectTransform)cards[index].transform;
        float top = -cardRt.anchoredPosition.y - cardRt.rect.height * (1f - cardRt.pivot.y);   // Content 上端からの距離
        scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(top / overflow);
    }

    // ------------------------------------------------------------
    // タブ
    // ------------------------------------------------------------

    private void WireTabs()
    {
        if (tabsWired) return;
        foreach (var t in tabs)
        {
            if (t == null || t.button == null) continue;
            if (t.label != null) t.baseText = t.label.text.Trim();
            var category = t.category;   // ラムダに渡すためのコピー
            t.button.onClick.AddListener(() => Open(category));   // onClick は実行時に配線する
        }
        tabsWired = true;
    }

    private void UpdateTabs()
    {
        if (SupportManager.I == null) return;
        foreach (var t in tabs)
        {
            if (t == null) continue;

            int available = 0;
            foreach (var s in SupportManager.I.GetByCategory(t.category))
                if (SupportManager.I.GetState(s) == SupportState.Available) available++;

            if (t.label != null)
                t.label.text = available > 0 ? $"{t.baseText}（{available}）" : t.baseText;

            if (t.button != null && t.button.targetGraphic != null)
                t.button.targetGraphic.color = t.category == current ? tabSelectedColor : tabNormalColor;
        }
    }

    // ------------------------------------------------------------
    // イベント
    // ------------------------------------------------------------

    private void Subscribe()
    {
        if (subscribed || SupportManager.I == null) return;
        SupportManager.I.OnStateChanged += HandleStateChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (SupportManager.I != null) SupportManager.I.OnStateChanged -= HandleStateChanged;
        subscribed = false;
    }

    // カードは自分で表示を更新するので、ここではタブの数だけ直す
    private void HandleStateChanged(SupportScheme s, SupportState state) => UpdateTabs();

    private static SupportScheme FindScheme(string id)
    {
        if (SupportManager.I == null || string.IsNullOrEmpty(id)) return null;
        foreach (var s in SupportManager.I.Schemes)
            if (s != null && s.id == id) return s;
        return null;
    }
}
