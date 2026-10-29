using UnityEngine;

/// 高架の電車を「ときどき」通過させる。
/// 画面外で待つ → 左右どちらかから走ってきて反対側へ抜ける → また待つ、をくり返す。
[RequireComponent(typeof(SpriteRenderer))]
public class TrainPass : MonoBehaviour
{
    [Tooltip("走る速さ（ユニット/秒）")]
    [SerializeField] private float speed = 4f;

    [Tooltip("次の電車が来るまでの待ち時間（最小〜最大、秒）")]
    [SerializeField] private Vector2 waitSeconds = new Vector2(4f, 12f);

    [Tooltip("左端（これより左は画面外）")]
    [SerializeField] private float minX = -12f;

    [Tooltip("右端（これより右は画面外）")]
    [SerializeField] private float maxX = 12f;

    [Tooltip("ドットの大きさ（1/PPU）。位置をこの単位にそろえる")]
    [SerializeField] private float pixelSize = 0.05f;

    private SpriteRenderer sr;
    private float x;
    private float dir;
    private float wait;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        sr.enabled = false;
        wait = Random.Range(0.5f, waitSeconds.x);
    }

    private void Update()
    {
        if (!sr.enabled)
        {
            wait -= Time.deltaTime;
            if (wait > 0f) return;
            dir = Random.value < 0.5f ? 1f : -1f;
            x = dir > 0f ? minX : maxX;
            sr.flipX = dir < 0f;
            sr.enabled = true;
        }

        x += dir * speed * Time.deltaTime;
        var p = transform.position;
        p.x = Mathf.Round(x / pixelSize) * pixelSize;
        transform.position = p;

        if ((dir > 0f && x > maxX) || (dir < 0f && x < minX))
        {
            sr.enabled = false;
            wait = Random.Range(waitSeconds.x, waitSeconds.y);
        }
    }
}
