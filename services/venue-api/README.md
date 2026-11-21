# venue-api｜桂林之眼本地服务

零依赖（Python 3 标准库 + SQLite），给 H5 四端与 Unity 控幕提供接口。

## 运行

```powershell
cd 仓库\GuilinEye\services\venue-api
python src\server.py --port 8080
# 浏览器打开 http://127.0.0.1:8080/ 即为 H5 入口
```

数据默认落在 `services/venue-api/data/venue.db`，首次启动自动建表并写入 12 条演示题。
也可以用 `--db <path>` 指定测试库。

## 测试

```powershell
python -u tests\test_api.py
# 期望：Ran 7 tests ... OK
```

## 接口

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/v1/health` | 健康检查 |
| GET | `/api/v1/control/state` | Unity 轮询的扁平状态：paused/segment/volume/command |
| POST | `/api/v1/control/{pause\|resume\|segment\|volume}` | 讲解员控幕 |
| GET | `/api/v1/b04/questions?age=` | 分龄取题（不含答案） |
| POST | `/api/v1/b04/answers` | 判题、连对/连错升降、落字事件 |
| GET | `/api/v1/b04/wall-events?after=` | Unity 拉取落字 |
| POST | `/api/v1/b04/certificates` | 免费电子证书；收费证书线下 |
| POST | `/api/v1/c03/blessings` | 放河灯，12 并发、第 13 排队 |
| GET | `/api/v1/c03/blessings/{id}` / `/c03/state` | 灯状态 / 当前并发 |
| POST | `/api/v1/c04/works` | 投画，每日 500 上限、18 格 |
| GET | `/api/v1/c04/wall` | 长卷当前 18 格 |
| GET | `/api/v1/c05/catalog` | 四类商品；价格 `null` + pending |
| POST | `/api/v1/c05/orders` | 生成 6 位取货码，印章 60 s |

## 边界

- 所有 POST 支持 `Idempotency-Key`，重复请求返回同一结果。
- 题库为 12 条演示种子（`bankStatus:"draft"`），正式 96 题待学科教师终审。
- 支付、库存、打印、售后不在本服务内；C-05 价格为 `null` 是刻意为之，不得由前端虚构。
- 内容审核/敏感词、鉴权、限流尚未接入，正式上线前必须补齐。
- 本地服务绑定 `0.0.0.0`，只用于馆内局域网调试；不要直接暴露到公网。
