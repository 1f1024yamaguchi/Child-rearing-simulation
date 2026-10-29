using UnityEngine;

/// 木や花を風で揺らす。
/// 絵は「上ほど左右にずらした5コマ」（左いっぱい／左／まっすぐ／右／右いっぱい）を用意しておき、
/// 風の強さに合わせてコマを選ぶ。回転させないのでドットが崩れない。
[RequireComponent(typeof(SpriteRenderer))]
public class WindSway : MonoBehaviour
{
    [Tooltip("5コマ：左いっぱい／左／まっすぐ／右／右いっぱい")]
    [SerializeField] private Sprite[] frames = new Sprite[5];

    [Tooltip("ゆらゆらする速さ")]
    [SerializeField] private float swaySpeed = 1.6f;

    [Tooltip("ゆらゆらの大きさ（0〜1）")]
    [Range(0f, 1f)][SerializeField] private float swayAmount = 0.5f;

    [Tooltip("突風の強さ（0〜1）。場所ごとに少し遅れて届くので、波のように揺れる")]
    [Range(0f, 1f)][SerializeField] private float gustAmount = 0.6f;

    private SpriteRenderer sr;
    private float phase;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        phase = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0) return;

        float t = Time.time;
        float sway = Mathf.Sin(t * swaySpeed + phase) * swayAmount;
        float gust = Gust(t, transform.position.x) * gustAmount;
        float v = Mathf.Clamp(sway + gust, -1f, 1f);       // -1=左いっぱい / 1=右いっぱい

        int center = frames.Length / 2;
        int index = Mathf.Clamp(center + Mathf.RoundToInt(v * center), 0, frames.Length - 1);
        if (sr.sprite != frames[index]) sr.sprite = frames[index];
    }

    /// 全員が同じ風を受ける（x が大きいほど少し遅れて届く）。おおむね右向きの風
    public static float Gust(float time, float x)
    {
        float n = Mathf.PerlinNoise(time * 0.35f - x * 0.08f, 0.37f);
        return Mathf.Clamp((n - 0.35f) * 1.8f, -0.4f, 1f);
    }
}
