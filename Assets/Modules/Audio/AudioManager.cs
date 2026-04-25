using UnityEngine;
using Cysharp.Threading.Tasks;
using Assets.Modules.Audio;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Config")]
    [SerializeField] private AudioConfig _config;

    [Header("Sources")]
    [SerializeField] private AudioSource _musicSource1;
    [SerializeField] private AudioSource _musicSource2;
    [SerializeField] private AudioSource _sfxSource; // Для звуков типа шлепков

    [Header("Settings")]
    [SerializeField] private float fadeDuration = 1.5f;
    private bool _isSource1Active = true;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    // ИГРАТЬ МУЗЫКУ ПО КЛЮЧУ (с плавным переходом)
    public async UniTask PlayMusic(string key)
    {
        AudioClip clip = _config.GetRandomClip(key);
        if (clip == null) return;

        AudioSource active = _isSource1Active ? _musicSource1 : _musicSource2;
        if (active.clip == clip) return; // Уже играет

        AudioSource next = _isSource1Active ? _musicSource2 : _musicSource1;
        next.clip = clip;
        next.Play();

        await Crossfade(active, next);
        _isSource1Active = !_isSource1Active;
    }

    // ИГРАТЬ ЗВУК ПО КЛЮЧУ (мгновенно, один раз)
    public void PlaySFX(string key, float volume = 1f)
    {
        AudioClip clip = _config.GetRandomClip(key);
        if (clip != null)
        {
            _sfxSource.PlayOneShot(clip, volume);
        }
    }

    private async UniTask Crossfade(AudioSource active, AudioSource next)
    {
        float timer = 0;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float percent = timer / fadeDuration;
            active.volume = Mathf.Lerp(1, 0, percent);
            next.volume = Mathf.Lerp(0, 1, percent);
            await UniTask.Yield();
        }
        active.Stop();
        active.volume = 0;
        next.volume = 1;
    }
}