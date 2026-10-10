using TMPro;
using UnityEngine;

/// <summary>
/// ［サポート］ボタンに付ける「申請できる」制度の数のバッジ（support-apply.md 7.5、ステップ3後半）。
/// 数は SupportManager の全制度のうち GetState == Available のもの。0 なら非表示。
/// SupportManager.OnStateChanged で更新する。ボタン自体は触らない（バッジの子だけ表示を切り替える）。
/// </summary>
public class SupportButtonBadge : MonoBehaviour
{
    [SerializeField] private GameObject badgeRoot;   // 丸い背景ごと表示／非表示するオブジェクト（ボタンの子）
    [SerializeField] private TMP_Text countText;
    [SerializeField, Min(1)] private int maxShown = 99;

    private bool subscribed;

    private void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    private void Start()
    {
        Subscribe();   // OnEnable の時点で SupportManager が無かったとき用
        Refresh();
    }

    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();

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

    private void HandleStateChanged(SupportScheme s, SupportState state) => Refresh();

    public void Refresh()
    {
        var mgr = SupportManager.I;
        int count = 0;
        if (mgr != null)
        {
            foreach (var s in mgr.Schemes)
            {
                if (s == null || string.IsNullOrEmpty(s.id)) continue;
                if (mgr.GetState(s) == SupportState.Available) count++;
            }
        }

        if (badgeRoot != null) badgeRoot.SetActive(count > 0);
        if (countText != null) countText.text = count > maxShown ? maxShown + "+" : count.ToString();
    }
}
