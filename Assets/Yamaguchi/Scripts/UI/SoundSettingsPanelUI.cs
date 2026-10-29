using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// SE・BGM 音量の設定パネルの表示を担当するスクリプト。
/// パネルを開いている間は GameClock.I.PushPause() でゲーム内時間を一時停止し、
/// 閉じたときに PopPause() で解除する。
/// </summary>
public class SoundSettingsPanelUI : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("設定パネル全体のルート GameObject（Open/Close で SetActive を切り替える対象）。")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("SE 音量を調整するスライダー（Min 0 / Max 1）。")]
    [SerializeField] private Slider seSlider;

    [Tooltip("BGM 音量を調整するスライダー（Min 0 / Max 1）。")]
    [SerializeField] private Slider bgmSlider;

    [Tooltip("設定パネルを閉じるボタン。")]
    [SerializeField] private Button closeButton;

    private SeSliderPointerUpHandler sePointerUpHandler;
    private bool isPaused;

    /// <summary>設定パネルを開く。</summary>
    public void Open()
    {
        if (panelRoot != null)
        {
            bool wasActive = panelRoot.activeSelf;
            panelRoot.SetActive(true);
            // このスクリプトが panelRoot とは別の常時アクティブなオブジェクトに付いている場合も、開くたびに最新値を反映する
            if (wasActive || gameObject != panelRoot)
            {
                ApplyAndSubscribe();
            }
        }
        else
        {
            gameObject.SetActive(true);
        }

        RequestPause();
    }

    /// <summary>設定パネルを閉じる。</summary>
    public void Close()
    {
        ReleasePause();

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        // panelRoot が別オブジェクトで初期非表示のときに親だけ OnEnable された場合は停止しない
        if (panelRoot == null || panelRoot == gameObject || panelRoot.activeSelf)
        {
            RequestPause();
        }

        ApplyAndSubscribe();
    }

    private void OnDisable()
    {
        ReleasePause();
        Unsubscribe();
    }

    private void RequestPause()
    {
        if (isPaused) return;
        if (GameClock.I != null)
        {
            GameClock.I.PushPause();
            isPaused = true;
        }
    }

    private void ReleasePause()
    {
        if (!isPaused) return;
        isPaused = false;
        if (GameClock.I != null)
        {
            GameClock.I.PopPause();
        }
    }

    private void ApplyAndSubscribe()
    {
        Unsubscribe();

        if (seSlider != null)
        {
            // 反映時に通知が飛ばないよう、購読前に現在値を入れる
            seSlider.value = SoundSettings.SeVolume;
            seSlider.onValueChanged.AddListener(OnSeSliderValueChanged);

            sePointerUpHandler = seSlider.GetComponent<SeSliderPointerUpHandler>();
            if (sePointerUpHandler == null)
            {
                sePointerUpHandler = seSlider.gameObject.AddComponent<SeSliderPointerUpHandler>();
            }
            sePointerUpHandler.OnPointerReleased += OnSeSliderPointerUp;
        }

        if (bgmSlider != null)
        {
            // 反映時に通知が飛ばないよう、購読前に現在値を入れる
            bgmSlider.value = SoundSettings.BgmVolume;
            bgmSlider.onValueChanged.AddListener(OnBgmSliderValueChanged);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }
    }

    private void Unsubscribe()
    {
        if (seSlider != null)
        {
            seSlider.onValueChanged.RemoveListener(OnSeSliderValueChanged);
        }

        if (sePointerUpHandler != null)
        {
            sePointerUpHandler.OnPointerReleased -= OnSeSliderPointerUp;
        }

        if (bgmSlider != null)
        {
            bgmSlider.onValueChanged.RemoveListener(OnBgmSliderValueChanged);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }
    }

    private void OnSeSliderValueChanged(float value)
    {
        SoundSettings.SetSeVolume(value);
    }

    private void OnBgmSliderValueChanged(float value)
    {
        SoundSettings.SetBgmVolume(value);
    }

    private void OnSeSliderPointerUp()
    {
        if (AudioManager.I == null) return;
        AudioManager.I.PlaySe(AudioManager.I.DefaultClickClip);
    }
}

/// <summary>
/// SE スライダーのつまみを離したタイミング（OnPointerUp）を検知して通知する補助コンポーネント。
/// </summary>
[DisallowMultipleComponent]
public class SeSliderPointerUpHandler : MonoBehaviour, IPointerUpHandler
{
    public event Action OnPointerReleased;

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;
        OnPointerReleased?.Invoke();
    }
}

