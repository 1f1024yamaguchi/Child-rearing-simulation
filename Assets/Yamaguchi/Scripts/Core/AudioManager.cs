using UnityEngine;

/// <summary>
/// BGM/SEの音量を管理する小物。
/// スライダーの OnValueChanged に SetBgmVolume / SetSeVolume をつなぐだけで使える。
/// どこからでも AudioManager.I で参照可能。
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager I { get; private set; }

    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource seSource;

    [Range(0f, 1f)] [SerializeField] private float bgmVolume = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float seVolume = 1f;

    private const string BgmKey = "vol_bgm";
    private const string SeKey = "vol_se";

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }
        I = this;
        DontDestroyOnLoad(gameObject);

        // 保存済み音量を復元
        bgmVolume = PlayerPrefs.GetFloat(BgmKey, bgmVolume);
        seVolume = PlayerPrefs.GetFloat(SeKey, seVolume);
        ApplyVolumes();
    }

    public void SetBgmVolume(float value)
    {
        bgmVolume = Mathf.Clamp01(value);
        if (bgmSource != null) bgmSource.volume = bgmVolume;
        PlayerPrefs.SetFloat(BgmKey, bgmVolume);
    }

    public void SetSeVolume(float value)
    {
        seVolume = Mathf.Clamp01(value);
        if (seSource != null) seSource.volume = seVolume;
        PlayerPrefs.SetFloat(SeKey, seVolume);
    }

    public void PlaySe(AudioClip clip)
    {
        if (clip != null && seSource != null) seSource.PlayOneShot(clip, seVolume);
    }

    private void ApplyVolumes()
    {
        if (bgmSource != null) bgmSource.volume = bgmVolume;
        if (seSource != null) seSource.volume = seVolume;
    }
}
