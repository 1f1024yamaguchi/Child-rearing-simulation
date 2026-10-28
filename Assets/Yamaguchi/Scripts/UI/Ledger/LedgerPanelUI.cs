using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 家計簿画面の本体（expenses.md 5.1・依頼E5）。
/// ［月］［年］タブの切り替え、開閉、時間停止を管理する。
/// </summary>
public class LedgerPanelUI : MonoBehaviour
{
    public enum TabType { Month, Year }

    [Header("タブボタン")]
    [SerializeField] private Button monthTabButton;
    [SerializeField] private Button yearTabButton;
    [SerializeField] private TMP_Text monthTabLabel;
    [SerializeField] private TMP_Text yearTabLabel;
    [SerializeField] private Color tabSelectedColor = new Color(1.00f, 0.85f, 0.55f);
    [SerializeField] private Color tabNormalColor = Color.white;

    [Header("ビュー")]
    [SerializeField] private LedgerMonthView monthView;
    [SerializeField] private LedgerYearView yearView;
    [SerializeField] private Button closeButton;

    [Header("色の設定（Inspector で変更可能）")]
    [SerializeField] private Color incomeColor = new Color(0.18f, 0.55f, 0.22f);   // 収入（緑）
    [SerializeField] private Color expenseColor = new Color(0.80f, 0.20f, 0.20f);  // 支出・天引き（赤）
    [SerializeField] private Color supportColor = new Color(0.10f, 0.45f, 0.80f);  // 支援（青）
    [SerializeField] private Color headerColor = new Color(0.20f, 0.20f, 0.20f);   // 見出し（濃いグレー）
    [SerializeField] private Color normalColor = new Color(0.25f, 0.25f, 0.25f);   // 通常テキスト

    public Color IncomeColor => incomeColor;
    public Color ExpenseColor => expenseColor;
    public Color SupportColor => supportColor;
    public Color HeaderColor => headerColor;
    public Color NormalColor => normalColor;

    private TabType currentTab = TabType.Month;
    private bool isPausing = false;

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (monthTabButton != null) monthTabButton.onClick.AddListener(() => SwitchTab(TabType.Month));
        if (yearTabButton != null) yearTabButton.onClick.AddListener(() => SwitchTab(TabType.Year));
    }

    private void OnEnable()
    {
        // 判断画面が開いている間は時間を止める（0004・0011）
        if (!isPausing && GameClock.I != null)
        {
            GameClock.I.PushPause();
            isPausing = true;
        }
    }

    private void OnDisable()
    {
        if (isPausing && GameClock.I != null)
        {
            GameClock.I.PopPause();
            isPausing = false;
        }
    }

    /// <summary>画面を開く。最初は「今月」の［月］タブ。</summary>
    public void Open()
    {
        gameObject.SetActive(true);
        currentTab = TabType.Month;
        UpdateTabButtons();

        if (monthView != null)
        {
            monthView.gameObject.SetActive(true);
            monthView.Show(Ledger.CurrentMonthIndex(), this);
        }
        if (yearView != null)
        {
            yearView.gameObject.SetActive(false);
        }
    }

    /// <summary>画面を閉じる。</summary>
    public void Close()
    {
        gameObject.SetActive(false);
    }

    /// <summary>タブを切り替える。</summary>
    public void SwitchTab(TabType tab)
    {
        currentTab = tab;
        UpdateTabButtons();

        if (currentTab == TabType.Month)
        {
            if (monthView != null)
            {
                monthView.gameObject.SetActive(true);
                monthView.Show(Ledger.CurrentMonthIndex(), this);
            }
            if (yearView != null)
            {
                yearView.gameObject.SetActive(false);
            }
        }
        else
        {
            if (monthView != null)
            {
                monthView.gameObject.SetActive(false);
            }
            if (yearView != null)
            {
                yearView.gameObject.SetActive(true);
                yearView.Show(Ledger.CurrentMonthIndex() / 12, this);
            }
        }
    }

    /// <summary>指定した月を表示（年の棒グラフからのジャンプ用）。</summary>
    public void ShowMonth(int targetMonthIndex)
    {
        currentTab = TabType.Month;
        UpdateTabButtons();

        if (monthView != null)
        {
            monthView.gameObject.SetActive(true);
            monthView.Show(targetMonthIndex, this);
        }
        if (yearView != null)
        {
            yearView.gameObject.SetActive(false);
        }
    }

    private void UpdateTabButtons()
    {
        if (monthTabButton != null)
        {
            var img = monthTabButton.GetComponent<Image>();
            if (img != null) img.color = currentTab == TabType.Month ? tabSelectedColor : tabNormalColor;
        }
        if (yearTabButton != null)
        {
            var img = yearTabButton.GetComponent<Image>();
            if (img != null) img.color = currentTab == TabType.Year ? tabSelectedColor : tabNormalColor;
        }
    }
}
