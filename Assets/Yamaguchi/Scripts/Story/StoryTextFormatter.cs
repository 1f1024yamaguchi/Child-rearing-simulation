using UnityEngine;

/// <summary>
/// ストーリーやイベント、チュートリアルの文章内の置換（{name} など）を行う窓口。
/// 子どもの名前を1か所（Household.I.Child.DisplayName）から取得して置換する。
/// </summary>
public static class StoryTextFormatter
{
    /// <summary>
    /// 現在の子どもの表示名。Household や Child が未設定の場合は「みらい」を返す。
    /// </summary>
    public static string ChildName
    {
        get
        {
            if (Household.I != null && Household.I.Child != null && !string.IsNullOrEmpty(Household.I.Child.DisplayName))
            {
                return Household.I.Child.DisplayName;
            }
            return "みらい";
        }
    }

    /// <summary>
    /// 文章中の "{name}" を子どもの名前に置き換える。
    /// </summary>
    public static string Format(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Replace("{name}", ChildName);
    }
}

