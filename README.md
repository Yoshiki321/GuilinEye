# GuilinEye｜桂林之眼展馆互动工程

「桂林之眼」18 个展项 + 1 个公共底座的统一仓库，包含 Unity 工程、单一参数源、H5 四端、本地 `venue-api` 服务及校验/探针工具。

## 当前状态（hw19）

- **Unity 版本**：`6000.6.10f1`（见 `unity/GuilinEye/ProjectSettings/ProjectVersion.txt`）
- **场景规模**：22 个场景（18 个模块场景 `A01`—`D05` + 4 个分区组合场景 `Entrance` / `Education` / `Family` / `Hall`）
- **单一参数源**：`config/` 下 7 份 JSON（`canvas`、`quality`、`sensors`、`schedule`、`installations`、`art-slots`、`hardware`），与 `unity/GuilinEye/Assets/StreamingAssets/config/` 保持逐字节一致
- **软件状态**：标准 OSC/TUIO 1.1 二进制解包（含空 `alive` 与 `source` 分桶）、H5→`venue-api`→Unity 上墙闭环、Release 构建与 Gate 校验均已完成，**具备进入现场真机联调条件**；真实 Femto Bolt、北阳雷达、红外与 EHE V1.0 播控待现场接入标定

## 目录结构

- `config/`：全馆唯一参数源（7 份 JSON）
- `unity/GuilinEye/`：唯一 Unity 工程（`Core` / `Hardware` / `Installations` / `Integration` / `Modules/A01…D05` / `Editor`）
- `services/venue-api/`：零依赖 Python 本地服务（`src/server.py` + `tests/test_api.py`）
- `web/h5/`：H5 四端入口（`quiz` / `lantern` / `draw` / `pickup`）
- `tools/`：参数校验（`validate_config.py`）与 TUIO 二进制探针（`tuio_binary_probe.py`、`tuio_b01_leave_probe.py`）
- `docs/decisions/`：播控分工（D-001）、设备基线（D-002）、单一参数源（D-003）

## 本地自检命令

```bash
python3 tools/validate_config.py
python3 services/venue-api/tests/test_api.py
```

## 维护规则

- 不提交 `Library/`、`Temp/`、`Log/`、`Logs/`、`obj/`、`Build/`、`UserSettings/`、`*.db` 等运行期产物。
- 不提交商业素材、字体、密钥、证书、真实访客数据。
- 模块只改各自的 `Modules/<ID>/`；底座、契约、组合场景与共享服务由集成负责人统一合并。
