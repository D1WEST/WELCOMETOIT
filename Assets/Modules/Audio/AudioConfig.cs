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

    [CreateAssetMenu(fileName = "AudioConfig", menuName = "Audio/Config")]
    public class AudioConfig : ScriptableObject
    {
        public List<AudioRelation> relations;

        public AudioRelation FindRelation(string key) => relations.Find(r => r.key == key);
    }
}