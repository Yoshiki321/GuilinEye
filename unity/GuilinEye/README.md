# 桂林之眼 Unity 工程

## 版本

- Unity **6000.6.10f1**（锁定版本，见 `ProjectSettings/ProjectVersion.txt`，不得各模块自行升级）。
- 依赖包：`Newtonsoft.Json`、`UGUI`、本地共享包 `com.guilineye.shared`。

## 首次打开与常用菜单

1. 用 Unity Hub 以 **6000.6.10f1** 打开本目录，等待包解析完成。
2. 菜单 `桂林之眼 → 1. 生成全部模块场景`：生成 18 个模块场景与 4 个组合场景。
3. 菜单 `桂林之眼 → 2. 校验参数源`：检查 `../../config` 合法性。
4. 菜单 `桂林之眼 → 3. 同步配置到 StreamingAssets`：同步 7 份 JSON 到 `Assets/StreamingAssets/config`。
5. 菜单 `桂林之眼 → 6. 构建 Windows 播放器`：默认构建 Release 版（设置 `GUILINEYE_DEV_BUILD=1` 时切为 Development）。

## 播放器命令行参数

- `GuilinEye.exe -all`：装配全馆 18 模块
- `GuilinEye.exe -onlyArea B,D`：仅装配指定分区
- `GuilinEye.exe -onlyModule B01`：仅装配单个模块
- 环境变量：`VENUE_API_URL`（控幕与 H5 拉取地址）、`VENUE_OPS_TOKEN`（运维/控幕鉴权令牌）

## 参数源与模块边界

- 运行期 Editor 读 `../../config`，Player 读 `StreamingAssets/config`，**唯一事实源是 `config/`**。
- `Core/`：配置、时钟、事件、画布映射、镜面、音频、控幕。
- `Hardware/`：传感器接口、Hub、Mock、Femto Bolt / 雷达 / 红外适配器与 OSC/TUIO 接收器。
- `Installations/`：`InstallationBase`、`ModuleKit`、`VenueBootstrap` 与 4 个组合场景。
- `Integration/`：BP-01—BP-05 与 `H5FeedPoller`。
- `Modules/<ID>/`：18 个展项控制器与独立场景。
