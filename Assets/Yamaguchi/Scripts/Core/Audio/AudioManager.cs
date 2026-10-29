using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SE と BGM の再生を担当するシングルトン（DontDestroyOnLoad）。
/// SoundSettings の音量設定に従って再生・音量更新を行う。
/// BGM は複数クリップのランダム連続再生（1曲終わるごとに次の曲をランダム選択）に対応する。
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }
    public static AudioManager I => Instance;

    [Header("AudioSource（未設定なら Awake で自動追加）")]
    [Tooltip("効果音（SE）再生用の AudioSource。未指定の場合は Awake で自動追加されます。")]
    [SerializeField] private AudioSource seSource;

    [Tooltip("BGM 再生用の AudioSource。未指定の場合は Awake で自動追加されます。")]
    [SerializeField] private AudioSource bgmSource;

    [Header("既定の音源")]
    [Tooltip("ボタンを押したときに鳴らす既定のクリック効果音。")]
    [SerializeField] private AudioClip defaultClickClip;

    [Header("BGM ランダム再生")]
    [Tooltip("ランダム連続再生する BGM の一覧。1曲終わるごとに次の曲をランダムに選びます（2曲以上ある場合は直前と同じ曲を避けます）。")]
    [SerializeField] private List<AudioClip> bgmClips = new List<AudioClip>();

    [Tooltip("起動時（Start）に bgmClips のランダム連続再生を自動で開始するか。")]
    [SerializeField] private bool playRandomBgmOnStart = true;

    /// <summary>既定のクリック効果音。</summary>
    public AudioClip DefaultClickClip => defaultClickClip;

    private bool isRandomBgmMode;
    private AudioClip currentBgmClip;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (transform.parent != null)
        {
            transform.SetParent(null);
        }
        DontDestroyOnLoad(gameObject);

        if (seSource == null)
        {
            seSource = gameObject.AddComponent<AudioSource>();
            seSource.playOnAwake = false;
        }

        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
        }

        bgmSource.volume = SoundSettings.BgmVolume;

        SoundSettings.OnChanged += HandleSoundSettingsChanged;
    }

    private void Start()
    {
        if (playRandomBgmOnStart && HasAnyValidBgmClip())
        {
            StartRandomBgm();
        }
    }

    private void Update()
    {
        if (!isRandomBgmMode || bgmSource == null) return;

        // バックグラウンド停止中や AudioListener 一時停止中は曲送りしない
        if (!Application.isFocused && !Application.runInBackground) return;
        if (AudioListener.pause) return;

        if (!bgmSource.isPlaying)
        {
            PlayNextRandomBgm();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        SoundSettings.OnChanged -= HandleSoundSettingsChanged;
    }

    private void HandleSoundSettingsChanged()
    {
        if (bgmSource != null)
        {
            bgmSource.volume = SoundSettings.BgmVolume;
        }
    }

    /// <summary>
    /// SE 用 AudioSource で PlayOneShot を呼ぶ。音量は SoundSettings.SeVolume。
    /// clip が null の場合は defaultClickClip を使用する。
    /// </summary>
    public void PlaySe(AudioClip clip)
    {
        AudioClip target = clip != null ? clip : defaultClickClip;
        if (target == null || seSource == null) return;

        seSource.PlayOneShot(target, SoundSettings.SeVolume);
    }

    /// <summary>
    /// 指定した単一 BGM をループ再生する。音量は SoundSettings.BgmVolume。
    /// （これを呼ぶとランダム連続再生モードは解除され、指定曲の単曲ループになる）
    /// </summary>
    public void PlayBgm(AudioClip clip)
    {
        isRandomBgmMode = false;
        if (bgmSource == null) return;

        if (clip == null)
        {
            bgmSource.Stop();
            bgmSource.clip = null;
            currentBgmClip = null;
            return;
        }

        currentBgmClip = clip;
        bgmSource.clip = clip;
        bgmSource.loop = true;
        bgmSource.volume = SoundSettings.BgmVolume;
        bgmSource.Play();
    }

    /// <summary>
    /// bgmClips に登録された BGM のランダム連続再生を開始する。
    /// 1曲終わるごとに自動で次の曲がランダムに選ばれる。
    /// </summary>
    public void StartRandomBgm()
    {
        if (!HasAnyValidBgmClip()) return;

        isRandomBgmMode = true;
        PlayNextRandomBgm();
    }

    /// <summary>BGM の再生を停止する。</summary>
    public void StopBgm()
    {
        isRandomBgmMode = false;
        currentBgmClip = null;
        if (bgmSource != null)
        {
            bgmSource.Stop();
            bgmSource.clip = null;
        }
    }

    private void PlayNextRandomBgm()
    {
        if (bgmSource == null) return;

        AudioClip next = PickRandomBgmClip();
        if (next == null)
        {
            isRandomBgmMode = false;
            return;
        }

        currentBgmClip = next;
        bgmSource.clip = next;
        bgmSource.loop = false; // 曲が終わったことを検知するため loop は false にする
        bgmSource.volume = SoundSettings.BgmVolume;
        bgmSource.Play();
    }

    private bool HasAnyValidBgmClip()
    {
        if (bgmClips == null) return false;
        for (int i = 0; i < bgmClips.Count; i++)
        {
            if (bgmClips[i] != null) return true;
        }
        return false;
    }

    private AudioClip PickRandomBgmClip()
    {
        if (bgmClips == null || bgmClips.Count == 0) return null;

        // null でない有効なクリップだけを集める
        List<AudioClip> validClips = new List<AudioClip>(bgmClips.Count);
        for (int i = 0; i < bgmClips.Count; i++)
        {
            if (bgmClips[i] != null)
            {
                validClips.Add(bgmClips[i]);
            }
        }

        if (validClips.Count == 0) return null;
        if (validClips.Count == 1) return validClips[0];

        // 2曲以上ある場合は、直前に流れていた曲以外からランダムに選ぶ
        List<AudioClip> candidates = new List<AudioClip>(validClips.Count);
        for (int i = 0; i < validClips.Count; i++)
        {
            if (validClips[i] != currentBgmClip)
            {
                candidates.Add(validClips[i]);
            }
        }

        if (candidates.Count == 0)
        {
            return validClips[Random.Range(0, validClips.Count)];
        }

        return candidates[Random.Range(0, candidates.Count)];
    }
}
