using System.Collections.Generic;
using Maruyama.Core;
using Maruyama.Events;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 進路パネルの画面（PathScreens）を、イベントツールで直せるイベント（出方＝コードから）として作る。
/// すでにある id は作らない（足りない分だけ作る）。置き場所：Assets/Yamaguchi/Resources/Events/Path/。
/// </summary>
public static class PathScreenFactory
{
    public const string Dir = "Assets/Yamaguchi/Resources/Events/Path";

    [MenuItem("子育て/進路/進路の画面をイベントとして作る")]
    public static void CreateMissing() => CreateMissing(false);

    /// <summary>quiet = true ならダイアログを出さずに、作った数を返す。</summary>
    public static int CreateMissing(bool quiet)
    {
        SupportEditorGUI.EnsureFolder(Dir);
        var existing = new HashSet<string>();
        foreach (var e in EventRegistry.All()) if (e != null && !string.IsNullOrEmpty(e.id)) existing.Add(e.id);

        int made = 0;
        EventData first = null;
        foreach (var d in PathScreens.All)
        {
            if (existing.Contains(d.id)) continue;
            var e = ScriptableObject.CreateInstance<EventData>();
            e.id = d.id;
            e.title = d.title;
            e.description = d.description;
            e.imageNote = d.imageNote;
            e.trigger = EventTrigger.Manual;
            e.category = d.id.EndsWith("_fail") || d.id == "path_retry" || d.id == "path_rec_cannot" ? EventCategory.Demerit : EventCategory.Merit;
            e.choices = new List<EventChoice>
            {
                new EventChoice { id = "c1", label = d.isChoice ? "（選択肢はゲームが作る）" : d.button, effects = new List<Effect>() }
            };
            string path = AssetDatabase.GenerateUniqueAssetPath($"{Dir}/Path_{d.id.Substring(PathScreens.IdPrefix.Length)}.asset");
            AssetDatabase.CreateAsset(e, path);
            if (first == null) first = e;
            made++;
        }
        AssetDatabase.SaveAssets();
        EventRegistry.Invalidate();
        if (quiet) return made;
        EditorUtility.DisplayDialog("進路の画面",
            made > 0 ? $"{made}個作りました（{Dir}）。\nイベントツールで、タイトル・説明・画像を直せます。" : "全部そろっています。新しく作るものはありません。", "OK");
        if (first != null) Selection.activeObject = first;
        return made;
    }

    /// <summary>イベントツールに出す説明。進路の画面でなければ null。</summary>
    public static string HelpFor(EventData e)
    {
        if (e == null || !PathScreens.IsPathScreen(e.id)) return null;
        var d = PathScreens.FindDef(e.id);
        return "進路の画面です（進路パネルで出ます。月の抽選には入りません）。\n" +
               "使われるのは：タイトル・説明・画像" + (d.isChoice ? "。選択肢は進路のデータからゲームが作るので、ここの選択肢は使いません。" : "・1つ目の選択肢のラベル（ボタンの文字）。") + "\n" +
               "使われないもの：出方・条件・効果。\n" +
               "差し込み：{name} 子の名前／{path} 進路の名前／{ronin}「（浪人2年）」／{reason} 推薦に出せない理由";
    }
}
