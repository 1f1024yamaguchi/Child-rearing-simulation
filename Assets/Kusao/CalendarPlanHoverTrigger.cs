using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// カレンダーの各日付マスまたは予定チップに付与され、
/// マウスホバー（カーソルを合わせたとき）に詳細ポップアップを表示するトリガー。
/// </summary>
public class CalendarPlanHoverTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private int boundAgeDays = -1;

    public void Bind(int ageDays)
    {
        boundAgeDays = ageDays;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (boundAgeDays < 0 || CalendarManager.I == null) return;

        List<CalendarPlan> plans = CalendarManager.I.GetPlansForDay(boundAgeDays);
        if (plans != null && plans.Count > 0)
        {
            if (CalendarDetailPopup.I != null)
            {
                CalendarDetailPopup.I.Show(plans, transform.position);
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (CalendarDetailPopup.I != null)
        {
            CalendarDetailPopup.I.Hide();
        }
    }

    private void OnDisable()
    {
        if (CalendarDetailPopup.I != null)
        {
            CalendarDetailPopup.I.Hide();
        }
    }
}
