/// <summary>
/// 子どもの名前の入力値を検証するユーティリティ（static）。
/// 長さ・禁止文字の判定を1か所にまとめる。
/// </summary>
public static class ChildNameValidator
{
    /// <summary>
    /// 名前の検証を行う。
    /// </summary>
    /// <param name="name">検証する名前（前後の空白はあらかじめ削っておくこと）</param>
    /// <param name="minLength">最小文字数（呼ぶ側から渡す）</param>
    /// <param name="maxLength">最大文字数（呼ぶ側から渡す）</param>
    /// <param name="errorMessage">NG の場合に画面に出す理由の文字列（OK なら空文字）</param>
    /// <returns>OK なら true、NG なら false</returns>
    public static bool Validate(string name, int minLength, int maxLength, out string errorMessage)
    {
        // 空・null
        if (string.IsNullOrEmpty(name))
        {
            errorMessage = "名前を入力してください";
            return false;
        }

        // 文字数が短い
        if (name.Length < minLength)
        {
            errorMessage = $"{minLength}文字以上で入力してください";
            return false;
        }

        // 文字数が長い
        if (name.Length > maxLength)
        {
            errorMessage = $"{maxLength}文字以内で入力してください";
            return false;
        }

        // 禁止文字（{ } を含むと {name} 置き換えを壊す）
        if (name.Contains("{") || name.Contains("}"))
        {
            errorMessage = "「{」「}」は使えません";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
