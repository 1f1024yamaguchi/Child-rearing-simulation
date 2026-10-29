using UnityEngine;

/// 雲をゆっくり横に流す。端まで行ったら反対側から出てくる。
public class CloudDrift : MonoBehaviour
{
    [Tooltip("流れる速さ（ユニット/秒）。正で右へ")]
    [SerializeField] private float speed = 0.15f;

    [Tooltip("この x より右に出たら左端へ戻す")]
    [SerializeField] private float maxX = 10f;

    [Tooltip("戻す先の x")]
    [SerializeField] private float minX = -10f;

    [Tooltip("ドットの大きさ（1/PPU）。位置をこの単位にそろえてにじみを防ぐ")]
    [SerializeField] private float pixelSize = 0.05f;

    private float x;

    private void Start()
    {
        x = transform.position.x;
    }

    private void Update()
    {
        x += speed * Time.deltaTime;
        if (x > maxX) x = minX;
        if (x < minX) x = maxX;
        var p = transform.position;
        p.x = Mathf.Round(x / pixelSize) * pixelSize;
        transform.position = p;
    }
}
