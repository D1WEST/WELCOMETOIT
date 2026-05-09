using Assets.Modules.Audio;
using Assets.Modules.Save;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioConfig _config;

    // Внутренние источники (только для 2D музыки и UI)
    private AudioSource _musicSource1;
    private AudioSource _musicSource2;
    private AudioSource _internalSfxSource2D;

    private bool _isSource1Active = true;
    private CancellationTokenSource _loopCts;

    private HashSet<string> _activeKeys = new HashSet<string>();
    private Dictionary<string, HashSet<AudioSource>> _playingRegistry = new Dictionary<string, HashSet<AudioSource>>();
    private float _music1BaseVol, _music2BaseVol;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SetupInternalSources();
        }
        else Destroy(gameObject);
    }

    private void SetupInternalSources()
    {
        _musicSource1 = gameObject.AddComponent<AudioSource>();
        _musicSource2 = gameObject.AddComponent<AudioSource>();
        _internalSfxSource2D = gameObject.AddComponent<AudioSource>();

        // Музыкальные источники НЕ должны участвовать в 3D логике
        _musicSource1.spatialBlend = _musicSource2.spatialBlend = _internalSfxSource2D.spatialBlend = 0f;
        _musicSource1.playOnAwake = _musicSource2.playOnAwake = false;
    }

    // МЕТОД ОСТАНОВКИ (Безопасный)
    public void StopAudio(AudioQuery query)
    {
        if (string.IsNullOrEmpty(query.Key)) return;

        if (query.WorldAnchor != null)
        {
            // Останавливаем только на конкретном объекте
            if (query.WorldAnchor.TryGetComponent<AudioSource>(out var source))
            {
                // ПРОВЕРКА: Не пытаемся ли мы остановить музыку менеджера?
                if (source == _musicSource1 || source == _musicSource2) return;

                source.Stop();
                source.clip = null; // Очищаем для надежности

                if (_playingRegistry.TryGetValue(query.Key, out var sources))
                    sources.Remove(source);
            }
        }
        else
        {
            // Глобальная остановка ключа
            if (_playingRegistry.TryGetValue(query.Key, out var sources))
            {
                foreach (var source in sources)
                {
                    if (source != null && source != _musicSource1 && source != _musicSource2)
                        source.Stop();
                }
                sources.Clear();
            }
        }
        _activeKeys.Remove(query.Key);
    }

    public async UniTask PlayAudio(AudioQuery query)
    {
        AudioClip clip = ResolveClip(query);
        if (clip == null) return;

        if (query.IsKeyInstance && _activeKeys.Contains(query.Key)) return;

        if (query.IsOneShot)
        {
            PlayManagedSFX(query, clip).Forget();
        }
        else if (query.IsPlaylist)
        {
            _loopCts?.Cancel();
            _loopCts = new CancellationTokenSource();
            PlaylistLoop(query, _loopCts.Token).Forget();
        }
        else
        {
            // Это вызов МУЗЫКИ (Crossfade использует только внутренние _musicSource1/2)
            await PerformCrossfade(clip, query.Volume, query.IsCycling);
        }
    }

    public void UpdateLiveVolume(float globalVolume)
    {
        // Обновляем громкость на лету, умножая базу на ползунок
        _musicSource1.volume = _music1BaseVol * globalVolume;
        _musicSource2.volume = _music2BaseVol * globalVolume;
        _internalSfxSource2D.volume = globalVolume;
    }


    private async UniTaskVoid PlaylistLoop(AudioQuery query, CancellationToken token)
    {
        var relation = _config.FindRelation(query.Key);
        if (relation == null) return;

        int currentIndex = 0;
        while (!token.IsCancellationRequested)
        {
            AudioClip clip = query.IsRandom
                ? relation.clips[Random.Range(0, relation.clips.Count)]
                : relation.clips[currentIndex];

            await PerformCrossfade(clip, query.Volume, false);

            float waitTime = clip.length - 1.5f;
            if (waitTime > 0) await UniTask.Delay((int)(waitTime * 1000), cancellationToken: token);

            currentIndex = (currentIndex + 1) % relation.clips.Count;
            if (!query.IsCycling && !query.IsRandom && currentIndex == 0) break;
        }
    }
    private async UniTaskVoid PlayManagedSFX(AudioQuery query, AudioClip clip)
    {
        _activeKeys.Add(query.Key);

        AudioSource targetSource = _internalSfxSource2D;
        if (query.WorldAnchor != null && query.WorldAnchor.TryGetComponent<AudioSource>(out var ext))
        {
            targetSource = ext;
        }

        // Регистрируем
        if (!_playingRegistry.ContainsKey(query.Key))
            _playingRegistry[query.Key] = new HashSet<AudioSource>();
        _playingRegistry[query.Key].Add(targetSource);

        float globalVol = GameDataManager.Instance.playerSettings.volume;

        if (query.IsCycling)
        {
            targetSource.clip = clip;
            targetSource.loop = true;
            targetSource.volume = query.Volume * globalVol;
            targetSource.Play();
        }
        else
        {
            
            targetSource.PlayOneShot(clip, query.Volume * globalVol);
        }

        if (!query.IsCycling)
        {
            await UniTask.Delay((int)(clip.length * 1000));
            if (targetSource != null && _playingRegistry.TryGetValue(query.Key, out var sources))
                sources.Remove(targetSource);
            _activeKeys.Remove(query.Key);
        }
    }

    private async UniTask PerformCrossfade(AudioClip nextClip, float targetVolume, bool loop)
    {
        AudioSource active = _isSource1Active ? _musicSource1 : _musicSource2;
        AudioSource next = _isSource1Active ? _musicSource2 : _musicSource1;

        if (active.clip == nextClip && active.isPlaying) return;

        float globalVol = GameDataManager.Instance.playerSettings.volume;

        next.clip = nextClip;
        next.loop = loop;
        next.Play();

        float timer = 0;
        float duration = 1.5f;
        while (timer < duration)
        {
            float currentGlobal = GameDataManager.Instance.playerSettings.volume;
            timer += Time.deltaTime;
            float p = timer / duration;
            active.volume = Mathf.Lerp(active.volume, 0, p); // Плавное затухание музыки
            next.volume = Mathf.Lerp(0, targetVolume, p) * currentGlobal;
            await UniTask.Yield();
        }

        active.Stop();
        _isSource1Active = !_isSource1Active;
    }

    private AudioClip ResolveClip(AudioQuery query)
    {
        var relation = _config.FindRelation(query.Key);
        if (relation == null || relation.clips.Count == 0) return null;
        if (query.Index >= 0 && query.Index < relation.clips.Count) return relation.clips[query.Index];
        return relation.clips[Random.Range(0, relation.clips.Count)];
    }
}