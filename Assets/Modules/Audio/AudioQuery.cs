using UnityEngine;

public class AudioQuery
{
    public string Key { get; private set; }
    public int Index { get; private set; } = -1;
    public bool IsRandom { get; private set; } = true;
    public float Volume { get; private set; } = 1f;
    public bool IsPlaylist { get; private set; } = false;
    public bool IsCycling { get; private set; } = false;
    public bool IsKeyInstance { get; private set; } = false;
    public bool IsClipInstance { get; private set; } = false;
    public bool IsOneShot { get; private set; } = false;
    public Transform WorldAnchor { get; private set; }

    private AudioQuery(string key) => Key = key;
    public static AudioQuery ByKey(string key) => new AudioQuery(key);

    public AudioQuery ByIndex(int index) { Index = index; IsRandom = false; return this; }
    public AudioQuery WithVolume(float volume) { Volume = Mathf.Clamp01(volume); return this; }
    public AudioQuery AsPlaylist() { IsPlaylist = true; IsRandom = false; return this; }
    public AudioQuery AsRandomPlaylist() { IsPlaylist = true; IsRandom = true; return this; }
    public AudioQuery Cycle() { IsCycling = true; return this; }
    public AudioQuery RandomSound() { IsOneShot = true; IsRandom = true; return this; }
    public AudioQuery AsInstance() { IsClipInstance = true; IsOneShot = true; return this; }
    public AudioQuery AsKeyInstance() { IsKeyInstance = true; IsOneShot = true; return this; }

    // Указываем объект, с чьего AudioSource играть звук
    public AudioQuery At(Transform target) { WorldAnchor = target; return this; }
}