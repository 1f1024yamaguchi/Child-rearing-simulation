using System.Collections;
using UnityEngine;

/// <summary>
/// 本編シーンの GameManager に付ける。タイトルから渡された GameLaunchRequest を見て、
/// Continue なら指定の枠をロードする。NewGame / None（エディタで本編を直接再生）は何もしない。
///
/// なぜ1フレーム待つか：
/// 参加者（ISaveParticipant）は OnEnable で Register するので、シーンが読み込まれた時点で全員そろっている。
/// ただし他の部品の Start（食事の最初の選択、初期値の設定など）はどの順で呼ばれるか決まっていないので、
/// Start の中で Load すると「ロードのあとに Start が初期値で上書きする」おそれがある。
/// そこで Start では時間を止めて1フレーム待ち、全員の Start が終わってから Load する。
/// </summary>
public class GameStartup : MonoBehaviour
{
    private IEnumerator Start()
    {
        var mode = GameLaunchRequest.Consume(out var slot);
        if (mode != GameLaunchRequest.Mode.Continue) yield break;

        // 待っている1フレームのあいだに時間が進まないようにする
        bool paused = false;
        if (GameClock.I != null)
        {
            GameClock.I.PushPause();
            paused = true;
        }

        yield return null;   // 全員の Start が終わるのを待つ

        try
        {
            if (!SaveSystem.Load(slot))
            {
                Debug.LogWarning($"[GameStartup] {slot} 枠をロードできなかったので、初期値のまま始めます。");
            }
        }
        finally
        {
            if (paused && GameClock.I != null) GameClock.I.PopPause();
        }
    }
}
