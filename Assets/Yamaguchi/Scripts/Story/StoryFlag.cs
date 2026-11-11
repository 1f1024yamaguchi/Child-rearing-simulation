using UnityEngine;

/// <summary>
/// ストーリー分岐で使用するフラグの定義 ScriptableObject。
/// </summary>
[CreateAssetMenu(fileName = "StoryFlag_", menuName = "子育て/ストーリー/フラグ")]
public class StoryFlag : ScriptableObject
{
    [Tooltip("フラグの識別ID（英数字と_のみ。セーブや条件判定で使用）")]
    public string id = "";

    [Tooltip("フラグの表示名")]
    public string displayName = "";

    [TextArea(2, 4)]
    [Tooltip("フラグの説明・用途メモ")]
    public string description = "";

    [Header("趣味の呼び名（任意・⑤で使用）")]
    [Tooltip("このフラグが立っているときに子どもの趣味表示を置き換えるか")]
    public bool useCustomHobbyName = false;

    [Tooltip("置き換え後の趣味の呼び名（例: 野球）")]
    public string customHobbyName = "";

    [Tooltip("対象となる趣味の分類（例: スポーツ）")]
    public HobbyKind hobbyKind = HobbyKind.スポーツ;
}

