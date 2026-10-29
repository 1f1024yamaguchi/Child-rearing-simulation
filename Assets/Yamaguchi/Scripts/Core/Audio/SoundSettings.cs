using System;
using UnityEngine;

/// <summary>
/// SE・BGM の音量設定（0〜1）を保持し、PlayerPrefs に保存・読み込みする静的クラス。
/// 端末ごとの設定のため SaveSystem には含めない。
/// </summary>
public static class SoundSettings
{
    public const string SeVolumeKey = "sound_se";
    public const string BgmVolumeKey = "sound_bgm";

    public const float DefaultSeVolume = 0.8f;
    public const float DefaultBgmVolume = 0.5f;

    private static float seVolume = DefaultSeVolume;
    private static float bgmVolume = DefaultBgmVolume;
    private static bool isLoaded;

    /// <summary>音量が変更されたときに発火するイベント。</summary>
    public static event Action OnChanged;

    /// <summary>現在の SE 音量（0〜1）。</summary>
    public static float SeVolume
    {
        get
        {
            EnsureLoaded();
            return seVolume;
        }
    }

    /// <summary>現在の BGM 音量（0〜1）。</summary>
    public static float BgmVolume
    {
        get
        {
            EnsureLoaded();
            return bgmVolume;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        isLoaded = false;
        OnChanged = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Load()
    {
        seVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SeVolumeKey, DefaultSeVolume));
        bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, DefaultBgmVolume));
        isLoaded = true;
    }

    private static void EnsureLoaded()
    {
        if (!isLoaded)
        {
            Load();
        }
    }

    /// <summary>SE 音量を 0〜1 にクランプして保存し、OnChanged を通知する。</summary>
    public static void SetSeVolume(float value)
    {
        EnsureLoaded();
        seVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SeVolumeKey, seVolume);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    /// <summary>BGM 音量を 0〜1 にクランプして保存し、OnChanged を通知する。</summary>
    public static void SetBgmVolume(float value)
    {
        EnsureLoaded();
        bgmVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(BgmVolumeKey, bgmVolume);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }
}

