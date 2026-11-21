using UnityEngine;

namespace GuilinEye.Core
{
    public enum AudioBusType { Bgm, Sfx, Voice }

    /// <summary>三条总线：BGM / SFX / VOICE，48kHz。模块不得直接操作全局 AudioSource。</summary>
    public class AudioBus : MonoBehaviour
    {
        public static AudioBus Instance { get; private set; }

        readonly AudioSource[] _sources = new AudioSource[3];
        readonly float[] _volumes = { 1f, 1f, 1f };

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            for (var i = 0; i < 3; i++)
            {
                var go = new GameObject("Bus_" + (AudioBusType)i);
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = (i == 0);
                _sources[i] = src;
            }
        }

        public void Play(string audioId, AudioBusType bus, float gain = 1f, float fadeSeconds = 0f)
        {
            var clip = Resources.Load<AudioClip>("Audio/" + audioId);
            if (clip == null)
            {
                Debug.LogWarning("[Audio] 缺少音频 " + audioId + "（总线 " + bus + "）");
                return;
            }
            var src = _sources[(int)bus];
            src.volume = _volumes[(int)bus] * Mathf.Clamp01(gain);
            src.PlayOneShot(clip);
        }

        public void PlayOneShot(AudioClip clip, AudioBusType bus, float gain = 1f)
        {
            if (clip == null) return;
            var src = _sources[(int)bus];
            src.volume = _volumes[(int)bus] * Mathf.Clamp01(gain);
            src.PlayOneShot(clip);
        }

        public void SetBusVolume(AudioBusType bus, float volume)
        {
            _volumes[(int)bus] = Mathf.Clamp01(volume);
            _sources[(int)bus].volume = _volumes[(int)bus];
        }

        public void Duck(AudioBusType bus, float target, float seconds)
        {
            SetBusVolume(bus, target);
        }
    }
}
