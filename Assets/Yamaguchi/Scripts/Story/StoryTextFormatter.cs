using UnityEngine;

/// <summary>
/// ストーリーやイベント、チュートリアルの文章内の置換（{name}・{dream}）を行う窓口。
/// 子どもの名前を1か所（Household.I.Child.DisplayName）から取得して置換する。
/// </summary>
public static class StoryTextFormatter
{
    /// <summary>
    /// 現在の子どもの表示名。Household や Child が未設定の場合、または名前が空か「未設定」の場合は「みらい」を返す。
    /// （PersonStatus の既定値が「未設定」なので、シーンで名前を入れ忘れても「未設定」と出さない）
    /// </summary>
    public static string ChildName
    {
        get
        {
            if (Household.I != null && Household.I.Child != null)
            {
                string name = Household.I.Child.DisplayName;
                if (!string.IsNullOrEmpty(name) && name != "未設定")
                    return name;
            }
            return "みらい";
        }
    }

    /// <summary>
    /// 将来の夢の職業名（DreamSystem）。まだ決まっていなければ空文字。
    /// </summary>
    public static string DreamName
    {
        get
        {
            var ds = DreamSystem.I;
            return ds != null && ds.HasDream ? ds.Dream.DisplayNameOrName : "";
        }
    }

    /// <summary>
    /// 文章中の "{name}" を子どもの名前に、"{dream}" を将来の夢の職業名に置き換える。
    /// </summary>
    public static string Format(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        text = text.Replace("{name}", ChildName);
        if (text.Contains("{dream}")) text = text.Replace("{dream}", DreamName);
        return text;
    }
}

