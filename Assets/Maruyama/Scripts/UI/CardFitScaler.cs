using UnityEngine;
using UnityEngine.UI;

namespace Maruyama.UI
{
    /// <summary>
    /// カードが画面（親キャンバス）に収まるまで縮小する。
    /// ・高さ：キャンバスの高さに収める
    /// ・幅：同じ親の下で表示中のカード枚数（メリット／デメリットの2枚並びなど）ぶんの幅を、キャンバスの幅に収める
    /// カードが横並びのレイアウトグループの子のときは、並び全体（親パネル）を縮める
    /// （カード単独で縮めると、並ぶ位置がずれて画面の外に出るため）。
    /// 画面の比率が変わっても、選択肢がはみ出さないようにする。
    /// </summary>
    [DisallowMultipleComponent]
    public class CardFitScaler : MonoBehaviour
    {
        [Tooltip("画面の高さのうち、カードが使ってよい割合")]
        [SerializeField, Range(0.5f, 1f)] private float maxHeightRatio = 0.96f;

        [Tooltip("画面の幅のうち、カード全体（並んだ枚数ぶん）が使ってよい割合")]
        [SerializeField, Range(0.5f, 1f)] private float maxWidthRatio = 0.98f;

        private RectTransform rt;

        private void LateUpdate()
        {
            if (rt == null) rt = (RectTransform)transform;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var canvasRect = canvas.rootCanvas.GetComponent<RectTransform>();
            float cardH = rt.rect.height;
            float cardW = rt.rect.width;
            if (cardH <= 1f || cardW <= 1f) return;

            // 並び（親が横並びグループ）なら親パネルごと縮める。そうでなければカード自身を縮める
            Transform target = transform;
            int count = 1;
            float spacing = 0f;
            var group = transform.parent != null ? transform.parent.GetComponent<HorizontalOrVerticalLayoutGroup>() : null;
            if (group != null)
            {
                target = transform.parent;
                spacing = group.spacing;
                count = 0;
                foreach (Transform sib in transform.parent)
                {
                    if (sib.gameObject.activeSelf && sib.GetComponent<EventCardUI>() != null) count++;
                }
                if (count < 1) count = 1;
            }

            float limitH = canvasRect.rect.height * maxHeightRatio;
            float limitW = canvasRect.rect.width * maxWidthRatio;
            float totalW = cardW * count + spacing * (count - 1);
            float s = Mathf.Min(1f, limitH / cardH, limitW / totalW);
            if (!Mathf.Approximately(target.localScale.x, s)) target.localScale = new Vector3(s, s, 1f);
        }
    }
}
