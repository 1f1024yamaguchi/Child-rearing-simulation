using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 実行中にコードから動的に生成される Button にも自動で ButtonClickSE を付与するコンポーネント。
/// AudioManager（DontDestroyOnLoad）と同じ GameObject に付けておくことで、
/// シーン遷移後も一定間隔で全 Button を走査してクリック SE を有効化する。
/// </summary>
[DisallowMultipleComponent]
public class ButtonSeAutoAttacher : MonoBehaviour
{
    [Tooltip("シーン内の全 Button を走査して ButtonClickSE を自動付与する間隔（秒）。時間停止中も動作します。")]
    [SerializeField] private float interval = 0.25f;

    private float timer;

    private void Start()
    {
        AttachToAllButtons();
    }

    private void Update()
    {
        timer += Time.unscaledDeltaTime;
        float checkInterval = Mathf.Max(0.05f, interval);
        if (timer < checkInterval) return;

        timer = 0f;
        AttachToAllButtons();
    }

    private void AttachToAllButtons()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null) continue;

            if (!button.TryGetComponent<ButtonClickSE>(out _))
            {
                button.gameObject.AddComponent<ButtonClickSE>();
            }
        }
    }
}

