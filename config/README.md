# config｜全馆唯一参数源

本目录是《桂林之眼》工程参数的唯一事实源，依据 `docs/decisions/D-003_单一参数源.md`。
提示词、项目卡、代码、美术规格都必须引用这里的键，不得各自写死数字。

| 文件 | 内容 |
|---|---|
| `canvas.json` | 11 个画布、24 条投影通道、区域与重叠 |
| `installations.json` | 18 个模块的画面矩形、时间轴、交互、粒子上限 |
| `schedule.json` | ④ 720 s 总轴与各模块循环 |
| `sensors.json` | S1—S3 / R1—R2 / 红外能力、触点区 T1—T8、地面九块 |
| `quality.json` | 画质档 T1—T4、分辨率、帧率、粒子上限、亮度 |
| `art-slots.json` | 91 个画面元素 → 模块 → slot |

## 约定

- 单位全部是 mm；每个画布以净幅左下角为原点，x 右、y 上。
- 模块的 `surfaces[].rect` 是相对该画布的矩形；`z` 越大越靠前。
- 像素换算只在运行期做，不写进配置。
- 源表未定的内容（7 件拓本、96 题、灵象 IP）不在本目录填猜测值。

## 校验

```powershell
python tools/validate_config.py
```

任何参数改动必须先过校验，再走 `_gate/changes/` 提案。
