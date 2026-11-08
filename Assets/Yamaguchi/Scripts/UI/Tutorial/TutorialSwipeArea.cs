using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 指・マウスで横にドラッグした量と速さを、Canvas の単位で知らせるだけの部品。
/// ページを送るかどうかの判断は TutorialPanelUI がする。
/// Raycast Target が ON の Image と同じオブジェクトに付ける（当たり判定が無いとドラッグを拾えない）。
/// </summary>
public class TutorialSwipeArea : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    /// ドラッグを始めた。
    public event Action DragBegan;
    /// ドラッグ中。引数は押した位置からの横の移動量（Canvas の単位。右が＋）。
    public event Action<float> Dragged;
    /// 指を離した。引数は 横の移動量 と 離す直前の横の速さ（Canvas の単位/秒）。
    public event Action<float, float> DragEnded;

    private Canvas canvas;
    private float startX;
    private float lastX;
    private float lastTime;
    private float velocity;

    public void OnBeginDrag(PointerEventData e)
    {
        float x = ToCanvasX(e);
        startX = lastX = x;
        lastTime = Time.unscaledTime;
        velocity = 0f;
        DragBegan?.Invoke();
    }

    public void OnDrag(PointerEventData e)
    {
        float x = ToCanvasX(e);
        float now = Time.unscaledTime;
        float dt = now - lastTime;
        if (dt > 0.0001f) velocity = Mathf.Lerp(velocity, (x - lastX) / dt, 0.5f);
        lastX = x;
        lastTime = now;
        Dragged?.Invoke(x - startX);
    }

    public void OnEndDrag(PointerEventData e)
    {
        float x = ToCanvasX(e);
        // 止めてから離したときは「はじいた」とみなさない
        if (Time.unscaledTime - lastTime > 0.1f) velocity = 0f;
        DragEnded?.Invoke(x - startX, velocity);
    }

    // 画面のピクセルを Canvas の単位に直す（CanvasScaler で拡大縮小されているため）
    private float ToCanvasX(PointerEventData e)
    {
        if (canvas == null)
        {
            var c = GetComponentInParent<Canvas>();
            canvas = c != null ? c.rootCanvas : null;
        }
        float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        return e.position.x / scale;
    }
}
