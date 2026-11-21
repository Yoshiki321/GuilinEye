# H5 四端｜桂林之眼

移动端优先的四个入口，共用 `shared/ge.css` 与 `shared/ge.js`。

| 目录 | 入口 | 对应展项 | 依赖接口 |
|---|---|---|---|
| `quiz/` | 诗词飞花答题 | B-04 | `/b04/questions`、`/b04/answers`、`/b04/certificates` |
| `lantern/` | 祈愿河灯 | C-03 | `/c03/blessings`、`/c03/state` |
| `draw/` | 千人共绘 | C-04 | `/c04/works`、`/c04/wall` |
| `pickup/` | 文创取件 | C-05 | `/c05/catalog`、`/c05/orders` |

## 本地预览

由 venue-api 直接托管（同源，无跨域）：

```powershell
cd 仓库\GuilinEye\services\venue-api
python src\server.py --port 8080
```

打开 `http://127.0.0.1:8080/`。

## 约定

- 不直连 Unity；所有状态经 venue-api 落库，Unity 侧由 `ControlApi` 或模块订阅轮询。
- 失败时展示可读错误，不吞异常；断网不伪造“已上墙”。
- 画作只传 JPEG dataURL（质量 0.55，640×480），正式环境应改为对象存储直传。
- 文案、题库、商品价格均以馆方确认版本为准；演示态必须在界面明示。
