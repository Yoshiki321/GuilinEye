using System;
using System.Collections.Generic;

namespace GuilinEye.Core
{
    [Serializable]
    public class RectMM
    {
        public float x, y, w, h;

        public bool Contains(float px, float py)
        {
            return px >= x && px <= x + w && py >= y && py <= y + h;
        }

        public override string ToString() { return string.Format("({0},{1},{2},{3})", x, y, w, h); }
    }

    [Serializable]
    public class BlendSide
    {
        public float width, from, to;
    }

    [Serializable]
    public class BlendConfig
    {
        public BlendSide left, right;
    }

    [Serializable]
    public class ChannelConfig
    {
        public string id, projector, model, tier, note;
        public RectMM rect;
        public int[] output;
        public BlendConfig blend;
    }

    [Serializable]
    public class RegionConfig
    {
        public string id, note;
        public RectMM rect;
    }

    [Serializable]
    public class CanvasConfig
    {
        public string id, area, name, origin, note;
        public float width, height;
        public bool flipX, isVirtual;
        public float brightness = 1f;
        public ChannelConfig[] channels;
        public RegionConfig[] regions;
    }

    [Serializable]
    public class CanvasFile
    {
        public string version, units, origin, source;
        public CanvasConfig[] canvases;
    }

    [Serializable]
    public class QualityTier
    {
        public string area, post, note;
        public int[] @internal;
        public int[] output;
        public int fps, particlesMax;
    }

    [Serializable]
    public class BrightnessConfig
    {
        public float normal = 1f, D03 = 0.6f, D05 = 1f;
        public float[] mirrorAttenuation;
    }

    [Serializable]
    public class ResponseConfig
    {
        public float footRipple, a03Greet, radarHover;
    }

    [Serializable]
    public class QualityFile
    {
        public string version, basis;
        public Dictionary<string, QualityTier> tiers;
        public Dictionary<string, int> segmentParticleCaps;
        public BrightnessConfig brightness;
        public ResponseConfig response;
    }

    [Serializable]
    public class SurfaceConfig
    {
        public string canvas;
        public RectMM rect;
        public int z;
    }

    [Serializable]
    public class InteractionConfig
    {
        public string type, sensor, zone, gesture, action, branch, route, secondHover, allNine;
        public float response, hold, hover, cardHold, spread, lineLife, budHeight, entryDissolve, exitDissolve, cooldown, ringRadius;
        public int count, maxConcurrent, wishMaxChars, dailyMax, sealHold, bestOf, collapseEvery, collapseDur, residualMax, fallSeconds, warnBeforeEnd, standRing;
        public string[] zones, presets, patterns, colors, sizes, stages;
        public float[] clipSeconds;
        public bool queue, hourlyRelease, jumpBurst, breakMoon, interrupt, resumePhase;
        public ThreeTogetherConfig threeTogether;
    }

    [Serializable]
    public class ThreeTogetherConfig
    {
        public float hold, riseFraction, window;
    }

    [Serializable]
    public class ModuleConfig
    {
        public string id, name, area, tier, show, artSlotPrefix, note;
        public int phase, particlesMax;
        public SurfaceConfig[] surfaces;
        public InteractionConfig[] interactions;
    }

    [Serializable]
    public class InstallationsFile
    {
        public string version;
        public ModuleConfig[] modules;
    }

    [Serializable]
    public class SegmentConfig
    {
        public string id;
        public float start, dur;
    }

    [Serializable]
    public class WindowConfig
    {
        public float start, dur;
    }

    [Serializable]
    public class ShowConfig
    {
        public string id, name;
        public float cycle, action, crossfade, intermission, atSecond, period;
        public SegmentConfig[] segments;
        public WindowConfig window;
        public SegmentConfig[] sequence;
        public string note;
    }

    [Serializable]
    public class ScheduleFile
    {
        public string version, note;
        public ShowConfig[] shows;
    }

    [Serializable]
    public class ZoneConfig
    {
        public string id, name, canvas, sensor, action;
        public RectMM rect;
    }

    [Serializable]
    public class SensorCapability
    {
        public string device, note, zone, conflict;
        public string[] supports;
    }

    [Serializable]
    public class SensorFile
    {
        public string version, source;
        public Dictionary<string, SensorCapability> capabilities;
        public ZoneConfig[] zones;
    }

    [Serializable]
    public class ArtSlotConfig
    {
        public string elementId, module, scene, name, type, position, behavior, condition, asset, source, priority, slot;
    }

    [Serializable]
    public class ArtSlotsFile
    {
        public string version, source;
        public int total;
        public Dictionary<string, int> perModule;
        public ArtSlotConfig[] elements;
    }
}
