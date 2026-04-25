using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Modules.Audio
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] private AudioConfig _config;
        private AudioSource _musicSource1, _musicSource2;
        private bool _isSource1Active = true;
        private CancellationTokenSource _loopCts;

        private void Awake()
        {
            if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); SetupSources(); }
            else Destroy(gameObject);
        }

        private void SetupSources()
        {
            _musicSource1 = gameObject.AddComponent<AudioSource>();
            _musicSource2 = gameObject.AddComponent<AudioSource>();
            _musicSource1.playOnAwake = _musicSource2.playOnAwake = false;
            _musicSource1.spatialBlend = _musicSource2.spatialBlend = 0f; // 2D звук
        }

        public async UniTask PlayAudio(AudioQuery query)
        {
            // Останавливаем любые запущенные циклы/плейлисты
            _loopCts?.Cancel();
            _loopCts?.Dispose();
            _loopCts = new CancellationTokenSource();

            if (query.IsPlaylist)
            {
                // ЗАПУСК ПЛЕЙЛИСТА
                PlaylistLoop(query, _loopCts.Token).Forget();
            }
            else
            {
                // ОДИНОЧНЫЙ ТРЕК
                AudioClip clip = ResolveClip(query);
                if (clip != null)
                {
                    // Если Cycle включен для одного трека, используем встроенный loop
                    bool useInternalLoop = query.IsCycling;
                    await PerformCrossfade(clip, query.Volume, useInternalLoop);
                }
            }
        }

        private async UniTaskVoid PlaylistLoop(AudioQuery query, CancellationToken token)
        {
            var relation = _config.FindRelation(query.Key);
            if (relation == null || relation.clips.Count == 0) return;

            int currentIndex = 0;

            while (!token.IsCancellationRequested)
            {
                // Выбираем клип
                AudioClip clip;
                if (query.IsRandom)
                {
                    clip = relation.clips[Random.Range(0, relation.clips.Count)];
                }
                else
                {
                    clip = relation.clips[currentIndex];
                    currentIndex = (currentIndex + 1) % relation.clips.Count;
                }

                // Плавная смена трека
                await PerformCrossfade(clip, query.Volume, false);

                // Ждем завершения (за вычетом времени кроссфейда 1.5 сек)
                float waitTime = clip.length - 1.5f;
                if (waitTime > 0)
                    await UniTask.Delay((int)(waitTime * 1000), cancellationToken: token);

                // Если Cycle выключен и мы дошли до конца последовательного списка - выходим
                if (!query.IsCycling && !query.IsRandom && currentIndex == 0) break;
            }
        }

        private async UniTask PerformCrossfade(AudioClip nextClip, float targetVolume, bool loop)
        {
            AudioSource active = _isSource1Active ? _musicSource1 : _musicSource2;
            AudioSource next = _isSource1Active ? _musicSource2 : _musicSource1;

            if (active.clip == nextClip && active.isPlaying) return;

            next.clip = nextClip;
            next.loop = loop; // Устанавливаем зацикливание
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

        private AudioClip ResolveClip(AudioQuery query)
        {
            var relation = _config.FindRelation(query.Key);
            if (relation == null || relation.clips.Count == 0) return null;
            if (query.Index >= 0 && query.Index < relation.clips.Count) return relation.clips[query.Index];
            return relation.clips[Random.Range(0, relation.clips.Count)];
        }

        // Привязка к Listener (как раньше)
        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            AudioListener listener = FindFirstObjectByType<AudioListener>();
            if (listener != null) transform.SetParent(listener.transform);
        }
    }
}