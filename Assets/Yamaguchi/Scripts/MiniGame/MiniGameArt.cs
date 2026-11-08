using UnityEngine;

/// <summary>
/// ミニゲームで使うドット絵の置き場所（差し替え枠）。
/// 実体は Assets/Yamaguchi/Resources/MiniGameArt.asset。
/// 小物の絵はメニュー「子育て → ミニゲーム → ドット絵を作り直す」で書き出したもの（家族キャラと同じマス目方式）。
/// 描き直した絵に差し替えるときは、この asset の欄に入れ替えるだけでよい。
/// すべて PPU=20 / Filter Mode = Point のドット絵前提（UI では整数倍に拡大して表示する）。
/// </summary>
[CreateAssetMenu(menuName = "子育て/ミニゲームの絵", fileName = "MiniGameArt")]
public class MiniGameArt : ScriptableObject
{
    [Header("共通")]
    [Tooltip("背景にする部屋の絵（既存の room_bg）")] public Sprite roomBackground;
    [Tooltip("吹き出し：泣いている")] public Sprite bubbleCry;
    [Tooltip("吹き出し：ごきげん（♪）")] public Sprite bubbleNote;
    [Tooltip("吹き出し：寝ている（Zz）")] public Sprite bubbleZzz;

    [Header("ミルクの温度合わせ")]
    public Sprite milkBottle;
    [Tooltip("湯気（コマ送り）")] public Sprite[] steam;
    [Tooltip("温度計の枠。管の中は透明にしておく")] public Sprite thermometerFrame;
    [Tooltip("温度計の中身（下から伸びる）")] public Sprite thermometerFill;
    [Tooltip("適温を指す矢印")] public Sprite zoneArrow;
    public Sprite waterDrop;
    public Sprite hotDrop;
    [Tooltip("スライダーのつまみ（蛇口のハンドル）")] public Sprite faucetKnob;
    [Tooltip("スライダーの溝（左＝水、右＝お湯）")] public Sprite sliderTrack;

    [Header("ミルクを飲ませる")]
    [Tooltip("横向きの哺乳瓶（乳首が左）")] public Sprite milkBottleSide;
    [Tooltip("顔のアップ：はらぺこ（泣き）")] public Sprite faceHungry;
    [Tooltip("顔のアップ：ごくごく")] public Sprite faceDrinking;
    [Tooltip("顔のアップ：にこにこ（ちょうどいい）")] public Sprite faceHappy;
    [Tooltip("顔のアップ：くるしい（飲みすぎ）")] public Sprite faceFull;
    [Tooltip("顔のアップ：けぽっ（吐く）")] public Sprite faceVomit;
    [Tooltip("顔のアップ：むせた")] public Sprite faceChoke;

    [Header("寝かしつけ")]
    [Tooltip("起きている赤ちゃん（既存の child_baby_idle）")] public Sprite[] babyAwake;
    [Tooltip("寝ている赤ちゃん（既存の child_baby_sleep）")] public Sprite[] babyAsleep;
    [Tooltip("起きている赤ちゃんの絵の中で、実際に描かれている範囲（ドット・左下原点）。メニューで自動計算")]
    public RectInt babyAwakeBounds = new RectInt(1, 0, 10, 13);
    [Tooltip("寝ている赤ちゃんの絵の中で、実際に描かれている範囲（ドット・左下原点）。メニューで自動計算")]
    public RectInt babyAsleepBounds = new RectInt(0, 0, 23, 20);
    public Sprite crib;
    [Tooltip("止まっている的の輪")] public Sprite ringTarget;
    [Tooltip("縮んでくる輪")] public Sprite ringApproach;

    private static MiniGameArt cached;

    /// Resources/MiniGameArt を読む。無ければ空のものを返す（絵は出ないが動く）。
    public static MiniGameArt Load()
    {
        if (cached != null) return cached;
        cached = Resources.Load<MiniGameArt>("MiniGameArt");
        if (cached == null)
        {
            Debug.LogWarning("[MiniGameArt] Resources/MiniGameArt が見つかりません。メニュー「子育て → ミニゲーム → ドット絵を作り直す」を実行してください。");
            cached = CreateInstance<MiniGameArt>();
        }
        return cached;
    }

    /// コマ送り用。配列が空なら null。
    public static Sprite Frame(Sprite[] frames, int index)
    {
        if (frames == null || frames.Length == 0) return null;
        int i = index % frames.Length;
        if (i < 0) i += frames.Length;
        return frames[i];
    }
}
