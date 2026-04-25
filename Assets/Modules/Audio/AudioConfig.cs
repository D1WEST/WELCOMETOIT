using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Modules.Audio
{
    [Serializable]
    public class AudioRelation
    {
        public string key;
        public List<AudioClip> clips;
    }

    [CreateAssetMenu(fileName = "AudioConfig", menuName = "Audio/Audio Config")]
    public class AudioConfig : ScriptableObject
    {
        public List<AudioRelation> relations;

        // Вспомогательный метод для быстрого поиска
        public AudioClip GetRandomClip(string key)
        {
            var relation = relations.Find(r => r.key == key);
            if (relation != null && relation.clips.Count > 0)
            {
                return relation.clips[UnityEngine.Random.Range(0, relation.clips.Count)];
            }
            Debug.LogWarning($"[AudioConfig] Ключ '{key}' не найден или пуст!");
            return null;
        }
    }
}