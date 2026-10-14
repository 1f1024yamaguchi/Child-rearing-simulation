using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 画面下の［セーブ］ボタン（Canvas/Buttons/save）に付ける。
/// 押したら SaveSystem.Save() を呼び、SupportToast で「セーブしました」と出す。
/// onClick は実行時に AddListener で配線する（Inspector の onClick は使わない）。
/// </summary>
[RequireComponent(typeof(Button))]
public class SaveButton : MonoBehaviour
{
    [SerializeField] private SupportToast toast;   // 空なら シーン内から探す

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        button.onClick.AddListener(OnClick);
    }

    private void OnDisable()
    {
        button.onClick.RemoveListener(OnClick);
    }

    private void OnClick()
    {
        if (AutoSaver.GameEnded) return;   // 終わったゲーム（ゴール・家出）は保存しない

        bool ok = SaveSystem.Save();
        if (toast == null) toast = FindFirstObjectByType<SupportToast>(FindObjectsInactive.Include);
        if (toast != null) toast.Show(ok ? "セーブしました" : "セーブに失敗しました");
    }
}
