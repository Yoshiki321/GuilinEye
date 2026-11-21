# GuilinEye｜仓库骨架（预置，未初始化 Unity）

本目录按 `01_总体架构与目录.md` 的目标结构预置，**只有目录与占位文件**，Unity 工程尚未初始化。

## 00 公共底座开工时要做的第一件事

1. 用冻结后的 Unity 版本创建/补齐 `unity/GuilinEye`（`ProjectSettings/`、`Packages/manifest.json` 以 Unity 生成的真实文件为准，不要手写伪造）。
2. 保留 `Assets/_Project/Modules/` 下 18 个编号目录：`A01 A02 A03 A04 B01 B02 B03 B04 C01 C02 C03 C04 C05 D01 D02 D03 D04 D05`。
3. 先落 `contracts/` 的事件与传感 Schema、`config/canvas/` 的画布参数，再让模块开工。

## 规则

- 不提交 `Library/`、`Temp/`、`Logs/`、`obj/`、`Build/`。
- 不提交商业素材、字体、密钥、证书、真实访客数据。
- 模块只改自己的 `Modules/<id>/`；底座、契约、安装组合、共享素材由集成负责人合并。
