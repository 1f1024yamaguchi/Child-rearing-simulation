using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Button の OnClick() が Inspector で未設定のときに警告を出す小さな道具（タイトル画面用）。
/// </summary>
public static class OnClickCheck
{
    public static void Warn(Button button, string label, Object context)
    {
        if (button == null)
        {
            Debug.LogWarning($"[OnClickCheck] 「{label}」の Button が Inspector で未設定です。", context);
            return;
        }
        if (button.onClick.GetPersistentEventCount() == 0)
        {
            Debug.LogWarning($"[OnClickCheck] {label}ボタンの OnClick が未設定です。（{button.name}）", button);
        }
    }
}
