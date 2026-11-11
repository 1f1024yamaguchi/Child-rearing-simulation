using UnityEngine;

/// リザルト画面の下の帯で、家族のキャラを左右に歩かせる（飾り）。
/// 決めた範囲の中で行き先を選んで歩き、着いたら少し立ち止まる、をくり返すだけ。
/// 絵の切り替えは UISpriteFlipbook に任せる。
[RequireComponent(typeof(RectTransform), typeof(UISpriteFlipbook))]
public class ResultCharacterWalker : MonoBehaviour
{
    [Tooltip("歩くときの絵")]
    [SerializeField] private Sprite[] walkFrames;

    [Tooltip("立ち止まっているときの絵")]
    [SerializeField] private Sprite[] idleFrames;

    [Tooltip("歩ける範囲（anchoredPosition.x の左端・右端）")]
    [SerializeField] private float minX = -300f;
    [SerializeField] private float maxX = 300f;

    [Tooltip("歩く速さ（UI の単位/秒）")]
    [SerializeField] private float speed = 40f;

    [Tooltip("立ち止まる秒数（最小・最大）")]
    [SerializeField] private Vector2 waitSeconds = new Vector2(1.5f, 4f);

    [Tooltip("絵が右向きなら ON（左へ歩くときに反転する）")]
    [SerializeField] private bool artFacesRight = true;

    private RectTransform rt;
    private UISpriteFlipbook flipbook;
    private float goalX;
    private float waitLeft;
    private bool walking;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        flipbook = GetComponent<UISpriteFlipbook>();
    }

    private void OnEnable()
    {
        // 最初は少し待ってから歩き出す（全員が同時に動き出さないように）
        walking = false;
        waitLeft = Random.Range(0f, waitSeconds.y);
        flipbook.SetFrames(idleFrames);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;   // 結果画面では時間が止まっているので unscaled
        if (!walking)
        {
            waitLeft -= dt;
            if (waitLeft > 0f) return;
            goalX = Random.Range(minX, maxX);
            walking = true;
            flipbook.SetFrames(walkFrames);
        }

        var p = rt.anchoredPosition;
        float dx = goalX - p.x;
        FaceTo(dx);
        p.x = Mathf.MoveTowards(p.x, goalX, speed * dt);
        rt.anchoredPosition = p;

        if (Mathf.Abs(goalX - p.x) < 0.5f)
        {
            walking = false;
            waitLeft = Random.Range(waitSeconds.x, waitSeconds.y);
            flipbook.SetFrames(idleFrames);
        }
    }

    private void FaceTo(float dx)
    {
        if (Mathf.Abs(dx) < 0.01f) return;
        bool goingLeft = dx < 0f;
        var s = rt.localScale;
        s.x = Mathf.Abs(s.x) * ((goingLeft == artFacesRight) ? -1f : 1f);
        rt.localScale = s;
    }
}
