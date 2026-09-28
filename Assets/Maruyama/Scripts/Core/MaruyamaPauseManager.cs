using UnityEngine;

namespace Maruyama.Core
{
    /// <summary>
    /// ADR 0004 準拠の時間停止マネージャー。
    /// 既存 GameClock.cs を直接改変せず、Maruyama 領域内で参照カウント管理を行い、
    /// GameClock.I.SetRunning() を安全に呼び出すラッパー。
    /// </summary>
    public class MaruyamaPauseManager : MonoBehaviour
    {
        public static MaruyamaPauseManager Instance { get; private set; }

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

        public void PushPause()
        {
            pauseRequests++;
            if (pauseRequests == 1)
            {
                var clock = FindFirstObjectByType<GameClock>();
                if (clock != null)
                {
                    clock.SetRunning(false);
                    Debug.Log("[MaruyamaPauseManager] 時間を停止しました (PushPause)");
                }
            }
        }

        public void PopPause()
        {
            pauseRequests = Mathf.Max(0, pauseRequests - 1);
            if (pauseRequests == 0)
            {
                var clock = FindFirstObjectByType<GameClock>();
                if (clock != null)
                {
                    clock.SetRunning(true);
                    Debug.Log("[MaruyamaPauseManager] 時間を再開しました (PopPause)");
                }
            }
        }
    }
}
