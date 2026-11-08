using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ミルクの温度合わせ。蛇口のつまみをスライドして水／お湯を足し、時間内に人肌の温度にする。
/// 適温の範囲に keepSeconds 秒入り続けたら完成（ボタンは無い）。外れるとキープは 0 に戻る。
/// 絵は MiniGameArt のドット絵（哺乳瓶・温度計・しずく・つまみ・部屋の背景）。
/// 数値はすべて調整前のたたき台（Settings を MiniGameManager の Inspector で調整できる）。
/// </summary>
public class MilkTemperatureGame : MiniGame
{
    [Serializable]
    public class Settings
    {
        [Header("時間")]
        [Tooltip("制限時間（秒）")] public float timeLimit = 30f;

        [Header("温度（℃）")]
        [Tooltip("はじめのミルクの温度の下限（70℃以上で溶かした直後なので熱い）")] public float startTempMin = 60f;
        [Tooltip("はじめのミルクの温度の上限")] public float startTempMax = 80f;
        [Tooltip("部屋の温度。放っておくとここへ近づく")] public float roomTemp = 25f;
        [Tooltip("自然に冷める速さ（1秒あたり、室温との差に掛ける割合）")] public float naturalCoolRate = 0.03f;
        [Tooltip("つまみを水いっぱいにしたときの冷める速さ（℃/秒）")] public float waterMaxRate = 7f;
        [Tooltip("つまみをお湯いっぱいにしたときの温まる速さ（℃/秒）")] public float hotMaxRate = 4f;
        [Tooltip("この温度より熱いと湯気が出る")] public float steamTemp = 45f;

        [Header("適温（人肌）")]
        public float zoneMin = 36f;
        public float zoneMax = 40f;

        [Header("判定")]
        [Tooltip("適温にこの秒数入り続けたら完成")] public float keepSeconds = 2f;
        [Tooltip("完成したとき、残りがこの秒数以上なら大成功")] public float greatRemainingSeconds = 12f;

        [Header("結果の変化")]
        public MiniGameEffectTable effects = new MiniGameEffectTable(
            new MiniGameEffect { childStress = 4f, parentStress = 2f },
            new MiniGameEffect { childStress = -3f, childHealth = 1f },
            new MiniGameEffect { childStress = -6f, childHealth = 2f, parentStress = -1f });
    }

    private const int Scale = 6;          // ドット絵の拡大率（1ドット＝6px）。この画面の絵はすべて同じ倍率
    private const int IntroScale = 7;
    private const float TempMin = 15f;    // 温度計の下端
    private const float TempMax = 95f;    // 温度計の上端

    // 温度計の管の中（thermometer_frame 11×39 のうち x=4〜6、下から 6〜36 行）
    private static readonly Vector2 TubeMin = new Vector2(4f / 11f, 6f / 39f);
    private static readonly Vector2 TubeMax = new Vector2(7f / 11f, 37f / 39f);

    private static readonly Color Dark = new Color(0.25f, 0.2f, 0.18f);
    private static readonly Color Green = new Color(0.1f, 0.55f, 0.2f);
    private static readonly Color Red = new Color(0.8f, 0.25f, 0.15f);
    private static readonly Color Blue = new Color(0.2f, 0.4f, 0.8f);

    private readonly Settings s;

    private float temp;
    private float sliderValue;
    private float keepTimer;
    private float steamTimer;
    private int steamFrame;

    private Image steamImage;
    private TMP_Text tempLabel;
    private Image thermoFill;
    private TMP_Text feedback;
    private RectTransform keepFill;

    public MilkTemperatureGame(Settings settings, MiniGameArt art) : base(art) { s = settings; }

    public override string Title => "ミルクの温度合わせ";
    public override string Instruction => $"つまみで温度を調整して、てきおんで {s.keepSeconds:0} 秒キープ！";
    public override float TimeLimit => s.timeLimit;

    public override string[] HowToSteps => new[]
    {
        "① つまみを左へ動かすと水で冷え、右へ動かすとお湯で温まる",
        $"② 温度計の緑の矢印（{s.zoneMin:0}〜{s.zoneMax:0}℃）に合わせる",
        $"③ てきおんのまま {s.keepSeconds:0} 秒キープできたら完成。早いほど高評価",
    };

    public override string Tip =>
        "粉ミルクは菌を減らすために 70℃以上のお湯で溶かし、そのあと湯冷ましや流水で人肌（体温くらい）まで冷まします。" +
        "飲ませる前に手首の内側に数滴たらして、温かいと感じるくらいなら OK。\n（WHO・厚生労働省のガイドラインより）";

    public override MiniGameEffect EffectFor(MiniGameRank rank) => s.effects.Get(rank);

    public override string ResultMessage(MiniGameRank rank)
    {
        switch (rank)
        {
            case MiniGameRank.大成功: return "あっという間に人肌！\nごくごく飲んでくれた";
            case MiniGameRank.成功:   return "ちょうどいい温度に\nできた";
            default:                   return "温度が合わなくて\n泣いてしまった…";
        }
    }

    // ------------------------------------------------------------
    // 説明画面の絵
    // ------------------------------------------------------------

    public override void BuildIntroArt(RectTransform area)
    {
        var bottle = MiniGameUIKit.MakePixel("Bottle", area, art.milkBottle, IntroScale, new Vector2(0.32f, 0.42f));
        MiniGameUIKit.MakePixel("Steam", bottle.transform, MiniGameArt.Frame(art.steam, 0), IntroScale,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 0f));
        var fill = BuildThermometer(area, new Vector2(0.70f, 0.50f), IntroScale, false);
        if (fill != null) fill.fillAmount = Norm((s.zoneMin + s.zoneMax) * 0.5f);
    }

    // ------------------------------------------------------------
    // 遊ぶ画面
    // ------------------------------------------------------------

    protected override void Build(RectTransform area)
    {
        temp = UnityEngine.Random.Range(s.startTempMin, s.startTempMax);
        sliderValue = 0f;
        keepTimer = 0f;
        steamTimer = 0f;
        steamFrame = 0;

        // 背景：いつもの部屋（文字が読めるよう薄く重ねる）
        MiniGameUIKit.MakeBackdrop("Backdrop", area, art.roomBackground, Color.white);
        MiniGameUIKit.MakeImage("Veil", area, Vector2.zero, Vector2.one, new Color(0.99f, 0.96f, 0.88f, 0.82f));

        // 哺乳瓶と湯気
        var bottle = MiniGameUIKit.MakePixel("Bottle", area, art.milkBottle, Scale, new Vector2(0.15f, 0.50f));
        steamImage = MiniGameUIKit.MakePixel("Steam", bottle.transform, MiniGameArt.Frame(art.steam, 0), Scale,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 0f));
        tempLabel = MiniGameUIKit.MakeLabel("TempLabel", area, "", 46, Dark, new Vector2(0.02f, 0.03f), new Vector2(0.28f, 0.18f));

        // 温度計
        thermoFill = BuildThermometer(area, new Vector2(0.33f, 0.50f), Scale, true);

        // 右側：説明・つまみ・キープのメーター
        feedback = MiniGameUIKit.MakeLabel("Feedback", area, "", 26, Dark,
            new Vector2(0.52f, 0.86f), new Vector2(0.99f, 0.99f));
        MiniGameUIKit.MakeLabel("FaucetTitle", area, "蛇口のつまみをスライド", 24, Dark,
            new Vector2(0.52f, 0.76f), new Vector2(0.99f, 0.85f));

        var slider = MiniGameUIKit.MakePixelSlider("Faucet", area, new Vector2(0.76f, 0.56f), art.sliderTrack, art.faucetKnob, Scale,
            -1f, 1f, 0f, v => sliderValue = v);
        // しずくは溝の両端の上、文字は両端の下
        MiniGameUIKit.MakePixel("WaterDrop", slider.transform, art.waterDrop, Scale, new Vector2(0.06f, 1f), new Vector2(0.5f, 0f));
        MiniGameUIKit.MakePixel("HotDrop", slider.transform, art.hotDrop, Scale, new Vector2(0.94f, 1f), new Vector2(0.5f, 0f));
        MiniGameUIKit.MakeLabel("WaterLabel", slider.transform, "水", 24, Blue,
            new Vector2(-0.1f, -0.9f), new Vector2(0.22f, -0.05f));
        MiniGameUIKit.MakeLabel("HotLabel", slider.transform, "お湯", 24, Red,
            new Vector2(0.78f, -0.9f), new Vector2(1.1f, -0.05f));

        MiniGameUIKit.MakeLabel("KeepTitle", area, $"てきおんで {s.keepSeconds:0} 秒キープ", 24, Green,
            new Vector2(0.56f, 0.17f), new Vector2(0.96f, 0.26f));
        keepFill = MiniGameUIKit.MakeBar("KeepBar", area, new Vector2(0.58f, 0.08f), new Vector2(0.94f, 0.15f),
            new Color(0.85f, 0.85f, 0.88f), new Color(0.3f, 0.7f, 0.4f));

        Refresh();
    }

    /// 温度計を置き、中身（Filled の Image）を返す。
    private Image BuildThermometer(Transform parent, Vector2 anchor, int scale, bool withLabel)
    {
        var frame = MiniGameUIKit.MakePixel("Thermometer", parent, art.thermometerFrame, scale, anchor);

        var fillRt = MiniGameUIKit.MakeRect("Fill", frame.transform, TubeMin, TubeMax);
        var fill = fillRt.gameObject.AddComponent<Image>();
        fill.raycastTarget = false;
        fill.sprite = art.thermometerFill;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Vertical;
        fill.fillOrigin = (int)Image.OriginVertical.Bottom;
        if (art.thermometerFill == null) fill.color = new Color(0.85f, 0.33f, 0.31f);

        // 適温の矢印（管の左に置く）
        float zoneMid = Mathf.Lerp(TubeMin.y, TubeMax.y, Norm((s.zoneMin + s.zoneMax) * 0.5f));
        MiniGameUIKit.MakePixel("ZoneArrow", frame.transform, art.zoneArrow, scale, new Vector2(2f / 11f, zoneMid), new Vector2(1f, 0.5f));
        if (withLabel)
        {
            MiniGameUIKit.MakeLabel("ZoneLabel", frame.transform, $"てきおん\n{s.zoneMin:0}〜{s.zoneMax:0}℃", 22,
                new Color(0.15f, 0.5f, 0.2f), new Vector2(1.05f, zoneMid - 0.07f), new Vector2(3.2f, zoneMid + 0.07f),
                TextAlignmentOptions.Left);
        }
        return fill;
    }

    private static float Norm(float t) => Mathf.InverseLerp(TempMin, TempMax, t);

    private bool InZone => temp >= s.zoneMin && temp <= s.zoneMax;

    protected override void OnTick(float dt)
    {
        // つまみ：中央付近は細かく、端に行くほど強く（2乗）
        float v = sliderValue;
        float rate = Mathf.Sign(v) * v * v * (v < 0f ? s.waterMaxRate : s.hotMaxRate);
        temp += rate * dt;
        temp += (s.roomTemp - temp) * s.naturalCoolRate * dt;
        temp = Mathf.Clamp(temp, TempMin, TempMax);

        // 適温に入り続けた時間。外れたら 0 に戻す
        keepTimer = InZone ? keepTimer + dt : 0f;

        // 湯気のコマ送り
        steamTimer += dt;
        if (steamTimer >= 0.35f)
        {
            steamTimer = 0f;
            steamFrame++;
        }
        Refresh();

        if (keepTimer >= s.keepSeconds)
            Finish(Remaining >= s.greatRemainingSeconds ? MiniGameRank.大成功 : MiniGameRank.成功);
    }

    protected override void OnTimeUp()
    {
        // 時間切れ：その時点で適温なら、そのまま飲ませたことにする
        Finish(InZone ? MiniGameRank.成功 : MiniGameRank.失敗);
    }

    private void Refresh()
    {
        if (tempLabel != null) tempLabel.text = $"{temp:0}℃";
        if (thermoFill != null) thermoFill.fillAmount = Norm(temp);
        MiniGameUIKit.SetBar(keepFill, s.keepSeconds > 0f ? keepTimer / s.keepSeconds : 0f);

        if (feedback != null)
        {
            if (InZone)              { feedback.text = "ちょうどいい！ そのままキープ"; feedback.color = Green; }
            else if (temp > s.zoneMax) { feedback.text = "熱すぎる！ 水で冷まそう";     feedback.color = Red; }
            else                     { feedback.text = "ぬるい… お湯を足そう";        feedback.color = Blue; }
        }

        if (steamImage != null)
        {
            bool hot = temp > s.steamTemp;
            MiniGameUIKit.SetPixelSprite(steamImage, hot ? MiniGameArt.Frame(art.steam, steamFrame) : null, Scale);
        }
    }
}
