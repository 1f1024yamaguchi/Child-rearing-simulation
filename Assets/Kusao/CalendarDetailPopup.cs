using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// カレンダーのセルで予定にカーソルを合わせたときに表示される詳細ポップアップ。
/// 制度の名称・期限日・残り日数・詳細な説明・メリット等を大きく見やすく表示する。
/// </summary>
public class CalendarDetailPopup : MonoBehaviour
{
    private static CalendarDetailPopup instance;
    public static CalendarDetailPopup I => instance;

    [Header("UI 参照")]
    [SerializeField] private RectTransform cardRect;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI deadlineText;
    [SerializeField] private TextMeshProUGUI categoryBadgeText;
    [SerializeField] private Image categoryBadgeImg;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI meritsText;

    private Canvas parentCanvas;
    private RectTransform canvasRect;

    private void Awake()
    {
        instance = this;
        EnsureUIBuilt();
        Hide();
    }

    /// <summary>
    /// カレンダー用のポップアップを作成・取得する。
    /// </summary>
    public static CalendarDetailPopup GetOrCreate(Transform parent)
    {
        if (instance != null) return instance;

        var go = new GameObject("CalendarDetailPopup", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var popup = go.AddComponent<CalendarDetailPopup>();
        return popup;
    }

    /// <summary>
    /// 予定リストと表示基準位置（ワールド座標またはスクリーン座標）を指定してポップアップを表示する。
    /// </summary>
    public void Show(List<CalendarPlan> plans, Vector3 targetWorldPos)
    {
        if (plans == null || plans.Count == 0)
        {
            Hide();
            return;
        }

        EnsureUIBuilt();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        var plan = plans[0]; // 最初の予定をメインで表示（複数ある場合はまとめて表示）
        SupportScheme scheme = null;
        if (SupportManager.I != null && !string.IsNullOrEmpty(plan.schemeId))
        {
            scheme = SupportManager.I.GetScheme(plan.schemeId);
        }

        // タイトル
        string kindLabel = plan.kind == PlanKind.Renew ? "【更新手続き】" : "【申請期限】";
        titleText.text = $"📅 {plan.title} {kindLabel}";

        // 期限日 & 残り日数
        int today = (GameState.I != null) ? Mathf.FloorToInt(GameState.I.AgeDays) : 0;
        int diff = plan.ageDays - today;
        string remainStr = diff > 0 ? $"（あと {diff} 日）" : (diff == 0 ? "（本日締め切り！）" : $"（{Mathf.Abs(diff)} 日前）");
        deadlineText.text = $"⏰ 期限：<b>{GameDate.Format(plan.ageDays)}</b> {remainStr}";
        deadlineText.color = diff <= 7 ? new Color(0.9f, 0.2f, 0.2f, 1f) : new Color(0.2f, 0.2f, 0.2f, 1f);

        // カテゴリバッジ
        if (scheme != null)
        {
            categoryBadgeText.text = CategoryToString(scheme.category);
            categoryBadgeImg.gameObject.SetActive(true);
        }
        else
        {
            categoryBadgeImg.gameObject.SetActive(false);
        }

        // 説明文
        if (scheme != null && !string.IsNullOrEmpty(scheme.description))
        {
            descriptionText.text = scheme.description;
            descriptionText.gameObject.SetActive(true);
        }
        else
        {
            descriptionText.text = "メモまたは詳細情報はありません。";
            descriptionText.gameObject.SetActive(true);
        }

        // メリット・支給内容・複数予定
        var sb = new StringBuilder();
        if (scheme != null)
        {
            if (scheme.merits != null && scheme.merits.Count > 0)
            {
                sb.AppendLine("<b>【メリット・効果】</b>");
                foreach (var m in scheme.merits)
                {
                    sb.AppendLine($"・{m}");
                }
            }

            // 毎月の支給額（あれば）
            if (scheme.monthlyTiers != null && scheme.monthlyTiers.Count > 0)
            {
                var t = scheme.monthlyTiers[0];
                if (t != null && t.MoneyAmount() > 0)
                {
                    sb.AppendLine($"・支給目安：月額 {t.MoneyAmount():N0} 円");
                }
            }
        }

        // 他の予定があれば追加
        if (plans.Count > 1)
        {
            sb.AppendLine();
            sb.AppendLine($"<b>【同日の他の予定（+{plans.Count - 1}件）】</b>");
            for (int i = 1; i < plans.Count; i++)
            {
                sb.AppendLine($"・{plans[i].title}");
            }
        }

        if (sb.Length > 0)
        {
            meritsText.text = sb.ToString().TrimEnd();
            meritsText.gameObject.SetActive(true);
        }
        else
        {
            meritsText.gameObject.SetActive(false);
        }

        // ポップアップ位置の調整
        UpdatePosition(targetWorldPos);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void UpdatePosition(Vector3 targetWorldPos)
    {
        if (cardRect == null) return;

        // ワールド座標からポップアップ親のローカル座標へ変換
        Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(null, targetWorldPos);
        RectTransform parentRT = transform.parent as RectTransform;
        if (parentRT != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRT, screenPos, null, out Vector2 localPos))
        {
            // セルの少し上か下に表示
            float offsetX = 0f;
            float offsetY = 60f; // セルの上部

            Vector2 newPos = localPos + new Vector2(offsetX, offsetY);

            // 画面境界内に収まるようクランプ
            Rect parentRect = parentRT.rect;
            float halfW = cardRect.rect.width * 0.5f;
            float halfH = cardRect.rect.height;

            if (newPos.y + halfH > parentRect.yMax - 20f)
            {
                // 上にはみ出る場合は下側に表示
                newPos.y = localPos.y - 70f - halfH;
            }

            newPos.x = Mathf.Clamp(newPos.x, parentRect.xMin + halfW + 10f, parentRect.xMax - halfW - 10f);
            cardRect.anchoredPosition = newPos;
        }
    }

    private string CategoryToString(SupportCategory cat)
    {
        switch (cat)
        {
            case SupportCategory.Government:   return "政府の支援";
            case SupportCategory.Municipality: return "自治体の支援";
            case SupportCategory.Company:      return "会社の制度";
            case SupportCategory.App:          return "アプリ・生活";
            default:                           return "支援制度";
        }
    }

    // ---------------- UI 動的構築 ----------------

    private void EnsureUIBuilt()
    {
        if (cardRect != null) return;

        var rootRT = GetComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        // メインカード
        var cardGO = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        cardGO.transform.SetParent(transform, false);
        cardRect = cardGO.GetComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(340f, 0f);
        cardRect.pivot = new Vector2(0.5f, 0f);

        var cardImg = cardGO.GetComponent<Image>();
        cardImg.color = new Color(0.98f, 0.98f, 1f, 0.98f); // わずかに青みがかったクリーンな白

        var vlg = cardGO.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(14, 14, 12, 12);
        vlg.spacing = 8f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var csf = cardGO.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 枠線効果（Outline 代替または子要素でのアクセントバー）
        var topBarGO = new GameObject("TopBar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        topBarGO.transform.SetParent(cardGO.transform, false);
        var topBarImg = topBarGO.GetComponent<Image>();
        topBarImg.color = new Color(0.16f, 0.83f, 0.96f, 1f); // シアンアクセント
        var topBarLe = topBarGO.GetComponent<LayoutElement>();
        topBarLe.minHeight = 4f;
        topBarLe.preferredHeight = 4f;

        // ヘッダー行（カテゴリバッジ + タイトル）
        var headerRow = new GameObject("HeaderRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        headerRow.transform.SetParent(cardGO.transform, false);
        var hlg = headerRow.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;

        var hLe = headerRow.GetComponent<LayoutElement>();
        hLe.minHeight = 24f;

        // バッジ
        var badgeGO = new GameObject("Badge", typeof(RectTransform), typeof(Image));
        badgeGO.transform.SetParent(headerRow.transform, false);
        categoryBadgeImg = badgeGO.GetComponent<Image>();
        categoryBadgeImg.color = new Color(0.2f, 0.6f, 0.9f, 1f);
        var badgeRT = badgeGO.GetComponent<RectTransform>();
        badgeRT.sizeDelta = new Vector2(85f, 22f);

        var badgeTxtGO = new GameObject("Text", typeof(RectTransform));
        badgeTxtGO.transform.SetParent(badgeGO.transform, false);
        categoryBadgeText = badgeTxtGO.AddComponent<TextMeshProUGUI>();
        categoryBadgeText.text = "政府";
        categoryBadgeText.fontSize = 12;
        categoryBadgeText.fontStyle = FontStyles.Bold;
        categoryBadgeText.alignment = TextAlignmentOptions.Center;
        categoryBadgeText.color = Color.white;
        categoryBadgeText.rectTransform.anchorMin = Vector2.zero;
        categoryBadgeText.rectTransform.anchorMax = Vector2.one;
        categoryBadgeText.rectTransform.offsetMin = Vector2.zero;
        categoryBadgeText.rectTransform.offsetMax = Vector2.zero;

        // タイトル
        var titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(cardGO.transform, false);
        titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.text = "📅 制度名";
        titleText.fontSize = 17;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(0.12f, 0.15f, 0.2f, 1f);
        titleText.enableWordWrapping = true;

        // 期限日
        var deadlineGO = new GameObject("Deadline", typeof(RectTransform));
        deadlineGO.transform.SetParent(cardGO.transform, false);
        deadlineText = deadlineGO.AddComponent<TextMeshProUGUI>();
        deadlineText.text = "⏰ 期限：0歳 4月30日（あと 10 日）";
        deadlineText.fontSize = 13;
        deadlineText.fontStyle = FontStyles.Bold;
        deadlineText.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        // 区切り線
        var divGO = new GameObject("Divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        divGO.transform.SetParent(cardGO.transform, false);
        var divImg = divGO.GetComponent<Image>();
        divImg.color = new Color(0.85f, 0.88f, 0.9f, 1f);
        var divLe = divGO.GetComponent<LayoutElement>();
        divLe.minHeight = 1f;
        divLe.preferredHeight = 1f;

        // 説明文
        var descGO = new GameObject("Description", typeof(RectTransform));
        descGO.transform.SetParent(cardGO.transform, false);
        descriptionText = descGO.AddComponent<TextMeshProUGUI>();
        descriptionText.text = "制度の説明文がここに入ります。";
        descriptionText.fontSize = 12;
        descriptionText.lineSpacing = 1.2f;
        descriptionText.color = new Color(0.25f, 0.28f, 0.32f, 1f);
        descriptionText.enableWordWrapping = true;

        // メリット
        var meritsGO = new GameObject("Merits", typeof(RectTransform));
        meritsGO.transform.SetParent(cardGO.transform, false);
        meritsText = meritsGO.AddComponent<TextMeshProUGUI>();
        meritsText.text = "・月額 15,000円";
        meritsText.fontSize = 12;
        meritsText.lineSpacing = 1.2f;
        meritsText.color = new Color(0.1f, 0.5f, 0.25f, 1f);
        meritsText.enableWordWrapping = true;
    }
}
