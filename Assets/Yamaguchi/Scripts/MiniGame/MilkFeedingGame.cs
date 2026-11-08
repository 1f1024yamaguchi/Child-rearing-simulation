using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ミルクを飲ませる。画面を長押ししている間だけ飲み、指を離すと休けい。
/// お腹の満たされ具合はゲージで見せず、赤ちゃんの顔だけで判断する（顔をうかがう）。
///   はらぺこ（泣き）→ ごくごく → にこにこ（ちょうどいい）→ くるしい（飲みすぎ）→ けぽっ（吐く）
/// 飲んだ分は少し遅れてお腹に届くので、にこにこを見てから離すと少し行きすぎる（先読みが必要）。
/// 飲みたい量は毎回ちがう。休まず続けて飲ませるとむせる。
/// にこにこ（またはくるしい）の顔で指を離して finishRestSeconds 秒休ませると完了（ボタンは無い）。
/// 数値はすべて調整前のたたき台（Settings を MiniGameManager の Inspector で調整できる）。
/// </summary>
public class MilkFeedingGame : MiniGame
{
    [Serializable]
    public class Settings
    {
        [Header("時間")]
        [Tooltip("制限時間（秒）")] public float timeLimit = 30f;

        [Header("飲みたい量（0〜100、毎回この間でランダム）")]
        public float targetMin = 55f;
        public float targetMax = 80f;
        [Tooltip("ちょうどいい（にこにこ）の幅：目標より何だけ下から")] public float happyBelow = 8f;
        [Tooltip("ちょうどいい（にこにこ）の幅：目標より何だけ上まで")] public float happyAbove = 7f;
        [Tooltip("くるしい（飲みすぎ）の幅。これを超えると吐く")] public float fullRange = 12f;

        [Header("飲み方")]
        [Tooltip("長押ししている間に口に入る量（1秒あたり）")] public float drinkRate = 14f;
        [Tooltip("口に入った分がお腹に届くまでの遅れ（秒）。大きいほど先読みが必要")] public float swallowLag = 0.6f;
        [Tooltip("この秒数以上続けて飲ませるとむせる")] public float chokeAfter = 3f;
        [Tooltip("むせている間は飲めない（秒）")] public float chokeDuration = 1.2f;
        [Tooltip("むせたときにこぼれる量")] public float chokeSpill = 4f;

        [Header("判定")]
        [Tooltip("にこにこの顔で指を離して、この秒数休ませたら完了")] public float finishRestSeconds = 2f;
        [Tooltip("一度もむせずに完了し、残りがこの秒数以上なら大成功")] public float greatRemainingSeconds = 8f;

        [Header("結果の変化")]
        public MiniGameEffectTable effects = new MiniGameEffectTable(
            new MiniGameEffect { childStress = 4f, childHealth = -2f, parentStress = 2f },
            new MiniGameEffect { childStress = -3f, childHealth = 1f },
            new MiniGameEffect { childStress = -6f, childHealth = 2f, parentStress = -1f });
    }

    private enum Mood { Hungry, Drinking, Happy, Full, Vomit, Choke }
    private enum Ending { None, Good, Overfull, Vomit, Hungry }

    private const int Scale = 9;           // 顔のアップの拡大率（1ドット＝9px）
    private const int IntroScale = 6;
    // 顔（24×22）の口の位置（左下原点の割合）
    private const int BottleDrop = 2;      // 哺乳瓶を口の中心より何ドット下げるか（右目を隠さないため）
    private static readonly Vector2 MouthAnchor = new Vector2(11.5f / 24f, 5.5f / 22f);

    private static readonly Color Dark = new Color(0.25f, 0.2f, 0.18f);
    private static readonly Color Green = new Color(0.1f, 0.55f, 0.2f);
    private static readonly Color Orange = new Color(0.85f, 0.45f, 0.15f);

    private readonly Settings s;

    private float target;
    private float fullness;
    private float mouth;
    private float holdTime;
    private float chokeTimer;
    private int chokes;
    private float restTimer;
    private float drinkAnim;
    private Ending ending;

    private HoldRelay hold;
    private Image faceImage;
    private RectTransform bottleRect;
    private TMP_Text gulpLabel;
    private TMP_Text hintLabel;
    private GameObject restGroup;
    private RectTransform restFill;

    public MilkFeedingGame(Settings settings, MiniGameArt art) : base(art) { s = settings; }

    public override string Title => "ミルクを飲ませる";
    public override string Instruction => "長押しで飲ませる。にこにこしたら指をはなそう！";
    public override float TimeLimit => s.timeLimit;

    public override string[] HowToSteps => new[]
    {
        "① 画面を長押しするとミルクを飲む。指をはなすと休けい",
        $"② 顔を見て、にこにこしたら指をはなす。{s.finishRestSeconds:0} 秒休ませたら完了",
        $"③ {s.chokeAfter:0} 秒以上続けて飲ませるとむせる。飲ませすぎると吐いちゃう",
    };

    public override string Tip =>
        "飲む量は赤ちゃんや日によってちがいます。満腹に近づくと吸う合間の休みが長くなり、満腹になると哺乳瓶から顔をそむけます。" +
        "飲ませすぎると吐き戻すこともあります。\n（ベビー用品メーカー・小児向け健康情報より）";

    public override MiniGameEffect EffectFor(MiniGameRank rank) => s.effects.Get(rank);

    public override string ResultMessage(MiniGameRank rank)
    {
        switch (ending)
        {
            case Ending.Good:     return chokes == 0 ? "ちょうどいい量！\nまんぞくそうな顔" : "おなかいっぱい。\nちょっとむせちゃったね";
            case Ending.Overfull: return "ちょっと飲ませすぎ…\nくるしそう";
            case Ending.Vomit:    return "飲ませすぎて\n吐いてしまった…";
            default:              return "まだおなかがすいて\n泣いている…";
        }
    }

    // ------------------------------------------------------------
    // 説明画面の絵
    // ------------------------------------------------------------

    public override void BuildIntroArt(RectTransform area)
    {
        MiniGameUIKit.MakeBackdrop("Backdrop", area, art.roomBackground, Color.white);
        MiniGameUIKit.MakeImage("Veil", area, Vector2.zero, Vector2.one, new Color(0.99f, 0.96f, 0.88f, 0.8f));

        var face = MiniGameUIKit.MakePixel("Face", area, art.faceDrinking, IntroScale, new Vector2(0.42f, 0.66f));
        var bottle = MiniGameUIKit.MakePixel("Bottle", face.transform, art.milkBottleSide, IntroScale, MouthAnchor, new Vector2(0f, 0.5f));
        bottle.rectTransform.anchoredPosition = new Vector2(-IntroScale, -BottleDrop * IntroScale);

        // 顔の変わり方の見本（小さく3つ）
        AddSample(area, art.faceHungry, 0.18f, "はらぺこ", Dark);
        AddSample(area, art.faceHappy, 0.50f, "にこにこ→はなす", Green);
        AddSample(area, art.faceFull, 0.82f, "のみすぎ", Orange);
    }

    private static void AddSample(RectTransform area, Sprite sprite, float x, string label, Color color)
    {
        MiniGameUIKit.MakePixel("Sample", area, sprite, 3, new Vector2(x, 0.24f));
        MiniGameUIKit.MakeLabel("SampleLabel", area, label, 16, color, new Vector2(x - 0.17f, 0.02f), new Vector2(x + 0.17f, 0.10f));
    }

    // ------------------------------------------------------------
    // 遊ぶ画面
    // ------------------------------------------------------------

    protected override void Build(RectTransform area)
    {
        target = UnityEngine.Random.Range(s.targetMin, s.targetMax);
        fullness = 0f;
        mouth = 0f;
        holdTime = 0f;
        chokeTimer = 0f;
        chokes = 0;
        restTimer = 0f;
        drinkAnim = 0f;
        ending = Ending.None;

        MiniGameUIKit.MakeBackdrop("Backdrop", area, art.roomBackground, Color.white);
        MiniGameUIKit.MakeImage("Veil", area, Vector2.zero, Vector2.one, new Color(0.99f, 0.96f, 0.88f, 0.8f));

        // 画面全体が長押しの面（透明）
        var holdArea = MiniGameUIKit.MakeImage("HoldArea", area, Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, 0.01f), true);
        hold = holdArea.gameObject.AddComponent<HoldRelay>();

        // 顔のアップと哺乳瓶
        faceImage = MiniGameUIKit.MakePixel("Face", area, art.faceHungry, Scale, new Vector2(0.36f, 0.52f));
        var bottle = MiniGameUIKit.MakePixel("Bottle", faceImage.transform, art.milkBottleSide, Scale, MouthAnchor, new Vector2(0f, 0.5f));
        bottleRect = bottle.rectTransform;
        gulpLabel = MiniGameUIKit.MakeLabel("Gulp", area, "", 30, Orange, new Vector2(0.10f, 0.86f), new Vector2(0.62f, 0.98f));

        // 右側：説明と「ごちそうさま」までの休けいメーター
        hintLabel = MiniGameUIKit.MakeLabel("Hint", area, "", 26, Dark,
            new Vector2(0.64f, 0.50f), new Vector2(0.98f, 0.80f));
        var rest = MiniGameUIKit.MakeRect("Rest", area, new Vector2(0.64f, 0.18f), new Vector2(0.98f, 0.40f));
        restGroup = rest.gameObject;
        MiniGameUIKit.MakeLabel("RestTitle", rest, "ごちそうさま…", 26, Green, new Vector2(0f, 0.5f), new Vector2(1f, 1f));
        restFill = MiniGameUIKit.MakeBar("RestBar", rest, new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.4f),
            new Color(0.85f, 0.85f, 0.88f), new Color(0.3f, 0.7f, 0.4f));

        Refresh();
    }

    private Mood CurrentMood()
    {
        if (ending == Ending.Vomit) return Mood.Vomit;
        if (chokeTimer > 0f) return Mood.Choke;
        if (fullness > target + s.happyAbove) return Mood.Full;
        if (fullness >= target - s.happyBelow) return Mood.Happy;
        return Holding ? Mood.Drinking : Mood.Hungry;
    }

    private bool Holding => hold != null && hold.IsHeld && chokeTimer <= 0f && ending == Ending.None;

    protected override void OnTick(float dt)
    {
        if (chokeTimer > 0f) chokeTimer -= dt;

        if (Holding)
        {
            holdTime += dt;
            mouth += s.drinkRate * dt;
            restTimer = 0f;
            drinkAnim += dt;
            if (holdTime >= s.chokeAfter)
            {
                // 休まず飲ませ続けてむせた
                chokes++;
                chokeTimer = s.chokeDuration;
                holdTime = 0f;
                mouth = 0f;
                fullness = Mathf.Max(0f, fullness - s.chokeSpill);
            }
        }
        else
        {
            holdTime = 0f;
        }

        // 口に入った分が少し遅れてお腹に届く
        float k = s.swallowLag > 0f ? 1f - Mathf.Exp(-dt / s.swallowLag) : 1f;
        float moved = mouth * k;
        mouth -= moved;
        fullness += moved;

        if (fullness > target + s.happyAbove + s.fullRange)
        {
            ending = Ending.Vomit;
            Refresh();
            Finish(MiniGameRank.失敗);
            return;
        }

        // にこにこ（またはくるしい）の顔で休ませ続けたら完了
        var mood = CurrentMood();
        bool resting = !Holding && chokeTimer <= 0f && mouth < 0.5f;
        if (resting && (mood == Mood.Happy || mood == Mood.Full)) restTimer += dt;
        else restTimer = 0f;

        Refresh();

        if (restTimer >= s.finishRestSeconds)
        {
            if (mood == Mood.Happy)
            {
                ending = Ending.Good;
                Finish(chokes == 0 && Remaining >= s.greatRemainingSeconds ? MiniGameRank.大成功 : MiniGameRank.成功);
            }
            else
            {
                ending = Ending.Overfull;
                Finish(MiniGameRank.成功);
            }
        }
    }

    protected override void OnTimeUp()
    {
        var mood = CurrentMood();
        if (mood == Mood.Happy)      { ending = Ending.Good;     Finish(MiniGameRank.成功); }
        else if (mood == Mood.Full)  { ending = Ending.Overfull; Finish(MiniGameRank.成功); }
        else                         { ending = Ending.Hungry;   Finish(MiniGameRank.失敗); }
    }

    private void Refresh()
    {
        var mood = CurrentMood();

        Sprite face;
        switch (mood)
        {
            case Mood.Drinking: face = art.faceDrinking; break;
            case Mood.Happy:    face = art.faceHappy; break;
            case Mood.Full:     face = art.faceFull; break;
            case Mood.Vomit:    face = art.faceVomit; break;
            case Mood.Choke:    face = art.faceChoke; break;
            default:            face = art.faceHungry; break;
        }
        MiniGameUIKit.SetPixelSprite(faceImage, face, Scale);

        // 哺乳瓶：飲んでいる間は口に、休けい中は少し離す、吐いたら遠ざける
        if (bottleRect != null)
        {
            float x = Holding ? -Scale : (mood == Mood.Vomit ? 8 * Scale : 3 * Scale);
            bottleRect.anchoredPosition = new Vector2(x, -BottleDrop * Scale);
        }

        if (gulpLabel != null)
            gulpLabel.text = Holding ? (Mathf.FloorToInt(drinkAnim / 0.4f) % 2 == 0 ? "ごくごく" : "ごくっ") : "";

        if (hintLabel != null)
        {
            if (mood == Mood.Choke)                               { hintLabel.text = "むせちゃった！\n息つぎさせてあげて"; hintLabel.color = Orange; }
            else if (Holding && holdTime >= s.chokeAfter - 1f)    { hintLabel.text = "そろそろ\nひと休みさせて…"; hintLabel.color = Orange; }
            else if (Holding)                                     { hintLabel.text = "ごくごく飲んでる"; hintLabel.color = Dark; }
            else                                                  { hintLabel.text = "長押しで飲ませる\nはなすと休けい"; hintLabel.color = Dark; }
        }

        if (restGroup != null)
        {
            restGroup.SetActive(restTimer > 0f);
            MiniGameUIKit.SetBar(restFill, s.finishRestSeconds > 0f ? restTimer / s.finishRestSeconds : 0f);
        }
    }
}
