using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// ストレスの段階を色付きバッジで表示する。
/// 母・父・子どのカードにも使える。
/// ストレスの段階ごとに表示名と色を Inspector から変えられる。
/// child-status.md 6章・rules-and-stats.md「ストレス値の段階」。
///
/// ※ これは風邪気味などの状態バッジではない。
///   ストレス値を分かりやすくするものなので混同しないこと。
public class StressBadgeUI : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("バッジの背景 Image（色を変える対象）")]
    [SerializeField] private Image badgeImage;

    [Tooltip("バッジのテキスト（\"好調\" など）")]
    [SerializeField] private TextMeshProUGUI badgeText;

    [Header("良好（ストレス 0–30）")]
    [SerializeField] private string label_良好 = "好調";
    [SerializeField] private Color color_良好 = new Color(0.2f, 0.7f, 0.2f, 1f); // 緑

    [Header("普通（ストレス 31–70）")]
    [SerializeField] private string label_普通 = "普通";
    [SerializeField] private Color color_普通 = new Color(0.5f, 0.5f, 0.5f, 1f); // 灰

    [Header("危険（ストレス 71–90）")]
    [SerializeField] private string label_危険 = "注意";
    [SerializeField] private Color color_危険 = new Color(0.9f, 0.2f, 0.2f, 1f); // 赤

    [Header("限界（ストレス 91–100）")]
    [SerializeField] private string label_限界 = "限界";
    [SerializeField] private Color color_限界 = new Color(0.5f, 0f, 0f, 1f); // 濃い赤

    private PersonStatus target;

    // ─── バインド ─────────────────────────────────────────
    /// 対象の PersonStatus を設定し、変更通知を購読して表示を初期化する。
    public void Bind(PersonStatus person)
    {
        if (target != null) target.OnStressChanged -= OnStressChanged;
        target = person;
        if (target != null)
        {
            target.OnStressChanged += OnStressChanged;
            UpdateBadge(target.StressLevel);
        }
    }

    private void OnDisable()
    {
        if (target != null) target.OnStressChanged -= OnStressChanged;
    }

    private void OnEnable()
    {
        if (target != null)
        {
            target.OnStressChanged += OnStressChanged;
            UpdateBadge(target.StressLevel);
        }
    }

    // ─── 内部 ────────────────────────────────────────────
    private void OnStressChanged(int _) => UpdateBadge(target.StressLevel);

    private void UpdateBadge(StressLevel level)
    {
        string label;
        Color color;
        switch (level)
        {
            case StressLevel.良好: label = label_良好; color = color_良好; break;
            case StressLevel.普通: label = label_普通; color = color_普通; break;
            case StressLevel.危険: label = label_危険; color = color_危険; break;
            default:               label = label_限界; color = color_限界; break;
        }
        if (badgeText  != null) badgeText.text = label;
        if (badgeImage != null) badgeImage.color = color;
    }
}
