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

    [Tooltip("ON にすると表示だけ反転する（100 − 値）。内部の値は変えない。例：体調（高い＝良い）を「体調不良」ゲージ（少ない＝良い）として見せる")]
    [SerializeField] private bool invertDisplay = false;

    // ─── 公開 API ──────────────────────────────────────────
    /// ラベルを設定する。
    public void SetLabel(string label)
    {
        if (labelText != null) labelText.text = label;
    }

    /// 反転表示（100 − 値）の ON/OFF を切り替える。
    public void SetInvertDisplay(bool invert)
    {
        invertDisplay = invert;
    }

    /// バーの色を「右（満タン）が緑・左（少ない）が赤」のグラデーションに差し替える。
    /// 体調のように「高いほど良い」メーター用。既定のバーは「右が赤」なので、そのままだと
    /// 元気なほど赤く見えてしまう。
    public void UseHighIsGoodGradient()
    {
        Image img = (slider != null && slider.fillRect != null)
            ? slider.fillRect.GetComponent<Image>()
            : fillImage;
        if (img == null) return;

        const int width = 64;
        var tex = new Texture2D(width, 1, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        for (int x = 0; x < width; x++)
        {
            float t = x / (float)(width - 1);
            // 色相 0（赤）→ 0.33（緑）
            tex.SetPixel(x, 0, Color.HSVToRGB(Mathf.Lerp(0f, 0.33f, t), 0.9f, 0.95f));
        }
        tex.Apply();
        img.sprite = Sprite.Create(tex, new Rect(0, 0, width, 1), new Vector2(0.5f, 0.5f), 100f);
    }

    /// 値（0〜100）を設定してバーと数値を更新する。
public void SetValue(float value100)
    {
        float clamped = Mathf.Clamp(value100, 0f, 100f);
        // 反転表示のときは 100 − 値 をバーと数値に出す
        float shown = invertDisplay ? 100f - clamped : clamped;
        float normalized = shown / 100f;

        if (slider != null)
            slider.SetValueWithoutNotify(normalized);
        else if (fillImage != null)
            fillImage.fillAmount = normalized;

        if (valueText != null)
            valueText.text = string.Format(valueFormat, Mathf.RoundToInt(shown));
    }

    /// 値（0〜1）を設定してバーと数値を更新する。
    public void SetValue01(float value01)
    {
        SetValue(value01 * 100f);
    }
}
