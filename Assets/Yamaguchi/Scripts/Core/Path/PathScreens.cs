using System.Collections.Generic;
using Maruyama.Events;
using UnityEngine;

/// <summary>
/// 進路パネル（PathPanelUI）に出す画面の「中身」。イベントツールで作った EventData（出方＝コードから）を id で探して、
/// タイトル・説明・画像・（お知らせは）1つ目の選択肢のラベルを使う。アセットが無ければ、ここに書いた既定の文で出す。
/// 置き場所：Assets/Yamaguchi/Resources/Events/Path/（メニュー「子育て/進路/進路の画面をイベントとして作る」で足りない分を作る）。
/// 出方が「コードから」なので、月の抽選には入らない。条件・効果・出方の設定は使わない。
///
/// 説明に使える差し込み：{name}（子の名前）{path}（進路の名前）{ronin}（「（浪人2年）」。浪人していなければ空）{reason}（推薦に出せない理由）
/// </summary>
public static class PathScreens
{
    public const string ResourcesPath = "Events";
    public const string IdPrefix = "path_";

    public class Def
    {
        public string id, title, description, button, imageNote;
        public bool isChoice;   // true = 選択肢はゲームが作る（進路を決める・選び直し）
        public Def(string id, string title, string description, string imageNote, bool isChoice = false, string button = "わかった")
        { this.id = id; this.title = title; this.description = description; this.imageNote = imageNote; this.isChoice = isChoice; this.button = button; }
    }

    public static readonly Def[] All =
    {
        new Def("path_decide", "進路を決める", "{name}の進路を決める時期になった。\n★は本人の希望。希望と違う進路を選ぶと、ストレスがたまる。", "高3の子が机で進路の資料を見ている", true),
        new Def("path_retry", "{path}は不合格", "{name}は不合格だった。これからどうする？\n★は本人の希望。", "合格発表の掲示板の前で落ち込む子", true),
        new Def("path_auto_decided", "{name}が自分で進路を決めた", "決めてもらえなかったので、{name}は自分の希望で「{path}」に決めた。", "子が自分で書類を書いている"),
        new Def("path_hs_pass", "公立高校に合格", "{name}は第一志望の公立高校に合格した。", "高校の合格発表で喜ぶ子"),
        new Def("path_hs_fail", "公立高校は不合格", "{name}は公立高校に届かず、私立高校に通うことになった。入学金や制服代がかかる。", "私立高校の制服を受け取る子"),
        new Def("path_rec_cannot", "推薦に出願できなかった", "推薦の条件を満たしていない（{reason}）。2月の一般入試で大学を受ける。", "先生と話す子"),
        new Def("path_rec_pass", "推薦で合格", "{name}は推薦で合格した！ 春から{path}の道に進む。", "合格通知を手に喜ぶ子と家族"),
        new Def("path_rec_fail", "推薦は不合格", "推薦は届かなかった。2月の一般入試で大学を受ける。", "机に向かい直す子"),
        new Def("path_pass_univ", "大学に合格", "{name}は{path}に合格した！{ronin}", "大学の合格発表で喜ぶ子"),
        new Def("path_pass_senmon", "専門学校に合格", "{name}は{path}に合格した。{ronin}", "専門学校の入学案内を見る子"),
    };

    private static Dictionary<string, EventData> assets;

    public static Def FindDef(string id)
    {
        foreach (var d in All) if (d.id == id) return d;
        return null;
    }

    /// <summary>id のイベントのアセット（無ければ null）。</summary>
    public static EventData FindAsset(string id)
    {
        if (assets == null)
        {
            assets = new Dictionary<string, EventData>();
            foreach (var e in Resources.LoadAll<EventData>(ResourcesPath))
                if (e != null && !string.IsNullOrEmpty(e.id) && e.id.StartsWith(IdPrefix) && !assets.ContainsKey(e.id)) assets[e.id] = e;
        }
        EventData found;
        return id != null && assets.TryGetValue(id, out found) ? found : null;
    }

    public static bool IsPathScreen(string id) => FindDef(id) != null;

    /// <summary>出す中身。候補の id を前から探し、アセット → 既定の文 の順で使う。</summary>
    public static void Resolve(string[] ids, Dictionary<string, string> tokens,
        out string title, out string description, out Sprite image, out string button)
    {
        title = description = ""; image = null; button = "わかった";
        Def def = null; EventData asset = null;
        foreach (var id in ids)
        {
            if (asset == null) asset = FindAsset(id);
            if (def == null) def = FindDef(id);
            if (asset != null) break;
        }
        if (def != null) { title = def.title; description = def.description; button = def.button; }
        if (asset != null)
        {
            if (!string.IsNullOrWhiteSpace(asset.title)) title = asset.title;
            if (!string.IsNullOrWhiteSpace(asset.description)) description = asset.description;
            image = asset.iconSprite;
            if (asset.choices != null && asset.choices.Count > 0 && !string.IsNullOrWhiteSpace(asset.choices[0].label)) button = asset.choices[0].label;
        }
        title = Replace(title, tokens);
        description = Replace(description, tokens);
    }

    private static string Replace(string s, Dictionary<string, string> tokens)
    {
        if (string.IsNullOrEmpty(s) || tokens == null) return s ?? "";
        foreach (var kv in tokens) s = s.Replace("{" + kv.Key + "}", kv.Value ?? "");
        return s;
    }
}
