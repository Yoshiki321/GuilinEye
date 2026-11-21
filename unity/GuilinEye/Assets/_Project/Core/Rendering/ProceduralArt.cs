using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuilinEye.Core
{
    /// <summary>
    /// 无美术资产时的程序化画面：按模块/层名生成 256² 纹理，保证 18 个展项各自可辨识。
    /// 正式美术接入后，ContentSlot 可替换同名层材质，不改控制器。
    /// </summary>
    public static class ProceduralArt
    {
        const int Size = 256;
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        public static string PatternFor(string moduleId, string layer)
        {
            switch (layer)
            {
                case "象": return "mountain";
                case "候": return "season";
                case "迎客": return "glow";
                case "拱": return "arch";
                case "月": return "moon";
                case "古人": return "figure";
                case "作揖": return "glow";
                case "卷": return "scroll";
                case "入卷": return "ink";
                case "海底": return "waves";
                case "抬升": return "mountain";
                case "溶蚀": return "stone";
                case "天坑": return "rings";
                case "万年后": return "glow";
                case "长卷": return "scroll";
                case "名称牌": return "glow";
                case "石刻": return "stone";
                case "对局": return "grid";
                case "题名": return "script";
                case "诗壁": return "script";
                case "落字": return "glow";
                case "灵象": return "elephant";
                case "积分": return "stars";
                case "云": return "clouds";
                case "江": return "river";
                case "雨": return "rain";
                case "河": return "river";
                case "河灯": return "lantern";
                case "纹样": return "pattern";
                case "印章": return "seal";
                case "春": return "spring";
                case "夏": return "summer";
                case "秋": return "autumn";
                case "冬": return "winter";
                case "涟漪": return "rings";
                case "花境": return "flowers";
                case "生花": return "petals";
                case "月影": return "ripple";
                case "江面": return "river";
                case "水线": return "waves";
                case "蝶道": return "butterfly";
                case "起蝶": return "butterflyGlow";
                default: return "gradient";
            }
        }

        public static Texture2D Create(string pattern, int seed, Color color, bool baseLayer)
        {
            var key = pattern + "|" + seed + "|" + ColorKey(color) + "|" + (baseLayer ? 1 : 0);
            Texture2D cached;
            if (Cache.TryGetValue(key, out cached) && cached != null) return cached;

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.name = "PA_" + pattern + "_" + seed;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[Size * Size];
            var rng = new System.Random(seed * 7919 + pattern.GetHashCode());
            var dark = Color.Lerp(color, Color.black, 0.55f);
            var light = Color.Lerp(color, Color.white, 0.55f);
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var u = x / (float)(Size - 1);
                    var v = y / (float)(Size - 1);
                    var c = baseLayer
                        ? Color.Lerp(dark, color, 0.55f + 0.25f * v)
                        : new Color(color.r, color.g, color.b, 0f);
                    if (baseLayer) c.a = 1f;
                    c = Sample(pattern, u, v, c, dark, light, color, rng);
                    pixels[y * Size + x] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            Cache[key] = tex;
            return tex;
        }

        static string ColorKey(Color c) { return ((int)(c.r * 255)) + "_" + ((int)(c.g * 255)) + "_" + ((int)(c.b * 255)); }

        static Color Add(Color baseColor, Color layer)
        {
            var a = Mathf.Clamp01(layer.a);
            return new Color(
                baseColor.r * (1f - a) + layer.r * a,
                baseColor.g * (1f - a) + layer.g * a,
                baseColor.b * (1f - a) + layer.b * a,
                Mathf.Clamp01(baseColor.a + a * (1f - baseColor.a)));
        }

        static Color Sample(string p, float u, float v, Color c, Color dark, Color light, Color color, System.Random rng)
        {
            switch (p)
            {
                case "gradient":
                    return Add(c, new Color(light.r, light.g, light.b, 0.08f * (0.5f + 0.5f * Mathf.Sin(u * 6.28f))));
                case "glow":
                {
                    var d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.52f));
                    var a = Mathf.Clamp01(1f - d / 0.45f);
                    return Add(c, new Color(light.r, light.g, light.b, a * a * 0.55f));
                }
                case "rings":
                {
                    var d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f));
                    var ring = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(d * 95f), 6f);
                    return Add(c, new Color(light.r, light.g, light.b, ring * 0.55f));
                }
                case "ripple":
                {
                    var d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.42f));
                    var ring = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(d * 70f - 1.2f), 10f);
                    return Add(c, new Color(0.92f, 0.94f, 0.88f, ring * 0.45f));
                }
                case "mountain":
                {
                    var ridge = 0.28f + 0.10f * Mathf.Sin(u * 7f + 0.5f) + 0.05f * Mathf.Sin(u * 19f + 1.3f);
                    if (v > ridge)
                    {
                        var k = Mathf.Clamp01((v - ridge) / 0.7f);
                        return Add(c, new Color(dark.r, dark.g, dark.b, 0.45f + 0.3f * k));
                    }
                    var haze = Mathf.Clamp01(0.25f + 0.55f * v);
                    return Add(c, new Color(light.r, light.g, light.b, 0.10f * haze));
                }
                case "arch":
                {
                    var dx = u - 0.5f;
                    var dy = v;
                    var r = Mathf.Sqrt(dx * dx + dy * dy);
                    var band = Mathf.Abs(r - 0.46f);
                    if (band < 0.055f && dy > 0f)
                        return Add(c, new Color(light.r, light.g, light.b, 0.8f * (1f - band / 0.055f)));
                    if (r < 0.41f && dy > 0f)
                        return new Color(c.r * 0.25f, c.g * 0.25f, c.b * 0.25f, 1f);
                    return c;
                }
                case "moon":
                {
                    var d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.56f));
                    if (d < 0.239f)
                    {
                        var k = Mathf.Clamp01(1f - d / 0.239f);
                        return Add(c, new Color(0.99f, 0.95f, 0.78f, 0.92f - 0.12f * k));
                    }
                    var halo = Mathf.Clamp01(1f - (d - 0.239f) / 0.18f);
                    return Add(c, new Color(0.95f, 0.92f, 0.75f, halo * halo * 0.14f));
                }
                case "stars":
                {
                    var cell = 0.055f;
                    var cx = Mathf.Floor(u / cell);
                    var cy = Mathf.Floor(v / cell);
                    var h = Hash((int)cx, (int)cy, 13);
                    if (h > 0.72f)
                    {
                        var px = (cx + 0.5f + 0.35f * Mathf.Sin(h * 99f)) * cell;
                        var py = (cy + 0.5f + 0.35f * Mathf.Cos(h * 77f)) * cell;
                        var d = Vector2.Distance(new Vector2(u, v), new Vector2(px, py));
                        var a = Mathf.Clamp01(1f - d / 0.025f);
                        return Add(c, new Color(1f, 0.92f, 0.66f, a * 0.9f));
                    }
                    return c;
                }
                case "waves":
                {
                    var w = 0.5f + 0.5f * Mathf.Sin((v * 18f + Mathf.Sin(u * 8f) * 1.6f) * Mathf.PI);
                    return Add(c, new Color(light.r, light.g, light.b, 0.08f + 0.12f * w));
                }
                case "river":
                {
                    var w = 0.5f + 0.5f * Mathf.Sin((v * 10f + u * 4f) * Mathf.PI);
                    var glint = Mathf.Pow(w, 14f);
                    return Add(c, new Color(0.86f, 0.96f, 0.98f, 0.06f + 0.22f * glint));
                }
                case "rain":
                {
                    var streak = Mathf.Pow(0.5f + 0.5f * Mathf.Sin((u * 18f + v * 40f) * Mathf.PI), 18f);
                    return Add(c, new Color(0.86f, 0.92f, 0.98f, streak * 0.35f));
                }
                case "clouds":
                {
                    var n = Mathf.PerlinNoise(u * 4f + 3.1f, v * 2.5f + 0.7f);
                    var a = Mathf.Clamp01((n - 0.42f) * 1.6f);
                    return Add(c, new Color(0.92f, 0.94f, 0.95f, a * 0.30f));
                }
                case "petals":
                {
                    for (var i = 0; i < 24; i++)
                    {
                        var px = Hash(i, 1, 31) * 1f;
                        var py = Hash(i, 2, 37) * 1f;
                        var rx = 0.012f + Hash(i, 3, 41) * 0.018f;
                        var ry = rx * 0.55f;
                        var dx = (u - px) / rx;
                        var dy = (v - py) / ry;
                        if (dx * dx + dy * dy < 1f)
                            return Add(c, new Color(1f, 0.80f, 0.78f, 0.75f));
                    }
                    return c;
                }
                case "flowers":
                {
                    for (var i = 0; i < 9; i++)
                    {
                        var cx = Hash(i, 4, 43) * 0.9f + 0.05f;
                        var cy = Hash(i, 5, 47) * 0.9f + 0.05f;
                        var ang = Mathf.Atan2(v - cy, u - cx);
                        var d = Vector2.Distance(new Vector2(u, v), new Vector2(cx, cy));
                        var petal = 0.05f + 0.035f * Mathf.Abs(Mathf.Sin(ang * 2.5f));
                        if (d < petal)
                            return Add(c, new Color(1f, 0.72f, 0.72f, 0.72f * (1f - d / petal)));
                    }
                    return c;
                }
                case "grid":
                {
                    var lx = Mathf.Abs(u - Mathf.Round(u * 6f) / 6f);
                    var ly = Mathf.Abs(v - Mathf.Round(v * 3f) / 3f);
                    var line = Mathf.Min(lx, ly);
                    if (line < 0.012f) return Add(c, new Color(light.r, light.g, light.b, 0.45f));
                    return c;
                }
                case "stone":
                {
                    var n = Mathf.PerlinNoise(u * 9f + 1.1f, v * 7f + 2.2f);
                    var crack = Mathf.Pow(0.5f + 0.5f * Mathf.Sin((u * 13f + n * 4f) * Mathf.PI), 24f);
                    return Add(c, new Color(n, n, n, 0.08f + 0.20f * crack));
                }
                case "scroll":
                {
                    var edge = Mathf.Clamp01(Mathf.Min(u, 1f - u) / 0.08f);
                    var col = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(u * 18f * Mathf.PI), 30f);
                    return Add(c, new Color(0.96f, 0.90f, 0.74f, 0.10f * edge + 0.10f * col));
                }
                case "script":
                {
                    var cell = 0.06f;
                    var cx = Mathf.Floor(u / cell);
                    var cy = Mathf.Floor(v / cell);
                    var h = Hash((int)cx, (int)cy, 53);
                    var lx = u - cx * cell;
                    var ly = v - cy * cell;
                    if (h > 0.55f && lx > 0.01f && lx < 0.045f && ly > 0.01f && ly < 0.05f)
                        return Add(c, new Color(1f, 0.95f, 0.82f, 0.45f + 0.45f * h));
                    return c;
                }
                case "figure":
                {
                    var head = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.70f));
                    var body = Mathf.Abs(u - 0.5f) < 0.17f && v < 0.62f && v > 0.18f;
                    if (head < 0.09f || body)
                        return Add(c, new Color(0.90f, 0.92f, 0.80f, 0.35f));
                    return c;
                }
                case "elephant":
                {
                    var body = ((u - 0.48f) * (u - 0.48f)) / (0.26f * 0.26f) + ((v - 0.45f) * (v - 0.45f)) / (0.19f * 0.19f);
                    var head = ((u - 0.70f) * (u - 0.70f)) / (0.13f * 0.13f) + ((v - 0.58f) * (v - 0.58f)) / (0.13f * 0.13f);
                    if (body < 1f || head < 1f)
                        return Add(c, new Color(0.96f, 0.82f, 0.62f, 0.62f));
                    // trunk
                    var tx = 0.76f + 0.10f * Mathf.Sin((0.58f - v) * 8f);
                    if (v < 0.58f && v > 0.18f && Mathf.Abs(u - tx) < 0.035f)
                        return Add(c, new Color(0.96f, 0.82f, 0.62f, 0.55f));
                    return c;
                }
                case "butterfly":
                {
                    for (var i = 0; i < 12; i++)
                    {
                        var px = 0.12f + Hash(i, 6, 59) * 0.76f;
                        var py = 0.12f + Hash(i, 7, 61) * 0.76f;
                        var dx = Mathf.Abs(u - px);
                        var dy = v - py;
                        var wing = dx < 0.035f + 0.015f * Mathf.Sin(dy * 40f) && Mathf.Abs(dy) < 0.05f;
                        if (wing)
                            return Add(c, new Color(1f, 0.88f, 0.66f, 0.65f));
                    }
                    return c;
                }
                case "butterflyGlow":
                {
                    var d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f));
                    var a = Mathf.Clamp01(1f - d / 0.55f);
                    return Add(c, new Color(1f, 0.90f, 0.68f, a * a * 0.35f));
                }
                case "season":
                {
                    var s = Mathf.Clamp((int)(u * 4f), 0, 3);
                    var quarter = new Color[] {
                        new Color(0.42f, 0.68f, 0.40f),
                        new Color(0.22f, 0.55f, 0.36f),
                        new Color(0.85f, 0.58f, 0.24f),
                        new Color(0.62f, 0.72f, 0.78f)
                    }[s];
                    return Add(c, new Color(quarter.r, quarter.g, quarter.b, 0.28f));
                }
                case "spring": return Add(c, new Color(0.50f, 0.82f, 0.46f, 0.30f));
                case "summer": return Add(c, new Color(0.18f, 0.62f, 0.40f, 0.30f));
                case "autumn": return Add(c, new Color(0.92f, 0.58f, 0.22f, 0.30f));
                case "winter": return Add(c, new Color(0.68f, 0.80f, 0.88f, 0.30f));
                case "ink":
                {
                    var n = Mathf.PerlinNoise(u * 3f + 9f, v * 4f + 4f);
                    return Add(c, new Color(0.05f, 0.08f, 0.08f, Mathf.Clamp01((n - 0.4f) * 0.8f)));
                }
                case "lantern":
                {
                    for (var i = 0; i < 7; i++)
                    {
                        var px = 0.18f + i * 0.10f;
                        var py = 0.25f + 0.18f * Mathf.Sin(i * 2.1f);
                        var d = Vector2.Distance(new Vector2(u, v), new Vector2(px, py));
                        var a = Mathf.Clamp01(1f - d / 0.05f);
                        if (a > 0) return Add(c, new Color(1f, 0.78f, 0.42f, a * 0.8f));
                    }
                    return c;
                }
                case "pattern":
                {
                    var n = Mathf.Abs(Mathf.Sin((u + v) * 18f) * Mathf.Sin((u - v) * 13f));
                    return Add(c, new Color(light.r, light.g, light.b, 0.12f * n));
                }
                case "seal":
                {
                    if (u > 0.30f && u < 0.70f && v > 0.30f && v < 0.70f)
                    {
                        var border = Mathf.Min(Mathf.Min(u - 0.30f, 0.70f - u), Mathf.Min(v - 0.30f, 0.70f - v));
                        if (border < 0.035f) return Add(c, new Color(0.98f, 0.92f, 0.78f, 0.85f));
                        return Add(c, new Color(0.70f, 0.16f, 0.14f, 0.85f));
                    }
                    return c;
                }
                case "market":
                {
                    for (var i = 0; i < 6; i++)
                    {
                        var x0 = 0.06f + i * 0.15f;
                        if (u > x0 && u < x0 + 0.10f && v < 0.32f)
                            return Add(c, new Color(0.90f, 0.62f, 0.34f, 0.18f));
                    }
                    return c;
                }
                default:
                    return Add(c, new Color(light.r, light.g, light.b, 0.06f));
            }
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                var h = x * 374761393 + y * 668265263 + seed * 2147483647;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0x7fffffff) / 2147483647f;
            }
        }
    }
}
