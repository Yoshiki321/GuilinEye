using System;
using UnityEngine;

namespace GuilinEye.Shared
{
    /// <summary>SP-18 统一缓动；避免每个模块各写一套曲线。</summary>
    public static class Easing
    {
        public static float Linear(float t) { return Mathf.Clamp01(t); }

        public static float InQuad(float t) { t = Mathf.Clamp01(t); return t * t; }

        public static float OutQuad(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }

        public static float InOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        public static float OutCubic(float t) { t = Mathf.Clamp01(t); return 1f - Mathf.Pow(1f - t, 3f); }

        public static float OutBack(float t, float overshoot = 1.70158f)
        {
            t = Mathf.Clamp01(t);
            var c = overshoot + 1f;
            return 1f + c * Mathf.Pow(t - 1f, 3f) + overshoot * Mathf.Pow(t - 1f, 2f);
        }

        public static float SmoothStep(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    }

    /// <summary>SP-18 可复现随机；同一 seed 的序列可重放，便于测试。</summary>
    public sealed class RandomScope
    {
        System.Random _random;

        public RandomScope(int seed = 1) { Reset(seed); }

        public void Reset(int seed) { _random = new System.Random(seed); }

        public float Value { get { return (float)_random.NextDouble(); } }

        public float Range(float min, float max)
        {
            if (max <= min) return min;
            return min + (max - min) * Value;
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return _random.Next(minInclusive, maxExclusive);
        }

        public bool Chance(float probability) { return Value < Mathf.Clamp01(probability); }

        public Vector2 PointInCircle(float radius)
        {
            var angle = Range(0f, Mathf.PI * 2f);
            var r = radius * Mathf.Sqrt(Value);
            return new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
        }

        public T Pick<T>(T[] items)
        {
            if (items == null || items.Length == 0) return default(T);
            return items[Range(0, items.Length)];
        }
    }
}
