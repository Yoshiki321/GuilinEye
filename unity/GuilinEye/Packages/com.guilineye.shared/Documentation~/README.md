# com.guilineye.shared v1.0

桂林之眼 18 个展项的共用插件包。只提供机制，不写展项玩法。

## 安装

包已嵌入工程：`Packages/com.guilineye.shared`。Unity 打开工程后自动识别，不需要改 `manifest.json`。

`Assets/_Project/GuilinEye.Runtime.asmdef` 必须引用 `GuilinEye.Shared`：

```json
"references": ["GuilinEye.Shared"]
```

## 插件清单

| ID | 类 | 文件 |
|---|---|---|
| SP-01 | `ServiceRegistry` | `Runtime/Core/Services.cs` |
| SP-02 | `ModuleStateMachine` | `Runtime/Core/Services.cs` |
| SP-03 | `Timeline` | `Runtime/Core/Timeline.cs` |
| SP-04 | `ScenarioRunner` | `Runtime/Core/ScenarioRunner.cs` |
| SP-05 | `SignalBus` | `Runtime/Core/SignalBus.cs` |
| SP-06 | `CooldownGate` / `DebounceGate` / `HysteresisGate` | `Runtime/Interaction/Gates.cs` |
| SP-07 | `InteractionRouter` / `SequenceDetector` / `DwellTracker` / `OccupancyCounter` | `Runtime/Interaction/InteractionKit.cs` |
| SP-08 | `SurfaceFader` / `SurfaceSet` | `Runtime/Rendering/Surfaces.cs` |
| SP-09 | `BrightnessGuard` / `MirrorCopy` | `Runtime/Rendering/Brightness.cs` |
| SP-10 | `ProjectionCalibration` | `Runtime/Rendering/Calibration.cs` |
| SP-11 | `ContentSlotRegistry` | `Runtime/Content/ContentSlot.cs` |
| SP-12 | `LayoutMath` | `Runtime/Rendering/Surfaces.cs` |
| SP-13 | `AudioCuePlayer` | `Runtime/Audio/AudioKit.cs` |
| SP-14 | `JsonStore` / `ConfigFingerprint` | `Runtime/Persistence/JsonStore.cs` |
| SP-15 | `JsonHttpClient` / `TelemetryBuffer` | `Runtime/Net/JsonHttpClient.cs` |
| SP-16 | `SafeAreaFitter` / `Billboard` | `Runtime/UI/UiKit.cs` |
| SP-17 | `ObjectPool` / `RingBuffer` | `Runtime/Utilities/Collections.cs` |
| SP-18 | `Easing` / `RandomScope` | `Runtime/Utilities/MathKit.cs` |
| SP-19 | `FrameBudgetMonitor` | `Runtime/Core/Diagnostics.cs` |
| SP-20 | `LogRouter` / `HealthProbe` | `Runtime/Core/Diagnostics.cs` |

## 最小用法

```csharp
using GuilinEye.Shared;

var fsm = new ModuleStateMachine("D03");
fsm.TryTransition(ModuleState.Active);

var tl = new Timeline(new[] {
    new TimelineSegment { id = "moonrise", start = 0, dur = 180 }
}, cycle: 180, crossfade: 3f);

var gate = new CooldownGate();
if (gate.TryPass("D03.step", 0.3f, Time.realtimeSinceStartup)) { /* ... */ }

var fader = gameObject.AddComponent<SurfaceFader>();
fader.FadeTo(1f, 3f);
```

## 边界

- 不依赖 `GuilinEye.Core`、`GuilinEye.Hardware`、`GuilinEye.Modules`。
- 不读 `config/*.json`；配置适配由 L1/L2 负责。
- 不改全局 `RenderSettings`；亮度只作用于传入的 Renderer。
- 所有 HTTP 必须带超时、重试上限、幂等键；所有写盘必须走 `JsonStore`。
- 共享包版本变更必须更新 `CHANGELOG.md`；破坏性变更升主版本。

## 版本

- `1.0.0`：首版，SP-01—SP-20。
