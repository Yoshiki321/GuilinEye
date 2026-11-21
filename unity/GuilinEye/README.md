# 桂林之眼 Unity 工程

## 版本

- Unity **6000.3.0f1**（建议 Unity 6.3 LTS；冻结后锁定，不得各模块自行升级）。
- 包：Newtonsoft.Json、UGUI、Audio、Video；不额外引入商业插件。

## 首次打开

1. 用 Unity Hub 以 6000.3.0f1 打开本目录。
2. 等待包解析完成（首次会自动生成 Library）。
3. 菜单 `桂林之眼 → 1. 生成全部模块场景`：生成 18 个模块场景与 4 个组合场景。
4. 菜单 `桂林之眼 → 2. 校验参数源`：检查 `../../config` 是否合法。
5. 打开 `Assets/_Project/Installations/Hall.unity` 可看大厅五段。

## 参数源

运行期从 `仓库/GuilinEye/config` 读取；打包时由 `桂林之眼 → 3. 同步配置到 StreamingAssets` 复制到 `Assets/StreamingAssets/config`。
**唯一事实源是 config，不在场景里改数字。**

## 模块边界

- `Core/`：配置、时钟、事件、画布映射、镜面、音频、控幕、启动。
- `Hardware/`：传感器接口、Hub、Mock、Femto/雷达/红外适配器。
- `Installations/`：模块基类与组合场景。
- `Modules/<ID>/`：18 个模块，各自 Scenes/Scripts/Configs/Prefabs/ArtSlots/Tests。
- `Editor/`：场景生成与校验工具。

## 状态标记

每个模块运行时在左上角显示：`模拟通过` / `现场待验证` / `已确认`。没有真实设备与现场联调，不得标“已确认”。
