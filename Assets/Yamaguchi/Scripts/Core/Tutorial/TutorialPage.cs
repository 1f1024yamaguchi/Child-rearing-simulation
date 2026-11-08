using System;
using UnityEngine;

/// <summary>
/// チュートリアルの1ページ。上の枠に出す画像と、その下に出す見出し・説明文。
/// 位置や大きさは持たない（配置は Canvas 上の TutorialPanel で決める）。
/// </summary>
[Serializable]
public class TutorialPage
{
    [Tooltip("上の枠に出すドット絵。未設定のあいだは灰色の差し替え枠と、下の「絵の指示メモ」が表示される")]
    public Sprite image;

    [Tooltip("絵の指示メモ（絵を描く人向け）。画像が未設定のあいだだけ枠の中に表示される。画像を入れたら表示されない")]
    [TextArea(2, 4)] public string imageNote;

    [Tooltip("画像の下に大きく出す見出し（例：家出）")]
    public string heading;

    [Tooltip("見出しの下に出す説明文。改行できる")]
    [TextArea(3, 6)] public string body;
}
