using System.Collections.Generic;
using UnityEngine;

/// 雪・花びら・落ち葉のように、小さな粒を風に流しながら降らせる。
/// 家より奥（並び順が家より下）に描くので、家の部分では見えず、空と左右の景色にだけ降る。
public class FallingPixels : MonoBehaviour
{
    [Tooltip("降らせる粒の絵（複数入れるとランダムに混ざる）")]
    [SerializeField] private Sprite[] sprites;

    [Tooltip("同時に降っている粒の数")]
    [SerializeField] private int count = 40;

    [Header("降る範囲（ワールド座標）")]
    [SerializeField] private float minX = -9.5f;
    [SerializeField] private float maxX = 9.5f;
    [Tooltip("この高さから降り始める")]
    [SerializeField] private float topY = 5.5f;
    [Tooltip("この高さまで落ちたら上に戻す")]
    [SerializeField] private float bottomY = -2.6f;

    [Header("動き")]
    [Tooltip("落ちる速さ（最小〜最大、ユニット/秒）")]
    [SerializeField] private Vector2 fallSpeed = new Vector2(0.4f, 0.8f);
    [Tooltip("ふらふら横に揺れる幅")]
    [SerializeField] private float wobble = 0.3f;
    [Tooltip("風に流される強さ（WindSway と同じ風を使う）")]
    [SerializeField] private float windPush = 0.6f;

    [Header("表示")]
    [SerializeField] private int sortingOrder = -35;
    [Tooltip("ドットの大きさ（1/PPU）。位置をこの単位にそろえる")]
    [SerializeField] private float pixelSize = 0.05f;

    private class Flake
    {
        public Transform tr;
        public Vector2 pos;
        public float speed, phase, wobbleSpeed;
    }

    private readonly List<Flake> flakes = new List<Flake>();

    private void Start()
    {
        if (sprites == null || sprites.Length == 0) return;
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("p" + i);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprites[Random.Range(0, sprites.Length)];
            sr.sortingOrder = sortingOrder;
            var f = new Flake { tr = go.transform };
            Respawn(f, Random.Range(bottomY, topY));   // 最初は画面全体にばらまく
            flakes.Add(f);
        }
    }

    private void Respawn(Flake f, float y)
    {
        f.pos = new Vector2(Random.Range(minX, maxX), y);
        f.speed = Random.Range(fallSpeed.x, fallSpeed.y);
        f.phase = Random.Range(0f, Mathf.PI * 2f);
        f.wobbleSpeed = Random.Range(1.2f, 2.4f);
    }

    private void Update()
    {
        float t = Time.time;
        float dt = Time.deltaTime;
        foreach (var f in flakes)
        {
            float wind = WindSway.Gust(t, f.pos.x) * windPush;
            f.pos.y -= f.speed * dt;
            f.pos.x += (wind + Mathf.Cos(t * f.wobbleSpeed + f.phase) * wobble) * dt;

            if (f.pos.y < bottomY) Respawn(f, topY);
            if (f.pos.x > maxX) f.pos.x = minX;
            if (f.pos.x < minX) f.pos.x = maxX;

            f.tr.position = new Vector3(Mathf.Round(f.pos.x / pixelSize) * pixelSize,
                                        Mathf.Round(f.pos.y / pixelSize) * pixelSize, 0f);
        }
    }
}
