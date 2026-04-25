using UnityEngine;

namespace Assets.Modules.Audio
{
    public class AudioQuery
    {
        public string Key { get; private set; }
        public int Index { get; private set; } = -1;
        public bool IsRandom { get; private set; } = true;
        public float Volume { get; private set; } = 1f;

        // Новые флаги
        public bool IsPlaylist { get; private set; } = false;
        public bool IsCycling { get; private set; } = false;

        private AudioQuery(string key) => Key = key;

        public static AudioQuery ByKey(string key) => new AudioQuery(key);

        public AudioQuery ByIndex(int index) { Index = index; IsRandom = false; return this; }

        public AudioQuery WithVolume(float volume) { Volume = Mathf.Clamp01(volume); return this; }

        // Включает режим последовательного плейлиста (0, 1, 2...)
        public AudioQuery AsPlaylist() { IsPlaylist = true; IsRandom = false; return this; }

        // Включает режим случайного плейлиста
        public AudioQuery AsRandomPlaylist() { IsPlaylist = true; IsRandom = true; return this; }

        // Зацикливает текущий запрос (один трек или весь плейлист)
        public AudioQuery Cycle() { IsCycling = true; return this; }
    }
}