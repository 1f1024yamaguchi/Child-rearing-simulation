using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Arisaka
{
    /// <summary>
    /// ゲームの進行速度（倍速機能）を制御するUIコントローラー。
    /// 画面上部（年月表示の横）に設置され、以下の3段階の速度切り替えを行う：
    /// - 通常（1x: 1秒で1日）
    /// - 2x（1秒で2日）
    /// - 3x（1秒で3日）
    /// 
    /// Time.timeScale を変更することで、GameClock の Update() 内の while ループ消化頻度を高め、
    /// 日付進行、食費日割り引き落とし、日給積立、親ストレス計算などの日次処理（DayTicker）
    /// を1日ずつ欠落なく毎日計算したまま進行速度を変更する。
    /// </summary>
    public class GameSpeedUI : MonoBehaviour
    {
        public enum GameSpeed
        {
            Normal = 1,
            Speed2x = 2,
            Speed3x = 3
        }

        public static GameSpeedUI Instance { get; private set; }

        [Header("ボタン参照")]
        [SerializeField] private Button btnNormal;
        [SerializeField] private Button btn2x;
        [SerializeField] private Button btn3x;

        [Header("テキスト参照 (TextMeshPro)")]
        [SerializeField] private TextMeshProUGUI txtNormal;
        [SerializeField] private TextMeshProUGUI txt2x;
        [SerializeField] private TextMeshProUGUI txt3x;

        [Header("ボタン配色")]
        [SerializeField] private Color activeBgColor = new Color(0.16f, 0.83f, 0.96f, 1f); // シアン (#29D4F5)
        [SerializeField] private Color activeTextColor = Color.white;
        [SerializeField] private Color inactiveBgColor = new Color(0.93f, 0.93f, 0.93f, 1f); // パネルライトグレー
        [SerializeField] private Color inactiveTextColor = new Color(0.13f, 0.13f, 0.13f, 1f); // ダークテキスト

        [Header("現在の倍率設定")]
        [SerializeField] private GameSpeed currentSpeed = GameSpeed.Normal;

        public GameSpeed CurrentSpeed => currentSpeed;
        public event Action<GameSpeed> OnSpeedChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // ボタンのクリックリスナー登録
            if (btnNormal != null) btnNormal.onClick.AddListener(() => SetSpeed(GameSpeed.Normal));
            if (btn2x != null) btn2x.onClick.AddListener(() => SetSpeed(GameSpeed.Speed2x));
            if (btn3x != null) btn3x.onClick.AddListener(() => SetSpeed(GameSpeed.Speed3x));

            // 初期表示・速度反映（開始時は通常 1.0f）
            SetSpeed(currentSpeed);
        }

        private void Update()
        {
            HandleKeyInput();
        }

        private void HandleKeyInput()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) SetSpeed(GameSpeed.Normal);
                else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) SetSpeed(GameSpeed.Speed2x);
                else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) SetSpeed(GameSpeed.Speed3x);
            }
#else
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) SetSpeed(GameSpeed.Normal);
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) SetSpeed(GameSpeed.Speed2x);
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) SetSpeed(GameSpeed.Speed3x);
#endif
        }

        private void OnDestroy()
        {
            // シーン破棄時やゲーム終了時は必ず通常速度(1.0f)に戻す
            Time.timeScale = 1.0f;
            Time.fixedDeltaTime = 0.02f;
        }

        /// <summary>
        /// 進行速度を変更する
        /// </summary>
        public void SetSpeed(GameSpeed speed)
        {
            currentSpeed = speed;
            float scale = (float)speed;

            Time.timeScale = scale;
            Time.fixedDeltaTime = 0.02f * scale;

            UpdateButtonVisuals();
            OnSpeedChanged?.Invoke(speed);

            Debug.Log($"[GameSpeedUI] ゲーム進行速度を変更: {speed} (Time.timeScale = {scale})");
        }

        /// <summary>
        /// ボタンの選択状態（背景色・文字色・太字）を更新する
        /// </summary>
        private void UpdateButtonVisuals()
        {
            UpdateButton(btnNormal, txtNormal, currentSpeed == GameSpeed.Normal);
            UpdateButton(btn2x, txt2x, currentSpeed == GameSpeed.Speed2x);
            UpdateButton(btn3x, txt3x, currentSpeed == GameSpeed.Speed3x);
        }

        private void UpdateButton(Button btn, TextMeshProUGUI txt, bool isActive)
        {
            if (btn != null)
            {
                var img = btn.GetComponent<Image>();
                if (img != null)
                {
                    img.color = isActive ? activeBgColor : inactiveBgColor;
                }
            }

            if (txt != null)
            {
                txt.color = isActive ? activeTextColor : inactiveTextColor;
                txt.fontStyle = isActive ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        /// <summary>
        /// インスペクターや外部から参照をセットするための初期化メソッド
        /// </summary>
        public void Bind(Button bNormal, TextMeshProUGUI tNormal,
                         Button b2x, TextMeshProUGUI t2x,
                         Button b3x, TextMeshProUGUI t3x)
        {
            btnNormal = bNormal;
            txtNormal = tNormal;
            btn2x = b2x;
            txt2x = t2x;
            btn3x = b3x;
            txt3x = t3x;

            if (btnNormal != null) btnNormal.onClick.AddListener(() => SetSpeed(GameSpeed.Normal));
            if (btn2x != null) btn2x.onClick.AddListener(() => SetSpeed(GameSpeed.Speed2x));
            if (btn3x != null) btn3x.onClick.AddListener(() => SetSpeed(GameSpeed.Speed3x));

            UpdateButtonVisuals();
        }
    }
}

