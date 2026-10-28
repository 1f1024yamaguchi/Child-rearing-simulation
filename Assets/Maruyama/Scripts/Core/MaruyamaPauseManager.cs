using UnityEngine;

namespace Maruyama.Core
{
    /// <summary>
    /// ADR 0004 準拠の時間停止マネージャー。
    /// GameClock.PushPause / PopPause を呼び出すことで、
    /// 食事選択画面（MealSelectionDialogUI）など他の停止要求との競合を防ぐ。
    /// 参照カウントは GameClock 側が一元管理するため、
    /// 他の理由で停止中の時間を誤って再開させることはない。
    /// </summary>
    public class MaruyamaPauseManager : MonoBehaviour
    {
        public static MaruyamaPauseManager Instance { get; private set; }

        /// <summary>このマネージャーが GameClock に積んでいる停止要求の数</summary>
        private int pauseRequests = 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// 時間停止を要求する。
        /// GameClock.PushPause() を呼ぶため、他の停止要求と安全に共存できる。
        /// </summary>
        public void PushPause()
        {
            pauseRequests++;
            if (pauseRequests == 1)
            {
                if (GameClock.I != null)
                {
                    GameClock.I.PushPause();
                    Debug.Log("[MaruyamaPauseManager] 時間を停止しました (PushPause → GameClock.PushPause)");
                }
                else
                {
                    Debug.LogWarning("[MaruyamaPauseManager] GameClock が見つかりません。停止できませんでした。");
                }
            }
        }

        /// <summary>
        /// 時間停止要求を解除する。
        /// このマネージャーが積んだ分だけ GameClock.PopPause() を呼ぶ。
        /// 他の理由で停止中の時間を誤って再開させることはない。
        /// </summary>
        public void PopPause()
        {
            if (pauseRequests <= 0)
            {
                Debug.LogWarning("[MaruyamaPauseManager] PopPause が呼ばれましたが、対応する PushPause がありません。無視します。");
                return;
            }

            pauseRequests--;
            if (pauseRequests == 0)
            {
                if (GameClock.I != null)
                {
                    GameClock.I.PopPause();
                    Debug.Log("[MaruyamaPauseManager] 時間を再開しました (PopPause → GameClock.PopPause)");
                }
                else
                {
                    Debug.LogWarning("[MaruyamaPauseManager] GameClock が見つかりません。再開できませんでした。");
                }
            }
        }

        private void OnDestroy()
        {
            // 積んだままの停止要求を解放する
            while (pauseRequests > 0)
            {
                pauseRequests--;
                if (GameClock.I != null)
                {
                    GameClock.I.PopPause();
                }
            }
        }
    }
}
