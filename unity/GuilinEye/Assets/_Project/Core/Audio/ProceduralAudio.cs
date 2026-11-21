using System.Collections.Generic;
using UnityEngine;

namespace GuilinEye.Core
{
    /// <summary>程序化提示音：无音频资产时，交互也有可听反馈。正式音效由 AudioCuePlayer 替换。</summary>
    public static class ProceduralAudio
    {
        const int Rate = 44100;
        static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        public static AudioClip Get(string kind)
        {
            AudioClip clip;
            if (Cache.TryGetValue(kind, out clip) && clip != null) return clip;
            switch (kind)
            {
                case "water": clip = Noise("water", 1.2f, 0.16f, 900f); break;
                case "step": clip = Noise("step", 0.18f, 0.30f, 240f); break;
                case "stone": clip = Noise("stone", 0.28f, 0.24f, 420f); break;
                case "bell": clip = Tone("bell", 1.6f, 660f, 0.18f, true); break;
                case "chime": clip = Tone("chime", 1.1f, 880f, 0.20f, false); break;
                case "low": clip = Tone("low", 1.4f, 220f, 0.16f, false); break;
                default: clip = Tone("cue", 0.6f, 520f, 0.14f, false); break;
            }
            Cache[kind] = clip;
            return clip;
        }

        static AudioClip Tone(string name, float seconds, float freq, float amp, bool harmonic)
        {
            var n = Mathf.Max(1, (int)(seconds * Rate));
            var data = new float[n];
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)Rate;
                var env = Mathf.Exp(-t * 3.2f) * Mathf.Clamp01(t * 40f);
                var s = Mathf.Sin(2f * Mathf.PI * freq * t);
                if (harmonic) s += 0.35f * Mathf.Sin(2f * Mathf.PI * freq * 2.01f * t);
                data[i] = s * env * amp;
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Noise(string name, float seconds, float amp, float cutoff)
        {
            var n = Mathf.Max(1, (int)(seconds * Rate));
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            var lp = 0f;
            var k = Mathf.Clamp01(cutoff / Rate * 6.28f);
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)Rate;
                var env = Mathf.Exp(-t * 5f) * Mathf.Clamp01(t * 60f);
                var white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (white - lp) * k;
                data[i] = lp * env * amp;
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static string CueFor(string action)
        {
            if (string.IsNullOrEmpty(action)) return "cue";
            if (action.Contains("step") || action.Contains("swipe") || action.Contains("jump")) return "step";
            if (action.Contains("water") || action.Contains("river") || action.Contains("rain")) return "water";
            if (action.Contains("write") || action.Contains("stone") || action.Contains("duel")) return "stone";
            if (action.Contains("greet") || action.Contains("wish") || action.Contains("seal")) return "bell";
            if (action.Contains("gesture") || action.Contains("touch")) return "chime";
            return "cue";
        }
    }
}
