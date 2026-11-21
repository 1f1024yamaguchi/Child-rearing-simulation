using System.Collections;
using UnityEngine;

/// <summary>
/// 本編シーンの GameManager に付ける。タイトルから渡された GameLaunchRequest を見て、
/// Continue なら指定の枠をロードし、NewGame なら名前決め画面を出す。
///
/// なぜ1フレーム待つか：
/// 参加者（ISaveParticipant）は OnEnable で Register するので、シーンが読み込まれた時点で全員そろっている。
/// ただし他の部品の Start（食事の最初の選択、初期値の設定など）はどの順で呼ばれるか決まっていないので、
/// Start の中で Load すると「ロードのあとに Start が初期値で上書きする」おそれがある。
/// そこで Start では時間を止めて1フレーム待ち、全員の Start が終わってから Load する。
/// </summary>
public class GameStartup : MonoBehaviour
{
    [Header("名前決め")]
    [Tooltip("子どもの名前を決める流れを管理するコンポーネント。NewGame 時に呼ぶ。未設定なら名前は「みらい」のまま始まる。")]
    [SerializeField] private ChildNamingFlow childNamingFlow;

    [Tooltip("エディタで本編を直接再生（Mode=None）したときも名前決め画面を出すか。開発用。")]
    [SerializeField] private bool showInEditorPlay = false;

    private IEnumerator Start()
    {
        // GameLaunchRequest は1回だけ読む（Consume で None に戻るため、他で読まない）
        var mode = GameLaunchRequest.Consume(out var slot);

        if (mode == GameLaunchRequest.Mode.Continue)
        {
            // ─── 続きから：1フレーム待ってからロード ───
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
        else if (mode == GameLaunchRequest.Mode.NewGame ||
                 (mode == GameLaunchRequest.Mode.None && showInEditorPlay))
        {
            // ─── 最初から（またはエディタで直接再生 & showInEditorPlay=ON）：名前決め画面を出す ───
            yield return null;   // 全員の Start が終わるのを待つ

            if (childNamingFlow != null)
            {
                childNamingFlow.EnsurePanelRuntime();
                childNamingFlow.BeginAfterTutorial();
            }
            else
            {
                childNamingFlow = FindFirstObjectByType<ChildNamingFlow>(FindObjectsInactive.Include);
                if (childNamingFlow == null)
                    childNamingFlow = gameObject.AddComponent<ChildNamingFlow>();

                childNamingFlow.EnsurePanelRuntime();
                childNamingFlow.BeginAfterTutorial();
            }
        }
        // mode == None かつ showInEditorPlay=false → 何もしない（今まで通り）
    }
}
