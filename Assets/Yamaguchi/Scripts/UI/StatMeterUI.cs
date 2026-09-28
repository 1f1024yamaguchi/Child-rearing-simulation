using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// ラベル＋バー＋数値の1本ぶんを表示する部品。
/// バーは Slider と Image(Filled) のどちらでも動くように両方の参照を持ち、
/// 入っているほうを使う。child-status.md 6章。
public class StatMeterUI : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("ラベルの TextMeshProUGUI（例：「成績」）")]
    [SerializeField] private TextMeshProUGUI labelText;

    [Tooltip("バー用の Slider（入っているほうを優先する）")]
    [SerializeField] private Slider slider;

    [Tooltip("バー用の Image（Filled。Slider が無いときに使う）")]
    [SerializeField] private Image fillImage;

    [Tooltip("数値表示の TextMeshProUGUI")]
    [SerializeField] private TextMeshProUGUI valueText;

    [Header("書式")]
    [Tooltip("数値のフォーマット。{0} が 0〜100 の整数値に置き換わる。例：\"{0}%\"")]
    [SerializeField] private string valueFormat = "{0}%";

    // ─── 公開 API ──────────────────────────────────────────
    /// ラベルを設定する。
    public void SetLabel(string label)
    {
        if (labelText != null) labelText.text = label;
    }

    /// 値（0〜100）を設定してバーと数値を更新する。
    public void SetValue(float value100)
    {
        float clamped = Mathf.Clamp(value100, 0f, 100f);
        float normalized = clamped / 100f;

        if (slider != null)
            slider.SetValueWithoutNotify(normalized);
        else if (fillImage != null)
            fillImage.fillAmount = normalized;

        if (valueText != null)
            valueText.text = string.Format(valueFormat, Mathf.RoundToInt(clamped));
    }

    /// 値（0〜1）を設定してバーと数値を更新する。
    public void SetValue01(float value01)
    {
        SetValue(value01 * 100f);
    }
}
