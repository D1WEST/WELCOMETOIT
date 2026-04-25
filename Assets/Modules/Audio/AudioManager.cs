using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Assets.Modules.Audio
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] private AudioConfig _config;

        // Внутренние источники для 2D (Музыка и Интерфейс)
        private AudioSource _musicSource1;
        private AudioSource _musicSource2;
        private AudioSource _internalSfxSource2D;

        private bool _isSource1Active = true;
        private CancellationTokenSource _loopCts;

        // Реестры для контроля повторов (Instances)
        private HashSet<string> _activeKeys = new HashSet<string>();
        private HashSet<AudioClip> _activeClips = new HashSet<AudioClip>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                SetupInternalSources();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void SetupInternalSources()
        {
            _musicSource1 = gameObject.AddComponent<AudioSource>();
            _musicSource2 = gameObject.AddComponent<AudioSource>();
            _internalSfxSource2D = gameObject.AddComponent<AudioSource>();

            // Эти источники всегда 2D (Spatial Blend = 0)
            _musicSource1.playOnAwake = _musicSource2.playOnAwake = _internalSfxSource2D.playOnAwake = false;
            _musicSource1.spatialBlend = _musicSource2.spatialBlend = _internalSfxSource2D.spatialBlend = 0f;
            _musicSource1.loop = _musicSource2.loop = true;
        }

        public async UniTask PlayAudio(AudioQuery query)
        {
            AudioClip clip = ResolveClip(query);
            if (clip == null) return;

            // Контроль инстансов (чтобы звуки не накладывались в кашу)
            if (query.IsKeyInstance && _activeKeys.Contains(query.Key)) return;
            if (query.IsClipInstance && _activeClips.Contains(clip)) return;

            if (query.IsOneShot)
            {
                PlayManagedSFX(query, clip).Forget();
            }
            else if (query.IsPlaylist)
            {
                _loopCts?.Cancel();
                _loopCts?.Dispose();
                _loopCts = new CancellationTokenSource();
                PlaylistLoop(query, _loopCts.Token).Forget();
            }
            else
            {
                // Обычная музыка/эмбиент с кроссфейдом
                await PerformCrossfade(clip, query.Volume, query.IsCycling);
            }
        }

        private async UniTaskVoid PlayManagedSFX(AudioQuery query, AudioClip clip)
        {
            _activeKeys.Add(query.Key);
            _activeClips.Add(clip);

            AudioSource targetSource = _internalSfxSource2D;

            // ЛОГИКА 3D ПОЗИЦИОНИРОВАНИЯ
            if (query.WorldAnchor != null)
            {
                // Ищем AudioSource на целевом объекте
                if (query.WorldAnchor.TryGetComponent<AudioSource>(out var externalSource))
                {
                    // Принудительно проверяем, что он в режиме 3D
                    if (externalSource.spatialBlend < 1f) externalSource.spatialBlend = 1f;
                    targetSource = externalSource;
                }
                else
                {
                    // Если AudioSource на объекте не найден, звук будет 2D.
                    // Можно добавить логику динамического добавления, но лучше просто логировать ошибку
                    Debug.LogWarning($"[Audio] На объекте {query.WorldAnchor.name} нет AudioSource! Звук будет 2D.");
                }
            }

            // Играем звук
            if (targetSource != null)
            {
                targetSource.PlayOneShot(clip, query.Volume);
            }

            // Ждем, пока клип доиграет, чтобы убрать его из реестра активных
            await UniTask.Delay((int)(clip.length * 1000));

            _activeKeys.Remove(query.Key);
            _activeClips.Remove(clip);
        }

        private async UniTask PerformCrossfade(AudioClip nextClip, float targetVolume, bool loop)
        {
            AudioSource active = _isSource1Active ? _musicSource1 : _musicSource2;
            AudioSource next = _isSource1Active ? _musicSource2 : _musicSource1;

            if (active.clip == nextClip && active.isPlaying) return;

            next.clip = nextClip;
            next.loop = loop;
            next.Play();

            float timer = 0;
            float duration = 1.5f;
            float startActiveVol = active.volume;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float p = timer / duration;

                active.volume = Mathf.Lerp(startActiveVol, 0, p);
                next.volume = Mathf.Lerp(0, targetVolume, p);

                await UniTask.Yield();
            }

            active.Stop();
            next.volume = targetVolume;
            _isSource1Active = !_isSource1Active;
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

        private AudioClip ResolveClip(AudioQuery query)
        {
            var relation = _config.FindRelation(query.Key);
            if (relation == null || relation.clips.Count == 0) return null;

            if (query.Index >= 0 && query.Index < relation.clips.Count) return relation.clips[query.Index];
            return relation.clips[Random.Range(0, relation.clips.Count)];
        }

        public void StopImmediately()
        {
            _loopCts?.Cancel();
            _musicSource1.Stop(); _musicSource2.Stop(); _internalSfxSource2D.Stop();
            _activeKeys.Clear(); _activeClips.Clear();
        }
    }
}