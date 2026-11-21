using System;
using System.Collections;
using UnityEngine;

namespace GuilinEye.Shared
{
    /// <summary>SP-13 单模块音频：淡入淡出、duck、循环；不直接改全局 AudioBus。</summary>
    public class AudioCuePlayer : MonoBehaviour
    {
        public float volume = 1f;

        AudioSource _source;
        Coroutine _fade;

        public AudioSource Source
        {
            get
            {
                if (_source == null)
                {
                    _source = GetComponent<AudioSource>();
                    if (_source == null) _source = gameObject.AddComponent<AudioSource>();
                    _source.playOnAwake = false;
                }
                return _source;
            }
        }

        public bool IsPlaying { get { return _source != null && _source.isPlaying; } }

        void Awake() { _source = Source; }

        public void Play(AudioClip clip, float gain = 1f, float fadeInSeconds = 0f, bool loop = false)
        {
            if (clip == null)
            {
                LogRouter.Warn("Audio", name + " 缺少 AudioClip");
                return;
            }
            StopImmediate();
            Source.clip = clip;
            Source.loop = loop;
            Source.volume = fadeInSeconds > 0.001f ? 0f : Mathf.Clamp01(volume * gain);
            Source.Play();
            if (fadeInSeconds > 0.001f) FadeTo(Mathf.Clamp01(volume * gain), fadeInSeconds, false);
        }

        public void Stop(float fadeOutSeconds = 0f)
        {
            if (_source == null) return;
            if (fadeOutSeconds <= 0.001f) StopImmediate();
            else FadeTo(0f, fadeOutSeconds, true);
        }

        void StopImmediate()
        {
            if (_fade != null) { StopCoroutine(_fade); _fade = null; }
            if (_source != null) _source.Stop();
        }

        public void Duck(float target, float seconds)
        {
            FadeTo(Mathf.Clamp01(target), seconds, false);
        }

        public void SetVolume(float value)
        {
            volume = Mathf.Clamp01(value);
            if (_source != null) _source.volume = volume;
        }

        void FadeTo(float target, float duration, bool stopAtEnd)
        {
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(FadeRoutine(target, duration, stopAtEnd));
        }

        IEnumerator FadeRoutine(float target, float duration, bool stopAtEnd)
        {
            var from = _source != null ? _source.volume : 0f;
            var t = 0f;
            while (t < duration && _source != null)
            {
                t += Time.unscaledDeltaTime;
                _source.volume = Mathf.Lerp(from, target, Mathf.Clamp01(t / duration));
                yield return null;
            }
            if (_source != null)
            {
                _source.volume = target;
                if (stopAtEnd) _source.Stop();
            }
            _fade = null;
        }

        void OnDisable() { StopImmediate(); }
    }
}
