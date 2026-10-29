using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 赤字発生時の警告ダイアログ（expenses.md 5.7・依頼E4）。
/// 赤字の月に時間を止めて開き、家計簿やサポート画面への導線を出す。
/// 非表示で置いておき、BankruptcyWatcher が Open() を呼んで開く（依頼E4-2。非表示だと Start が呼ばれず、自分では購読できないため）。
/// </summary>
public class DeficitWarningUI : MonoBehaviour
{
    [Header("UI要素")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button ledgerButton;
    [SerializeField] private Button supportButton;
    [SerializeField] private Button closeButton;

    [Header("参照（未設定なら自動検索）")]
    [SerializeField] private LedgerPanelUI ledgerPanel;
    [SerializeField] private SupportPanelController supportPanel;

    private bool isPausing = false;

    private void Awake()
    {
        if (ledgerButton != null) ledgerButton.onClick.AddListener(OnLedgerClicked);
        if (supportButton != null) supportButton.onClick.AddListener(OnSupportClicked);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    private void OnEnable()
    {
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

    public void Open()
    {
        if (ledgerPanel == null) ledgerPanel = FindFirstObjectByType<LedgerPanelUI>(FindObjectsInactive.Include);
        if (supportPanel == null) supportPanel = FindFirstObjectByType<SupportPanelController>(FindObjectsInactive.Include);

        int shortage = GameState.I != null ? Mathf.Max(0, -GameState.I.Money) : 0;
        if (messageText != null)
        {
            messageText.text = $"今月は <color=#D32F2F>{shortage:N0}円</color> 足りませんでした。\n来月も足りないと家計が破綻します。";
        }

        gameObject.SetActive(true);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    private void OnLedgerClicked()
    {
        Close();

        if (ledgerPanel == null) ledgerPanel = FindFirstObjectByType<LedgerPanelUI>(FindObjectsInactive.Include);
        if (ledgerPanel != null)
        {
            ledgerPanel.Open();
        }
        else
        {
            Debug.LogWarning("[DeficitWarningUI] LedgerPanelUI が見つかりません");
        }
    }

    private void OnSupportClicked()
    {
        Close();

        if (supportPanel == null) supportPanel = FindFirstObjectByType<SupportPanelController>(FindObjectsInactive.Include);
        if (supportPanel != null)
        {
            supportPanel.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[DeficitWarningUI] SupportPanelController が見つかりません");
        }
    }
}
