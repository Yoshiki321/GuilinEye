# ORBBEC Femto Bolt SDK（现场硬件对应）

本目录是桂林之眼现场 3 台 **ORBBEC Femto Bolt** 的 SDK 落盘与接入记录。
现场硬件表：体感互动投影机 → 奥比中光 ORBBEC Femto Bolt ×3（S1/S2/S3）。

## 已下载文件

| 文件 | 版本 | 大小 | SHA-256 | 来源 |
|---|---|---|---|---|
| `OrbbecSDK_Unity_v1.1.9_20231123_c23d106e_release.zip` | Unity SDK v1.1.9 | 52,263,554 B | `82aa16757bc40ad9fadeecab5b7693683609ab33e42fcaa499a07623ca7517f1` | `https://github.com/orbbec/OrbbecUnitySDK/releases/tag/v1.1.9` |
| `OrbbecSDK_K4A_Wrapper_v2.0.12_windows_202605090937.zip` | K4A Wrapper v2.0.12 | 5,189,465 B | `b476d989bf2954f1b33e2cdb9e21c0f9e8ba483c5e407fb4f3b6caf17d88ad46` | `https://github.com/orbbec/OrbbecSDK-K4A-Wrapper/releases/tag/v2.0.12` |

> K4A Wrapper v2.0.12 的 SHA-256 来自 GitHub Release API 的 `digest` 字段，下载后已本地复核一致。
> Unity SDK v1.1.9 的 Release API 没有 digest 字段，下载后只做了 zip 完整性检查；版本与文件大小已核对。

## 解压内容

- `k4a_wrapper_v2.0.12/`
  - `bin/k4a.dll`、`OrbbecSDK.dll`、`depthengine_2_0.dll`、`k4aviewer.exe`
  - `include/k4a/k4a.h`、`lib/k4a.lib`
  - 官方 `README.md`：Femto Bolt 最低固件 1.1.2，推荐 1.1.3
- `unity_sdk_v1.1.9/`
  - `SDK/OrbbecSDK_v1.1.9_20231123_c23d106e.unitypackage`
  - `Examples/OrbbecSDK_Sample_win64.zip`
  - `README.md`

## 已接入 Unity 工程的文件

只把运行必需的 3 个库复制到工程，没有整包导入示例场景：

- `Assets/Plugins/Orbbec/ob_csharp.dll`（C# API）
- `Assets/Plugins/Orbbec/x86_64/OrbbecSDK.dll`（原生 SDK）
- `Assets/Plugins/Orbbec/x86_64/depthengine_2_0.dll`（深度引擎）

`FemtoBoltProvider.cs` 使用 `Orbbec` 命名空间的 `Context / DeviceList / Pipeline / Config / Frameset / DepthFrame`，启动 640×576@30 Y16 深度流，输出：

- `presence`：有效深度点超过阈值
- `point`：深度质心映射到画布 mm（约 10 Hz）
- `gesture`：S1/S2 的深度质心左右往复启发式（≥2 次换向、1.2 s 窗口、1.2 s 冷却）
- `leave`：450 ms 无有效深度点

> 这不是 32 关节骨骼。真正的多人骨架需要 Azure Kinect Body Tracking SDK 1.1.2（MSI 约 1.6 GB）配合 K4A Wrapper；Orbbec 官方 FAQ 明确不承诺支持 Microsoft Body Tracking SDK，属于现场兼容/社区路径。未实测前不在代码里伪造骨骼或身份。

## 官方文档 / API

- Femto Bolt 文档：`https://doc.orbbec.com/documentation/Orbbec%20Femto%20Bolt%20Documentation`
- Orbbec SDK 总入口：`https://www.orbbec.com/developers/orbbec-sdk/`
- K4A Wrapper 文档：`https://orbbec.github.io/OrbbecSDK-K4A-Wrapper/`
- K4A Wrapper 接入 AKDK 应用说明：`k4a_wrapper_v2.0.12/doc/Access_AKDK_Application_Software_with_Femto_Bolt.pdf`
- Unity SDK 仓库：`https://github.com/orbbec/OrbbecUnitySDK`

## 现场上电顺序（建议）

1. 先只接 1 台 Femto Bolt，USB-C 数据线直连服务器 USB 3.0 口，确认设备管理器出现 ORBBEC 设备。
2. 运行 `k4a_wrapper_v2.0.12/bin/k4aviewer.exe`，确认 depth/color 能出图；再用 Unity 播放器确认 `[S1] Femto Bolt 在线`。
3. 三台全部接好后，用 `hardware.json` 的 `serial` 字段按 SN 绑定 S1/S2/S3；不填则按枚举顺序 0/1/2，现场必须核对。
4. 若使用 USB 光纤传输器，确认其 USB 3.0 速率与供电；Femto Bolt 是 USB-C 单线供电+数据，光纤方案必须支持供电回传。

## 许可

- OrbbecSDK-K4A-Wrapper：MIT License
- OrbbecUnitySDK：Apache License 2.0
- 再分发时请保留各自仓库的 LICENSE 文件与版权声明。
