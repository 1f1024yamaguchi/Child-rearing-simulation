using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 寝かしつけ。縮んでくる輪が赤ちゃんのまわりの輪に重なる瞬間にタップして、リズムよくトントンする。
/// リズムが合うと泣き声が下がり、落ち着いている間「ねんね」がたまる。
/// 画面のどこをタップしてもよい（タブレット想定）。
/// 絵は MiniGameArt のドット絵（既存の赤ちゃん・部屋、ベビーベッド・輪・吹き出し）。
/// 赤ちゃんの絵はコマの左下に寄せて描かれているので、MiniGameArt の描画範囲（Bounds）で中央に合わせる。
/// 数値はすべて調整前のたたき台（Settings を MiniGameManager の Inspector で調整できる）。
/// </summary>
public class SleepRhythmGame : MiniGame
{
    [Serializable]
    public class Settings
    {
        [Header("時間")]
        [Tooltip("制限時間（秒）")] public float timeLimit = 30f;

        [Header("リズム")]
        [Tooltip("1分あたりの拍数")] public float bpm = 70f;
        [Tooltip("この秒数以内のズレなら「ぴったり」")] public float perfectWindow = 0.10f;
        [Tooltip("この秒数以内のズレなら「いいね」。これより外は「ずれた」")] public float goodWindow = 0.22f;

        [Header("泣き声（0〜100）")]
        public float startCry = 70f;
        [Tooltip("何もしなくても泣き声が増える速さ（1秒あたり）")] public float cryRisePerSec = 2.5f;
        [Tooltip("ぴったりのときに下がる量")] public float perfectCryDown = 10f;
        [Tooltip("いいねのときに下がる量")] public float goodCryDown = 6f;
        [Tooltip("ずれた・連打したときに上がる量")] public float missCryUp = 6f;
        [Tooltip("泣き声がこの値より大きいと「泣いている」吹き出しを出す")] public float cryBubbleThreshold = 55f;

        [Header("ねんね（0〜100）")]
        [Tooltip("泣き声がこの値以下のとき、ねんねがたまる")] public float calmThreshold = 35f;
        [Tooltip("泣き声がこの値以上のとき、ねんねが減る")] public float wakeThreshold = 60f;
        [Tooltip("落ち着いているときにたまる速さ（1秒あたり）")] public float sleepGainPerSec = 8f;
        [Tooltip("ぐずっているときに減る速さ（1秒あたり）")] public float sleepLossPerSec = 3f;

        [Header("判定")]
        [Tooltip("ねんねが満タンになったとき、残りがこの秒数以上なら大成功")] public float greatRemainingSeconds = 8f;

        [Header("結果の変化")]
        public MiniGameEffectTable effects = new MiniGameEffectTable(
            new MiniGameEffect { childStress = 4f, childHealth = -1f, parentStress = 3f },
            new MiniGameEffect { childStress = -3f, parentStress = -1f },
            new MiniGameEffect { childStress = -5f, childHealth = 1f, parentStress = -3f });
    }

    private const int Scale = 7;                  // ドット絵の拡大率（1ドット＝7px）
    private const int IntroScale = 6;
    private const float ApproachMaxScale = 1.75f;
    private const float MattressTop = 8f / 20f;   // crib（34×20）のマットレス上端（下から）

    private static readonly Color Night = new Color(0.4f, 0.44f, 0.68f);   // 夜らしく暗め。白い文字を目立たせる

    private readonly Settings s;

    private float elapsed;
    private float cry;
    private float sleep;
    private int lastScoredBeat;
    private float judgeAlpha;
    private float bobTimer;

    private Image babyImage;
    private Image bubbleImage;
    private RectTransform approachRing;
    private TMP_Text judgeLabel;
    private RectTransform cryFill;
    private RectTransform sleepFill;

    public SleepRhythmGame(Settings settings, MiniGameArt art) : base(art) { s = settings; }

    private float Interval => 60f / Mathf.Max(1f, s.bpm);
    private bool Asleep => sleep >= 100f;

    public override string Title => "寝かしつけ";
    public override string Instruction => "輪が重なる瞬間にタップ！ 泣き声を下げて寝かせよう";
    public override float TimeLimit => s.timeLimit;

    public override string[] HowToSteps => new[]
    {
        "① 黄色い輪が水色の輪に重なった瞬間に、画面をタップ（トントン）",
        "② リズムが合うと「なきごえ」が下がる。ずれ・連打は逆効果",
        "③ 泣きやんでいる間「ねんね」がたまる。満タンで成功",
    };

    public override string Tip =>
        "1歳になるまでは、寝かせるときは仰向けにします（うつぶせ寝は乳幼児突然死症候群のリスクを高めます）。" +
        "顔の近くには、口や鼻をふさぐような物を置かないようにしましょう。\n（こども家庭庁・消費者庁より）";

    public override MiniGameEffect EffectFor(MiniGameRank rank) => s.effects.Get(rank);

    public override string ResultMessage(MiniGameRank rank)
    {
        switch (rank)
        {
            case MiniGameRank.大成功: return "あっという間に\nぐっすり眠った";
            case MiniGameRank.成功:   return "寝てくれた。おやすみ";
            default:                   return "なかなか寝てくれず\n夜泣きになってしまった…";
        }
    }

    // ------------------------------------------------------------
    // 配置の共通部分
    // ------------------------------------------------------------

    /// ベビーベッド・赤ちゃん・輪・吹き出しを置く。戻り値は（赤ちゃん, 縮む輪, 吹き出し）。
    private (Image baby, RectTransform approach, Image bubble) BuildScene(Transform parent, Vector2 anchor, int scale)
    {
        var crib = MiniGameUIKit.MakePixel("Crib", parent, art.crib, scale, anchor);
        Vector2 cribSize = art.crib != null ? art.crib.rect.size * scale : new Vector2(34f, 20f) * scale;

        // 赤ちゃん（ベッドのマットレスの上・左右中央）
        var baby = MiniGameUIKit.MakePixel("Baby", crib.transform, MiniGameArt.Frame(art.babyAwake, 0), scale,
            new Vector2(0.5f, MattressTop), Vector2.zero);
        PlaceBaby(baby, false, scale, 0);

        // 輪（起きている赤ちゃんの中心に重ねる）
        var b = art.babyAwakeBounds;
        var ringPos = new Vector2(0f, -cribSize.y * 0.5f + cribSize.y * MattressTop + b.height * scale * 0.5f);
        var target = MiniGameUIKit.MakePixel("TargetRing", crib.transform, art.ringTarget, scale, new Vector2(0.5f, 0.5f));
        target.rectTransform.anchoredPosition = ringPos;
        var approach = MiniGameUIKit.MakePixel("ApproachRing", crib.transform, art.ringApproach, scale, new Vector2(0.5f, 0.5f));
        approach.rectTransform.anchoredPosition = ringPos;

        // 吹き出し（輪の右上）
        float ringR = art.ringTarget != null ? art.ringTarget.rect.width * scale * 0.5f : 11f * scale;
        var bubble = MiniGameUIKit.MakePixel("Bubble", crib.transform, art.bubbleCry, scale, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f));
        bubble.rectTransform.anchoredPosition = ringPos + new Vector2(ringR * 0.55f, ringR * 0.55f);

        return (baby, approach.rectTransform, bubble);
    }

    /// 赤ちゃんの絵を差し替え、描かれている部分がマットレス中央に来るように置く。
    private void PlaceBaby(Image baby, bool asleep, int scale, int frame)
    {
        if (baby == null) return;
        var sprite = asleep ? MiniGameArt.Frame(art.babyAsleep, frame) : MiniGameArt.Frame(art.babyAwake, frame);
        var b = asleep ? art.babyAsleepBounds : art.babyAwakeBounds;
        MiniGameUIKit.SetPixelSprite(baby, sprite, scale);
        float x = -Mathf.Floor(b.x + b.width * 0.5f) * scale;
        float y = -b.y * scale + (bobTimer > 0f && !asleep ? scale : 0f);
        baby.rectTransform.anchoredPosition = new Vector2(x, y);
    }

    // ------------------------------------------------------------
    // 説明画面の絵
    // ------------------------------------------------------------

    public override void BuildIntroArt(RectTransform area)
    {
        MiniGameUIKit.MakeBackdrop("Backdrop", area, art.roomBackground, Night);
        var scene = BuildScene(area, new Vector2(0.5f, 0.32f), IntroScale);
        scene.approach.localScale = new Vector3(1.375f, 1.375f, 1f);
    }

    // ------------------------------------------------------------
    // 遊ぶ画面
    // ------------------------------------------------------------

    protected override void Build(RectTransform area)
    {
        elapsed = 0f;
        cry = s.startCry;
        sleep = 0f;
        lastScoredBeat = -1;
        judgeAlpha = 0f;
        bobTimer = 0f;

        // 背景：夜の部屋
        MiniGameUIKit.MakeBackdrop("Backdrop", area, art.roomBackground, Night);

        // 画面全体がタップ面（透明）
        var tap = MiniGameUIKit.MakeImage("TapArea", area, Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, 0.01f), true);
        tap.gameObject.AddComponent<PointerDownRelay>().onDown = OnTap;

        var scene = BuildScene(area, new Vector2(0.5f, 0.34f), Scale);
        babyImage = scene.baby;
        approachRing = scene.approach;
        bubbleImage = scene.bubble;

        // メーター（HUD と同じ平らなバー）
        MiniGameUIKit.MakeLabel("CryTitle", area, "なきごえ", 26, new Color(1f, 0.88f, 0.88f),
            new Vector2(0.04f, 0.92f), new Vector2(0.46f, 1f), TextAlignmentOptions.Left);
        cryFill = MiniGameUIKit.MakeBar("CryBar", area, new Vector2(0.04f, 0.86f), new Vector2(0.46f, 0.92f),
            new Color(0.2f, 0.2f, 0.3f, 0.8f), new Color(0.95f, 0.35f, 0.3f));
        MiniGameUIKit.MakeLabel("SleepTitle", area, "ねんね", 26, new Color(0.88f, 0.92f, 1f),
            new Vector2(0.54f, 0.92f), new Vector2(0.96f, 1f), TextAlignmentOptions.Left);
        sleepFill = MiniGameUIKit.MakeBar("SleepBar", area, new Vector2(0.54f, 0.86f), new Vector2(0.96f, 0.92f),
            new Color(0.2f, 0.2f, 0.3f, 0.8f), new Color(0.45f, 0.6f, 1f));

        judgeLabel = MiniGameUIKit.MakeLabel("Judge", area, "", 44, Color.white, new Vector2(0.2f, 0.0f), new Vector2(0.8f, 0.1f));

        Refresh();
    }

    private void OnTap()
    {
        if (IsFinished) return;

        float interval = Interval;
        int beat = Mathf.RoundToInt(elapsed / interval);
        float diff = Mathf.Abs(elapsed - beat * interval);

        // 同じ拍を2回叩いた（連打）はずれ扱い
        bool alreadyScored = beat == lastScoredBeat;
        if (!alreadyScored && diff <= s.perfectWindow)
        {
            lastScoredBeat = beat;
            cry -= s.perfectCryDown;
            bobTimer = 0.15f;
            ShowJudge("ぴったり！", new Color(1f, 0.8f, 0.2f));
        }
        else if (!alreadyScored && diff <= s.goodWindow)
        {
            lastScoredBeat = beat;
            cry -= s.goodCryDown;
            bobTimer = 0.15f;
            ShowJudge("いいね", new Color(0.55f, 0.95f, 0.6f));
        }
        else
        {
            cry += s.missCryUp;
            ShowJudge("ずれた…", new Color(0.8f, 0.8f, 0.85f));
        }
        cry = Mathf.Clamp(cry, 0f, 100f);
        Refresh();
    }

    protected override void OnTick(float dt)
    {
        elapsed += dt;

        cry = Mathf.Clamp(cry + s.cryRisePerSec * dt, 0f, 100f);
        if (cry <= s.calmThreshold)       sleep += s.sleepGainPerSec * dt;
        else if (cry >= s.wakeThreshold)  sleep -= s.sleepLossPerSec * dt;
        sleep = Mathf.Clamp(sleep, 0f, 100f);

        if (Asleep)
        {
            if (approachRing != null) approachRing.gameObject.SetActive(false);
            Refresh();
            Finish(Remaining >= s.greatRemainingSeconds ? MiniGameRank.大成功 : MiniGameRank.成功);
            return;
        }

        // 輪：拍の瞬間に的の輪（1倍）に重なる。ドット感を保つため 1/16 刻みで縮める
        float phase = elapsed / Interval;
        phase -= Mathf.Floor(phase);
        if (approachRing != null)
        {
            float sc = Mathf.Lerp(ApproachMaxScale, 1f, phase);
            sc = Mathf.Round(sc * 16f) / 16f;
            approachRing.localScale = new Vector3(sc, sc, 1f);
        }

        if (bobTimer > 0f) bobTimer -= dt;

        // 判定の文字をだんだん消す
        if (judgeAlpha > 0f && judgeLabel != null)
        {
            judgeAlpha = Mathf.Max(0f, judgeAlpha - dt * 1.6f);
            var c = judgeLabel.color;
            c.a = judgeAlpha;
            judgeLabel.color = c;
        }
        Refresh();
    }

    protected override void OnTimeUp()
    {
        Finish(MiniGameRank.失敗);
    }

    private void ShowJudge(string text, Color color)
    {
        if (judgeLabel == null) return;
        judgeLabel.text = text;
        judgeAlpha = 1f;
        color.a = 1f;
        judgeLabel.color = color;
    }

    private void Refresh()
    {
        MiniGameUIKit.SetBar(cryFill, cry / 100f);
        MiniGameUIKit.SetBar(sleepFill, sleep / 100f);

        // 起きている間は拍ごとに2コマを切り替え、寝たら寝姿のコマ
        int frame = Asleep ? Mathf.FloorToInt(elapsed / 0.6f) : Mathf.FloorToInt(elapsed / Interval);
        PlaceBaby(babyImage, Asleep, Scale, frame);

        if (bubbleImage != null)
        {
            Sprite b = null;
            if (Asleep)                             b = null;              // 寝姿の絵に Zz が描かれている
            else if (cry > s.cryBubbleThreshold)    b = art.bubbleCry;
            else if (cry <= s.calmThreshold)        b = art.bubbleZzz;     // うとうと
            MiniGameUIKit.SetPixelSprite(bubbleImage, b, Scale);
        }
    }
}
