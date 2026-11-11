using UnityEngine;
using UnityEngine.UI;

/// UI の Image に、絵を順番に入れ替えてパラパラ漫画のように動かす。
/// 本編のキャラ・木の絵（SpriteRenderer 用の Animator）を UI でも動かすためのもの。
/// 時間が止まっていても動くよう、unscaledDeltaTime で進める。
[RequireComponent(typeof(Image))]
public class UISpriteFlipbook : MonoBehaviour
{
    [Tooltip("順番に出す絵")]
    [SerializeField] private Sprite[] frames;

    [Tooltip("1秒に何枚めくるか")]
    [SerializeField] private float framesPerSecond = 4f;

    private Image image;
    private float timer;
    private int index;

    private void Awake()
    {
        image = GetComponent<Image>();
    }

    /// 出す絵を差し替える（歩き⇔立ち など）
    public void SetFrames(Sprite[] newFrames)
    {
        if (newFrames == frames) return;
        frames = newFrames;
        index = 0;
        timer = 0f;
        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    private void Update()
    {
        if (frames == null || frames.Length <= 1 || framesPerSecond <= 0f) return;
        timer += Time.unscaledDeltaTime;
        float step = 1f / framesPerSecond;
        while (timer >= step)
        {
            timer -= step;
            index = (index + 1) % frames.Length;
            Apply();
        }
    }

    private void Apply()
    {
        if (image == null) image = GetComponent<Image>();
        if (frames == null || frames.Length == 0 || frames[index] == null) return;
        image.sprite = frames[index];
    }
}
