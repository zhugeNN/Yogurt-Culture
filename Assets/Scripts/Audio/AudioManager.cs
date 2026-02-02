// AudioManager.cs
using UnityEngine;
using System.Collections.Generic;

public class AudioManager : Singleton<AudioManager>
{

    [Header("音频源")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private Transform sfxPoolParent;

    [Header("音量控制")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 1f;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Header("音效配置")]
    [SerializeField] private SFXDefinition sfxDefinition;
    [Header("默认背景音乐")]
    [SerializeField] private AudioClip defaultBGM;

    private Dictionary<string, AudioClip> sfxClipMap;
    private Queue<AudioSource> sfxPool;
    private List<AudioSource> activeSfxSources;

    protected override void Awake()
    {
        base.Awake();
        DontDestroyOnLoad(gameObject);

        InitializeSFXPool(10); // 初始池大小，可根据项目调整
        BuildSFXClipMap();

        // 播放默认背景音乐（如果有设置）
        if (defaultBGM != null)
        {
            PlayMusicImmediate(defaultBGM, true, 2f); // 使用2秒淡入，避免过长的等待
        }
    }

    void InitializeSFXPool(int poolSize)
    {
        sfxPool = new Queue<AudioSource>();
        activeSfxSources = new List<AudioSource>();

        for (int i = 0; i < poolSize; i++)
        {
            CreateNewPooledSource();
        }
    }

    AudioSource CreateNewPooledSource()
    {
        GameObject obj = new GameObject($"SFXSource_{sfxPool.Count}");
        obj.transform.SetParent(sfxPoolParent);
        AudioSource source = obj.AddComponent<AudioSource>();
        source.playOnAwake = false;
        sfxPool.Enqueue(source);
        return source;
    }

    void BuildSFXClipMap()
    {
        sfxClipMap = new Dictionary<string, AudioClip>();
        foreach (var sfx in sfxDefinition.clips)
        {
            if (!sfxClipMap.ContainsKey(sfx.key))
                sfxClipMap.Add(sfx.key, sfx.clip);
        }
    }

    // ============ 公开API ============
    public void PlayMusic(AudioClip clip, bool loop = true, float fadeDuration = 0.5f)
    {
        if (clip == null || musicSource == null)
        {
            Debug.LogWarning("无法播放音乐：音频剪辑或音频源为空");
            return;
        }

        // 如果当前正在播放，先淡出
        if (musicSource.isPlaying)
        {
            StartCoroutine(FadeOutMusicThenPlayNew(clip, loop, fadeDuration));
        }
        else
        {
            // 直接播放新音乐
            PlayMusicImmediate(clip, loop, fadeDuration);
        }
    }

    /// <summary>
    /// 立即播放音乐（用于初始化）
    /// </summary>
    public void PlayMusicImmediate(AudioClip clip, bool loop = true, float fadeDuration = 0f)
    {
        if (clip == null || musicSource == null)
        {
            Debug.LogWarning("无法播放音乐：音频剪辑或音频源为空");
            return;
        }

        musicSource.clip = clip;
        musicSource.loop = loop;

        if (fadeDuration > 0f)
        {
            musicSource.volume = 0f;
            StartCoroutine(FadeInMusic(fadeDuration));
        }
        else
        {
            musicSource.volume = musicVolume * masterVolume;
        }

        musicSource.Play();
    }

    public void PlaySFX(string key)
    {
        if (!sfxClipMap.ContainsKey(key))
        {
            Debug.LogWarning($"音效键 '{key}' 不存在!");
            return;
        }

        AudioSource source = GetAvailableSFXSource();
        if (source == null) return;

        SFXDefinition.SFXClip config = GetSFXConfig(key);
        source.clip = sfxClipMap[key];
        source.volume = config.volume * sfxVolume * masterVolume;
        source.pitch = config.pitch;
        source.loop = config.loop;
        source.Play();

        if (!config.loop)
            StartCoroutine(ReturnToPoolAfterPlay(source, source.clip.length));
    }

    public void StopAllSFX()
    {
        foreach (var source in activeSfxSources)
        {
            source.Stop();
            sfxPool.Enqueue(source);
        }
        activeSfxSources.Clear();
    }

    // ============ 内部辅助方法 ============
    private AudioSource GetAvailableSFXSource()
    {
        if (sfxPool.Count > 0)
        {
            AudioSource source = sfxPool.Dequeue();
            activeSfxSources.Add(source);
            return source;
        }

        // 池为空则动态扩容
        AudioSource newSource = CreateNewPooledSource();
        activeSfxSources.Add(newSource);
        return sfxPool.Dequeue();
    }

    private SFXDefinition.SFXClip GetSFXConfig(string key)
    {
        foreach (var sfx in sfxDefinition.clips)
            if (sfx.key == key) return sfx;
        return new SFXDefinition.SFXClip { volume = 1f, pitch = 1f };
    }

    private System.Collections.IEnumerator ReturnToPoolAfterPlay(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (source != null && !source.loop)
        {
            source.Stop();
            activeSfxSources.Remove(source);
            sfxPool.Enqueue(source);
        }
    }

    private System.Collections.IEnumerator FadeOutMusic(float duration)
    {
        if (musicSource == null || !musicSource.isPlaying)
            yield break;

        float startVolume = musicSource.volume;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;
            musicSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        musicSource.volume = 0f;
        musicSource.Stop();
    }

    private System.Collections.IEnumerator FadeInMusic(float duration)
    {
        if (musicSource == null || musicSource.clip == null)
            yield break;

        float targetVolume = musicVolume * masterVolume;
        musicSource.volume = 0f;

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;
            musicSource.volume = Mathf.Lerp(0f, targetVolume, t);
            yield return null;
        }

        musicSource.volume = targetVolume;
    }

    private System.Collections.IEnumerator FadeOutMusicThenPlayNew(AudioClip newClip, bool loop, float fadeDuration)
    {
        // 先淡出当前音乐
        yield return StartCoroutine(FadeOutMusic(fadeDuration));

        // 然后播放新音乐
        PlayMusicImmediate(newClip, loop, fadeDuration);
    }
}