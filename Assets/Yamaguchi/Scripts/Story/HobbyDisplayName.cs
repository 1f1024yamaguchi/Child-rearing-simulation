using UnityEngine;

/// <summary>
/// 子どもの趣味の表示名（呼び名）を取得する窓口。
/// 立っているフラグのうち、useCustomHobbyName が true で hobbyKind が一致するもののうち、
/// 後から立ったものの customHobbyName を返す。無ければ HobbyKind の名前（分類名）。
/// </summary>
public static class HobbyDisplayName
{
    /// <summary>
    /// 現在有効な趣味の表示名を取得する。
    /// </summary>
    public static string Get(HobbyKind kind)
    {
        if (kind == HobbyKind.なし) return "なし";

        var state = StoryState.I;
        if (state != null)
        {
            var flagOrder = state.ActiveFlagOrder;
            // 後から立ったもの（リストの後ろ）を優先して調べる
            for (int i = flagOrder.Count - 1; i >= 0; i--)
            {
                string flagId = flagOrder[i];
                var flag = state.GetFlagAsset(flagId);
                if (flag != null && flag.useCustomHobbyName && flag.hobbyKind == kind && !string.IsNullOrEmpty(flag.customHobbyName))
                {
                    return flag.customHobbyName;
                }
            }
        }

        return kind.ToString();
    }
}

