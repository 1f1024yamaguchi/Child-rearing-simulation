using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ミニゲーム用の小物のドット絵を PNG に書き出し、MiniGameArt に登録する。
/// 家族キャラと同じ「1文字＝1ドット」のマス目方式・同じパレット（輪郭 #2B2233）で描いている。
/// 絵を直したいときは下のマス目を書き換えてメニューを実行し直すか、
/// 描いた PNG を MiniGameArt.asset の欄に差し替える。
/// メニュー：子育て → ミニゲーム → ドット絵を作り直す
/// </summary>
public static class MiniGamePixelArtBuilder
{
    private const string Folder = "Assets/Images_animation/MiniGame";
    private const string ArtAssetPath = "Assets/Yamaguchi/Resources/MiniGameArt.asset";
    private const float PixelsPerUnit = 20f;   // 家族キャラ・部屋と同じ

    // 家族キャラのパレット ＋ 小物用に数色
    private static readonly Dictionary<char, string> Palette = new Dictionary<char, string>
    {
        { 'o', "2B2233" },  // 輪郭
        { 'W', "FFFFFF" },
        { 'w', "EDEFF5" },  // ガラス
        { 'L', "C9CCD6" },  // 明るい灰
        { 'k', "8A8FA0" },  // 灰（目盛り・金属の影）
        { 'Y', "FFF3C4" },  // ミルク
        { 'T', "F7DC8A" },  // 乳首（ベージュ）
        { 'U', "3F6FB5" },  // 青
        { 'l', "8FCBEF" },  // 水色
        { 'R', "D9534F" },  // 赤
        { 'P', "F4A6A0" },  // ピンク
        { 'g', "5CB85C" },  // 緑
        { 'b', "C68A55" },  // 木
        { 'B', "A0522D" },  // 濃い木
        { 'y', "F2C14E" },  // 黄
        { 'n', "DCEBFA" },  // マットレス
        { 'H', "3B2A20" },  // 髪（家族キャラと同じ）
        { 'S', "F5CBA7" },  // 肌（家族キャラと同じ）
    };

    // ───────────── マス目（手で描いたもの） ─────────────

    private static readonly string[] Bottle =
    {
        "....oooo....",
        "....oTTo....",
        "....oTTo....",
        "...oTTTTo...",
        "..oooooooo..",
        "..oUlUUUUo..",
        "..oUUUUUUo..",
        "..oooooooo..",
        ".oWwwwwwwwo.",
        ".oWwwwwwwwo.",
        ".oWwwwwwwwo.",
        ".oYYYYYYYYo.",
        ".okYYYYYYYo.",
        ".oYYYYYYYYo.",
        ".okYYYYYYYo.",
        ".oYYYYYYYYo.",
        ".okYYYYYYYo.",
        ".oYYYYYYYYo.",
        ".oYYYYYYYYo.",
        ".oTYYYYYYTo.",
        "..oTTTTTTo..",
        "...oooooo...",
    };

    /// 横向きの哺乳瓶（18×8、乳首が左）。顔のアップの口に当てても目を隠さない細さにしている。
    private static readonly string[] BottleSide =
    {
        ".......ooooooooooo",
        "....ooooWwwwwwwwwo",
        ".ooooUUoWwwwwwwwwo",
        "oTTToUUoYYYkYYYkYo",
        "oTTToUUoYYYYYYYYYo",
        ".ooooUUoYYYYYYYYYo",
        "....ooooYYYYYYYYYo",
        ".......ooooooooooo",
    };

    private static readonly string[] Steam0 =
    {
        "..w.....w...",
        "...w.....w..",
        "...w....w...",
        "..w....w....",
        "..w.....w...",
        "...L.....L..",
        "...L....L...",
        "............",
    };

    private static readonly string[] Steam1 =
    {
        "...w....w...",
        "..w....w....",
        "..w.....w...",
        "...w.....w..",
        "...w....w...",
        "..L....L....",
        "..L.....L...",
        "............",
    };

    private static readonly string[] ZoneArrow =
    {
        "o...",
        "oo..",
        "ogo.",
        "oggo",
        "ogo.",
        "oo..",
        "o...",
    };

    private static readonly string[] WaterDrop =
    {
        "...o...",
        "..olo..",
        "..olo..",
        ".owllo.",
        "owlllUo",
        "olllllo",
        "ollllUo",
        ".oUUUo.",
        "..ooo..",
    };

    private static readonly string[] FaucetKnob =
    {
        "...oooo...",
        ".ooLwwLoo.",
        ".oLwwwwLo.",
        "oLwwwwwwLo",
        "oLwwkkwwLo",
        "oLwwkkwwLo",
        "oLLwwwwLLo",
        ".okLLLLko.",
        ".ookkkkoo.",
        "...oooo...",
    };

    private static readonly string[] IconCry =
    {
        "oo....oo",
        "lU....Ul",
        "l......l",
        "l..oo..l",
        "..o..o..",
        "........",
    };

    private static readonly string[] IconZzz =
    {
        "UUUU....",
        "..U.....",
        ".U......",
        "UUUU.UUU",
        "......U.",
        ".....U..",
        ".....UUU",
    };

    private static readonly string[] IconNote =
    {
        "...RR.",
        "...R.R",
        "...R..",
        "...R..",
        ".RRR..",
        "RRRR..",
        ".RR...",
    };

    // ───────────── 形が規則的なもの（マス目を組み立てる） ─────────────

    /// 温度計の枠（11×39）。管の中（x=4〜6, 上から2〜32行）は透明。
    private static string[] ThermometerFrame()
    {
        var rows = new List<string> { "...ooooo...", "..owwwwwo.." };
        for (int y = 2; y <= 32; y++) rows.Add(y % 3 == 1 ? "..ow...wok." : "..ow...wo..");
        rows.Add(".owRRRRRwo.");
        rows.Add("owRRRRRRRwo");
        rows.Add("owRWRRRRRwo");
        rows.Add("owRRRRRRRwo");
        rows.Add(".owRRRRRwo.");
        rows.Add("..ooooooo..");
        return rows.ToArray();
    }

    /// 温度計の中身（3×31）。
    private static string[] ThermometerFill()
    {
        var rows = new string[31];
        for (int i = 0; i < rows.Length; i++) rows[i] = "PRR";
        return rows;
    }

    /// スライダーの溝（48×8）。左＝水、右＝お湯。
    private static string[] SliderTrack()
    {
        const int w = 48, h = 8;
        var rows = new string[h];
        for (int y = 0; y < h; y++)
        {
            var c = new char[w];
            for (int x = 0; x < w; x++)
            {
                bool edgeY = y == 0 || y == h - 1;
                bool edgeX = x == 0 || x == w - 1;
                if (edgeY && edgeX) c[x] = '.';
                else if (edgeY || edgeX) c[x] = 'o';
                else if (x == 23 || x == 24) c[x] = 'w';
                else if (x < 23) c[x] = y <= 2 ? 'l' : 'U';
                else c[x] = y <= 2 ? 'P' : 'R';
            }
            rows[y] = new string(c);
        }
        return rows;
    }

    /// ベビーベッド（34×20）。赤ちゃんの絵（10×13ドット・寝姿 23×20ドット）に合わせた大きさ。
    /// マットレスの上端は下から 8 ドット目（SleepRhythmGame.MattressTop = 8/20）。
    private static string[] Crib()
    {
        const int w = 34, h = 20;
        var rows = new string[h];
        for (int y = 0; y < h; y++)
        {
            var c = new char[w];
            for (int x = 0; x < w; x++)
            {
                char ch = '.';
                bool post = x <= 3 || x >= 30;
                if (post)
                {
                    bool edge = x == 0 || x == 3 || x == 30 || x == 33 || y == 0 || y == h - 1;
                    ch = edge ? 'o' : (y == 1 ? 'b' : 'B');
                }
                else if (y >= 3 && y <= 5)        ch = (y == 3 || y == 5) ? 'o' : 'b';      // 上の手すり
                else if (y >= 6 && y <= 11)
                {
                    int m = (x - 4) % 4;                                                    // 柵
                    ch = m == 1 || m == 3 ? 'o' : (m == 2 ? 'b' : '.');
                }
                else if (y >= 12 && y <= 15)      ch = (y == 12 || y == 15) ? 'o' : (y == 13 ? 'W' : 'n');   // マットレス
                else if (y == 16)                 ch = 'b';                                  // 下の板
                else if (y == 17)                 ch = 'o';
                c[x] = ch;
            }
            rows[y] = new string(c);
        }
        return rows;
    }

    /// 輪っか（size×size、太さ約2ドット）。
    private static string[] Ring(char color, int size)
    {
        float r = size * 0.5f;
        float center = r - 0.5f;
        var rows = new string[size];
        for (int y = 0; y < size; y++)
        {
            var c = new char[size];
            for (int x = 0; x < size; x++)
            {
                float dx = x - center, dy = y - center;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                c[x] = d >= r - 2.2f && d <= r - 0.3f ? color : '.';
            }
            rows[y] = new string(c);
        }
        return rows;
    }

    /// PNG の中で実際に色が塗られている範囲（左下原点）。読めなければ fallback。
    private static RectInt ContentBounds(string path, RectInt fallback)
    {
        if (!File.Exists(path)) return fallback;
        var tex = new Texture2D(2, 2);
        if (!tex.LoadImage(File.ReadAllBytes(path))) { Object.DestroyImmediate(tex); return fallback; }
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (int y = 0; y < tex.height; y++)
            for (int x = 0; x < tex.width; x++)
                if (tex.GetPixel(x, y).a > 0.1f)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
        Object.DestroyImmediate(tex);
        return maxX < 0 ? fallback : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    /// 吹き出し（18×15）に絵文字のマス目を重ねる。
    private static string[] Bubble(string[] icon, int ox, int oy)
    {
        const int w = 18, h = 15;
        var g = new char[h, w];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) g[y, x] = '.';
        for (int y = 0; y <= 11; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool cornerY = y == 0 || y == 11;
                bool cornerX = x == 0 || x == w - 1;
                if (cornerY && cornerX) continue;
                g[y, x] = (cornerY || cornerX) ? 'o' : 'W';
            }
        }
        // しっぽ（左下）
        g[11, 4] = 'W'; g[11, 5] = 'W';
        g[12, 3] = 'o'; g[12, 4] = 'W'; g[12, 5] = 'W'; g[12, 6] = 'o';
        g[13, 3] = 'o'; g[13, 4] = 'W'; g[13, 5] = 'o';
        g[14, 3] = 'o'; g[14, 4] = 'o';
        for (int iy = 0; iy < icon.Length; iy++)
            for (int ix = 0; ix < icon[iy].Length; ix++)
                if (icon[iy][ix] != '.') g[oy + iy, ox + ix] = icon[iy][ix];

        var rows = new string[h];
        for (int y = 0; y < h; y++)
        {
            var c = new char[w];
            for (int x = 0; x < w; x++) c[x] = g[y, x];
            rows[y] = new string(c);
        }
        return rows;
    }

    /// 赤ちゃんの顔のアップ（24×22）。家族キャラと同じ肌・髪・輪郭の色で、表情ごとに目と口を描き分ける。
    /// kind：hungry（はらぺこ・泣き）/ drinking（ごくごく）/ happy（にこにこ）/ full（くるしい）/ vomit（けぽっ）/ choke（むせ）
    private static string[] Face(string kind)
    {
        const int w = 24, h = 22;
        var g = new char[h, w];
        var inside = new bool[h, w];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x - 11.5f) / 10.5f, dy = (y - 12.5f) / 9f;
                inside[y, x] = dx * dx + dy * dy <= 1f;
                g[y, x] = '.';
            }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!inside[y, x]) continue;
                bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1
                    || !inside[y - 1, x] || !inside[y + 1, x] || !inside[y, x - 1] || !inside[y, x + 1];
                g[y, x] = edge ? 'o' : (y <= 6 ? 'H' : 'S');      // 上のほうは髪
            }
        // ちょこんと立った髪
        g[1, 12] = 'o'; g[2, 11] = 'o'; g[2, 12] = 'H'; g[2, 13] = 'o'; g[3, 11] = 'o'; g[3, 12] = 'H'; g[3, 13] = 'o';

        switch (kind)
        {
            case "hungry":    // ぎゅっとつぶった目＋涙、大きく開いた口
                Stamp(g, 6, 12, "ooo"); Stamp(g, 15, 12, "ooo");
                Stamp(g, 7, 13, "l", "l"); Stamp(g, 16, 13, "l", "l");
                Stamp(g, 9, 15, ".oooo.", "oRRRRo", "oRRRRo", ".oooo.");
                break;
            case "drinking":  // 目を開けて、口は哺乳瓶をくわえる位置
                Stamp(g, 7, 11, "oo", "oo"); Stamp(g, 15, 11, "oo", "oo");
                Stamp(g, 4, 14, "PP"); Stamp(g, 18, 14, "PP");
                Stamp(g, 11, 16, "oo", "oo");
                break;
            case "happy":     // にっこり目、ほっぺ、笑った口
                Stamp(g, 6, 11, ".o.", "o.o"); Stamp(g, 15, 11, ".o.", "o.o");
                Stamp(g, 4, 13, "PP", "PP"); Stamp(g, 18, 13, "PP", "PP");
                Stamp(g, 10, 15, "o..o", ".oo.");
                break;
            case "full":      // 苦しそうな目、波打つ口、汗と青い線
                Stamp(g, 6, 12, "ooo"); Stamp(g, 15, 12, "ooo");
                Stamp(g, 7, 8, "UUU"); Stamp(g, 14, 8, "UUU");
                Stamp(g, 9, 16, "o.o.o.", ".o.o.o");
                Stamp(g, 20, 7, "l", "ll");
                break;
            case "vomit":     // ばつ目、口からミルク
                Stamp(g, 6, 10, "o.o", ".o.", "o.o"); Stamp(g, 15, 10, "o.o", ".o.", "o.o");
                Stamp(g, 10, 15, ".oo.", "oWWo", "oWWo");
                Stamp(g, 11, 18, "WW", "W.", "W.");
                break;
            case "choke":     // ＞＜の目、小さく開いた口、汗
                Stamp(g, 6, 10, "o..", ".o.", "o.."); Stamp(g, 15, 10, "..o", ".o.", "..o");
                Stamp(g, 10, 15, ".oo.", "oRRo", ".oo.");
                Stamp(g, 20, 8, "l", "l");
                break;
        }

        var rows = new string[h];
        for (int y = 0; y < h; y++)
        {
            var c = new char[w];
            for (int x = 0; x < w; x++) c[x] = g[y, x];
            rows[y] = new string(c);
        }
        return rows;
    }

    /// マス目に部品を重ねる（'.' は透明として上書きしない）。
    private static void Stamp(char[,] g, int x0, int y0, params string[] pat)
    {
        for (int iy = 0; iy < pat.Length; iy++)
            for (int ix = 0; ix < pat[iy].Length; ix++)
                if (pat[iy][ix] != '.') g[y0 + iy, x0 + ix] = pat[iy][ix];
    }

    /// マス目を左に90度回す（上にあったものが左に来る）。横向きの哺乳瓶に使う。
    private static string[] RotateLeft(string[] src)
    {
        int h = src.Length, w = src[0].Length;
        var rows = new string[w];
        for (int r = 0; r < w; r++)
        {
            var c = new char[h];
            for (int col = 0; col < h; col++) c[col] = src[col][w - 1 - r];
            rows[r] = new string(c);
        }
        return rows;
    }

    private static string[] Recolor(string[] src, char from1, char to1, char from2, char to2)
    {
        var rows = new string[src.Length];
        for (int i = 0; i < src.Length; i++) rows[i] = src[i].Replace(from1, to1).Replace(from2, to2);
        return rows;
    }

    // ───────────── 書き出し ─────────────

    [MenuItem("子育て/ミニゲーム/ドット絵を作り直す")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder);

        var files = new Dictionary<string, string[]>
        {
            { "milk_bottle", Bottle },
            { "milk_steam_0", Steam0 },
            { "milk_steam_1", Steam1 },
            { "thermometer_frame", ThermometerFrame() },
            { "thermometer_fill", ThermometerFill() },
            { "zone_arrow", ZoneArrow },
            { "water_drop", WaterDrop },
            { "hot_drop", Recolor(WaterDrop, 'l', 'P', 'U', 'R') },
            { "faucet_knob", FaucetKnob },
            { "slider_track", SliderTrack() },
            { "crib", Crib() },
            { "ring_target", Ring('l', 22) },
            { "ring_approach", Ring('y', 22) },
            { "bubble_cry", Bubble(IconCry, 5, 3) },
            { "bubble_zzz", Bubble(IconZzz, 5, 2) },
            { "bubble_note", Bubble(IconNote, 6, 2) },
            { "milk_bottle_side", BottleSide },
            { "baby_face_hungry", Face("hungry") },
            { "baby_face_drinking", Face("drinking") },
            { "baby_face_happy", Face("happy") },
            { "baby_face_full", Face("full") },
            { "baby_face_vomit", Face("vomit") },
            { "baby_face_choke", Face("choke") },
        };

        foreach (var kv in files) WritePng(Path.Combine(Folder, kv.Key + ".png"), kv.Value);
        AssetDatabase.Refresh();
        foreach (var kv in files) ConfigureImporter(Folder + "/" + kv.Key + ".png");

        RegisterArt();
        Debug.Log($"[MiniGamePixelArtBuilder] ドット絵を {files.Count} 枚書き出し、{ArtAssetPath} に登録しました。");
    }

    private static void WritePng(string path, string[] rows)
    {
        int h = rows.Length;
        int w = rows[0].Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var clear = new Color32(0, 0, 0, 0);
        for (int y = 0; y < h; y++)
        {
            if (rows[y].Length != w) Debug.LogError($"[MiniGamePixelArtBuilder] {path} の {y} 行目の幅が {rows[y].Length}（{w} のはず）");
            for (int x = 0; x < w; x++)
            {
                char ch = x < rows[y].Length ? rows[y][x] : '.';
                Color32 col = clear;
                if (Palette.TryGetValue(ch, out var hex))
                {
                    ColorUtility.TryParseHtmlString("#" + hex, out var c);
                    col = c;
                }
                tex.SetPixel(x, h - 1 - y, col);   // マス目は上の行から、テクスチャは下から
            }
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    private static void ConfigureImporter(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = PixelsPerUnit;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.SaveAndReimport();
    }

    private static Sprite S(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);
    private static Sprite M(string name) => S(Folder + "/" + name + ".png");

    private static void RegisterArt()
    {
        var art = AssetDatabase.LoadAssetAtPath<MiniGameArt>(ArtAssetPath);
        if (art == null)
        {
            art = ScriptableObject.CreateInstance<MiniGameArt>();
            AssetDatabase.CreateAsset(art, ArtAssetPath);
        }

        // 既存のドット絵（部屋・赤ちゃん）
        art.roomBackground = S("Assets/Images_animation/Room/room_bg.png");
        const string child = "Assets/Images_animation/Characters/Child/";
        art.babyAwake = new[] { S(child + "child_baby_idle_0.png"), S(child + "child_baby_idle_1.png") };
        art.babyAsleep = new[] { S(child + "child_baby_sleep_0.png"), S(child + "child_baby_sleep_1.png") };

        // 今回描いた小物
        art.bubbleCry = M("bubble_cry");
        art.bubbleNote = M("bubble_note");
        art.bubbleZzz = M("bubble_zzz");
        art.milkBottle = M("milk_bottle");
        art.steam = new[] { M("milk_steam_0"), M("milk_steam_1") };
        art.thermometerFrame = M("thermometer_frame");
        art.thermometerFill = M("thermometer_fill");
        art.zoneArrow = M("zone_arrow");
        art.waterDrop = M("water_drop");
        art.hotDrop = M("hot_drop");
        art.faucetKnob = M("faucet_knob");
        art.sliderTrack = M("slider_track");
        art.crib = M("crib");
        art.ringTarget = M("ring_target");
        art.ringApproach = M("ring_approach");
        art.milkBottleSide = M("milk_bottle_side");
        art.faceHungry = M("baby_face_hungry");
        art.faceDrinking = M("baby_face_drinking");
        art.faceHappy = M("baby_face_happy");
        art.faceFull = M("baby_face_full");
        art.faceVomit = M("baby_face_vomit");
        art.faceChoke = M("baby_face_choke");

        // 赤ちゃんの絵の中で実際に描かれている範囲（コマの左下に寄せて描かれているため、中央に置くのに使う）
        art.babyAwakeBounds = ContentBounds(child + "child_baby_idle_0.png", art.babyAwakeBounds);
        art.babyAsleepBounds = ContentBounds(child + "child_baby_sleep_0.png", art.babyAsleepBounds);

        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
    }
}
