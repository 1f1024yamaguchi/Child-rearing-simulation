using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// タイトル画面のボタンの処理。ボタンの押下は Button の OnClick() に public メソッドを Inspector で登録する
/// （このスクリプトでは AddListener しない）。ボタンの文字はシーンの TMP に直接書き、コードでは触らない。
/// </summary>
public class TitleMenuUI : MonoBehaviour
{
    [Header("ボタン")]
    [Tooltip("「続きから」ボタン。OnClick に OnClickContinue を登録する。セーブが無いときはグレーアウトする。")]
    [SerializeField] private Button continueButton;

    [Tooltip("「最初から」ボタン。OnClick に OnClickNewGame を登録する。")]
    [SerializeField] private Button newGameButton;

    [Tooltip("「育成記録」ボタン。OnClick に OnClickRecords を登録する。本編ができるまでは押せない（interactable=false）。")]
    [SerializeField] private Button recordsButton;

    [Tooltip("「終了」ボタン。OnClick に OnClickQuit を登録する。")]
    [SerializeField] private Button quitButton;

    [Header("ダイアログ・パネル")]
    [Tooltip("「最初から」の確認ダイアログ。")]
    [SerializeField] private ConfirmDialogUI confirmDialog;

    [Tooltip("「続きから」で開くセーブ選択パネル。")]
    [SerializeField] private SaveSelectPanelUI saveSelectPanel;

    [Header("本編")]
    [Tooltip("本編のシーン名。Build Settings に登録されている必要がある（Start_scene を0番、本編を1番）。")]
    [SerializeField] private string gameSceneName = "Main_test2";

    private bool loading;

    private void Start()
    {
        bool hasSave = SaveSystem.HasAnySave();

        if (continueButton != null) continueButton.interactable = hasSave;
        if (recordsButton != null) recordsButton.interactable = false;

        Select(hasSave ? continueButton : newGameButton);

        if (saveSelectPanel != null) saveSelectPanel.OnClosed += HandleSavePanelClosed;

        // OnClick の繋ぎ忘れに気付けるように
        OnClickCheck.Warn(continueButton, "続きから", this);
        OnClickCheck.Warn(newGameButton, "最初から", this);
        OnClickCheck.Warn(recordsButton, "育成記録", this);
        OnClickCheck.Warn(quitButton, "終了", this);
        if (confirmDialog != null) confirmDialog.CheckOnClickSetup();
        else Debug.LogWarning("[TitleMenuUI] ConfirmDialogUI が未設定です。", this);
        if (saveSelectPanel != null) saveSelectPanel.CheckOnClickSetup();
        else Debug.LogWarning("[TitleMenuUI] SaveSelectPanelUI が未設定です。", this);
    }

    private void OnDestroy()
    {
        if (saveSelectPanel != null) saveSelectPanel.OnClosed -= HandleSavePanelClosed;
    }

    // ---- Button の OnClick() に登録する ----

    public void OnClickContinue()
    {
        if (saveSelectPanel == null) return;
        saveSelectPanel.Open();
    }

    public void OnClickNewGame()
    {
        if (SaveSystem.HasAnySave() && confirmDialog != null)
        {
            confirmDialog.Open(StartNewGame, () => Select(newGameButton));
        }
        else
        {
            StartNewGame();
        }
    }

    public void OnClickRecords()
    {
        Debug.Log("[TitleMenuUI] 育成記録は本編ができてから作ります（今は何もしません）。");
    }

    public void OnClickQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---- 内部 ----

    private void StartNewGame()
    {
        if (loading) return;
        loading = true;
        GameLaunchRequest.RequestNewGame();   // セーブは消さない
        SceneManager.LoadScene(gameSceneName);
    }

    private void HandleSavePanelClosed()
    {
        Select(continueButton);
    }

    private static void Select(Button button)
    {
        if (button == null || EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(button.gameObject);
    }
}
