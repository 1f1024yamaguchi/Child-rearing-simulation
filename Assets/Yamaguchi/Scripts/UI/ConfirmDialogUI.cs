using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// はい／いいえの汎用ダイアログ。タイトル以外でも使い回せる。文言は持たない（シーンの TMP に直接書く）。
/// ボタンの押下は Button の OnClick() に OnClickYes / OnClickNo / OnClickBackground を Inspector で登録する。
/// このスクリプトは「常に有効なオブジェクト」に付けること（ダイアログのルートに付けると、閉じている間 Update が動かない）。
/// </summary>
public class ConfirmDialogUI : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("ダイアログ全体のルート（暗幕を含む）。開閉は SetActive で行う。このスクリプト自身とは別のオブジェクトにする。")]
    [SerializeField] private GameObject root;

    [Tooltip("「はい」ボタン。OnClick に OnClickYes を登録する。")]
    [SerializeField] private Button yesButton;

    [Tooltip("「いいえ」ボタン。OnClick に OnClickNo を登録する。開いたとき既定で選ばれる。")]
    [SerializeField] private Button noButton;

    [Tooltip("背面の暗幕 Button（任意。OnClick に OnClickBackground を登録する）。未設定の警告チェック用。")]
    [SerializeField] private Button backgroundButton;

    private Action onYes;
    private Action onNo;

    public bool IsOpen => root != null && root.activeSelf;

    private void Start()
    {
        if (root != null) root.SetActive(false);
    }

    /// <summary>表示して、既定の選択を「いいえ」にする。</summary>
    public void Open(Action onYes, Action onNo = null)
    {
        this.onYes = onYes;
        this.onNo = onNo;
        if (root != null) root.SetActive(true);
        if (EventSystem.current != null && noButton != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(noButton.gameObject);
        }
    }

    public void OnClickYes()
    {
        var cb = onYes;
        Close();
        cb?.Invoke();
    }

    public void OnClickNo()
    {
        var cb = onNo;
        Close();
        cb?.Invoke();
    }

    /// <summary>暗幕のクリック。いいえと同じ。</summary>
    public void OnClickBackground() => OnClickNo();

    private void Close()
    {
        onYes = null;
        onNo = null;
        if (root != null) root.SetActive(false);
    }

    private void Update()
    {
        if (!IsOpen) return;
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) OnClickNo();
    }

    /// <summary>OnClick の繋ぎ忘れを警告する（TitleMenuUI.Start から呼ぶ）。</summary>
    public void CheckOnClickSetup()
    {
        OnClickCheck.Warn(yesButton, "ダイアログの「はい」", this);
        OnClickCheck.Warn(noButton, "ダイアログの「いいえ」", this);
        OnClickCheck.Warn(backgroundButton, "ダイアログの暗幕", this);
    }
}
