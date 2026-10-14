using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 「続きから」で開くセーブ選択パネル。カード（オート・手動）は人間がシーンに置く。
/// ボタンの押下は Button の OnClick() に OnClickAutoSlot / OnClickManualSlot / OnClickBack / OnClickBackground を Inspector で登録する。
/// このスクリプトは「常に有効なオブジェクト」に付けること（パネルのルートに付けると、閉じている間 Update が動かない）。
/// </summary>
public class SaveSelectPanelUI : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("パネル全体のルート（暗幕を含む）。開閉は SetActive で行う。このスクリプト自身とは別のオブジェクトにする。")]
    [SerializeField] private GameObject root;

    [Tooltip("オートセーブ用のカード。")]
    [SerializeField] private SaveSlotCardUI autoCard;

    [Tooltip("手動セーブ用のカード。")]
    [SerializeField] private SaveSlotCardUI manualCard;

    [Tooltip("「もどる」ボタン。OnClick に OnClickBack を登録する。")]
    [SerializeField] private Button backButton;

    [Tooltip("背面の暗幕 Button（OnClick に OnClickBackground を登録する）。未設定の警告チェック用。")]
    [SerializeField] private Button backgroundButton;

    [Header("本編")]
    [Tooltip("本編のシーン名。Build Settings に登録されている必要がある。")]
    [SerializeField] private string gameSceneName = "Main_test2";

    /// <summary>パネルを閉じたとき（もどる・暗幕・Esc）に通知する。</summary>
    public event Action OnClosed;

    private bool loading;

    public bool IsOpen => root != null && root.activeSelf;

    private void Start()
    {
        if (root != null) root.SetActive(false);
    }

    /// <summary>2枠の見出しを読んでカードに渡して表示する。既定の選択は保存日時が新しいほう。</summary>
    public void Open()
    {
        var a = SaveSystem.GetSlotInfo(SaveSlot.Auto);
        var m = SaveSystem.GetSlotInfo(SaveSlot.Manual);
        if (autoCard != null) autoCard.Show(a);
        if (manualCard != null) manualCard.Show(m);
        if (root != null) root.SetActive(true);

        SaveSlot first = SaveSlot.Auto;
        if (!a.exists && m.exists) first = SaveSlot.Manual;
        else if (a.exists && m.exists && SaveSystem.ParseSavedAt(m.savedAt) > SaveSystem.ParseSavedAt(a.savedAt)) first = SaveSlot.Manual;

        var card = first == SaveSlot.Auto ? autoCard : manualCard;
        if (EventSystem.current != null && card != null && card.Button != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(card.Button.gameObject);
        }
    }

    public void OnClickAutoSlot() => Launch(SaveSlot.Auto);

    public void OnClickManualSlot() => Launch(SaveSlot.Manual);

    public void OnClickBack() => Close();

    public void OnClickBackground() => Close();

    private void Launch(SaveSlot slot)
    {
        if (loading) return;
        loading = true;
        GameLaunchRequest.RequestContinue(slot);
        SceneManager.LoadScene(gameSceneName);
    }

    private void Close()
    {
        if (!IsOpen) return;
        root.SetActive(false);
        OnClosed?.Invoke();
    }

    private void Update()
    {
        if (!IsOpen) return;
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) Close();
    }

    /// <summary>OnClick の繋ぎ忘れを警告する（TitleMenuUI.Start から呼ぶ）。</summary>
    public void CheckOnClickSetup()
    {
        OnClickCheck.Warn(autoCard != null ? autoCard.Button : null, "オートセーブのカード", this);
        OnClickCheck.Warn(manualCard != null ? manualCard.Button : null, "手動セーブのカード", this);
        OnClickCheck.Warn(backButton, "セーブ選択の「もどる」", this);
        OnClickCheck.Warn(backgroundButton, "セーブ選択の暗幕", this);
    }
}
