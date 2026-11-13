using Maruyama.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Maruyama.UI
{
    /// <summary>
    /// ステータス変化を表示する単一チップ UI コンポーネント。
    /// 「体調 -15」「子ストレス +8」「親ストレス -10」などを自動整形して描画する。
    /// </summary>
    public class EffectChipUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI labelText;
        [SerializeField] private Image bgImage;

        [Header("カラー設定")]
        [SerializeField] private Color demeritColor = new Color(1.0f, 0.85f, 0.85f); // 薄赤背景
        [SerializeField] private Color demeritTextColor = new Color(0.7f, 0.1f, 0.1f); // 赤文字
        [SerializeField] private Color meritColor = new Color(0.85f, 0.95f, 0.85f); // 薄緑背景
        [SerializeField] private Color meritTextColor = new Color(0.1f, 0.5f, 0.1f); // 緑文字

        public void SetEffect(Effect effect)
        {
            if (effect == null) return;

            string targetName = GetTargetName(effect.target, effect.kind);
            string kindName = GetKindName(effect.kind);
            string sign = effect.value > 0 ? "+" : "";
            string text = $"{targetName}{kindName} {sign}{effect.value:0}";

            if (labelText != null) labelText.text = text;

            // ストレスは増加がデメリット、体調・成績等は増加がメリット
            bool isDemerit = (effect.kind == EffectKind.Stress && effect.value > 0) ||
                             (effect.kind != EffectKind.Stress && effect.value < 0);

            if (bgImage != null)
            {
                bgImage.color = isDemerit ? demeritColor : meritColor;
            }
            if (labelText != null)
            {
                labelText.color = isDemerit ? demeritTextColor : meritTextColor;
            }
        }

        private string GetTargetName(EffectTarget target, EffectKind kind)
        {
            if (kind == EffectKind.Health) return ""; // 体調の場合はプレフィックスなし ("体調 -15")
            if (kind == EffectKind.Hobby) return "";  // 趣味は子どもだけが持つのでプレフィックスなし ("野球 +20")

            switch (target)
            {
                case EffectTarget.Mother: return "母";
                case EffectTarget.Father: return "父";
                case EffectTarget.Parents: return "親";
                case EffectTarget.Child: return "子";
                case EffectTarget.Everyone: return "家族";
                default: return "";
            }
        }

        /// <summary>
        /// 趣味のチップの名前。子どもの趣味が決まっていれば、その呼び名（スポーツ＋フラグ「野球」なら「野球」）。
        /// まだ「なし」のとき（同じ選択肢で趣味を決める場合など）は「趣味」。
        /// </summary>
        private static string HobbyChipName()
        {
            var child = Household.I != null ? Household.I.Child : null;
            if (child == null || child.Hobby == HobbyKind.なし) return "趣味";
            return HobbyDisplayName.Get(child.Hobby);
        }

        private string GetKindName(EffectKind kind)
        {
            switch (kind)
            {
                case EffectKind.Money: return "お金";
                case EffectKind.Stress: return "ストレス";
                case EffectKind.Health: return "体調";
                case EffectKind.Grades: return "成績";
                case EffectKind.Hobby: return HobbyChipName();
                default: return kind.ToString();
            }
        }
    }
}
