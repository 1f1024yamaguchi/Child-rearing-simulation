using System;
using UnityEngine;

namespace Maruyama.Core
{
    /// <summary>
    /// 丸山担当機能（イベントシステム）専用のローカルな仮子どもステータスモデル。
    /// 既存 Core を変更せずに独立動作させるための実装。
    /// </summary>
    public class MaruyamaChildStatus : MonoBehaviour, IChildStatus
    {
        public static MaruyamaChildStatus Instance { get; private set; }

        [Header("子どもの仮ステータス初期値")]
        [SerializeField] private float health = 100f;
        [SerializeField] private float stress = 0f;

        public event Action<float> OnHealthChanged;
        public event Action<float> OnStressChanged;

        public float Health => health;
        public float Stress => stress;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void AddHealth(float delta)
        {
            health = Mathf.Clamp(health + delta, 0f, 100f);
            Debug.Log($"[MaruyamaChildStatus] 子どもの体調変化: {delta:+#;-#;0} => 現在の体調: {health}");
            OnHealthChanged?.Invoke(health);
        }

        public void AddStress(float delta)
        {
            stress = Mathf.Clamp(stress + delta, 0f, 100f);
            Debug.Log($"[MaruyamaChildStatus] 子どものストレス変化: {delta:+#;-#;0} => 現在のストレス: {stress}");
            OnStressChanged?.Invoke(stress);
        }
    }
}
