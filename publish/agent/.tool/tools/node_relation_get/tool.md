# node_relation_get - 查询单个关系

## 功能说明

按ID查询节点关系，调用后端 `GET /api/node-relations/{id}` 接口。

## 参数说明

| 参数 | 类型 | 必需 | 默认值 | 说明 |
|------|------|------|--------|------|
| `id` | long | ✅ | - | 关系 ID |
| `netmind_api_base_url` | string | ❌ | 运行时配置 | NetMind API 基础地址 |
| `timeout_seconds` | number | ❌ | 10 | HTTP 请求超时时间（秒） |

## 返回值说明

```json
{
  "success": true,
  "message": "成功",
  "status_code": 200,
  "endpoint": "/api/node-relations/42",
  "id": 42,
  "relation": {
    "id": 42,
    "sourceId": 10,
    "targetId": 20,
    "relationType": "relates_to",
    "weight": 1.0,
    "mapId": 42,
    ...
  },
  "raw_response": { ... }
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `success` | boolean | API 返回的成功标识 |
| `message` | string | API 返回的消息 |
| `status_code` | integer | HTTP 状态码 |
| `endpoint` | string | 实际调用的端点路径 |
| `id` | long | 查询的关系 ID |
| `relation` | object/null | 节点关系对象（NodeRelationDto），失败时为 null |
| `raw_response` | object | 完整的 API 响应体 |

## 使用示例

```json
{
  "id": 42
}
```

## 异常说明

| 场景 | 返回值 |
|------|--------|
| 缺少 `id` | `success: false`，`error: "缺少必需参数: id"` |
| 未配置 API 基础地址 | 抛出 `ValueError` |
| HTTP 超时 | 抛出 `RuntimeError` |
| 非 JSON 响应 | 抛出 `ValueError` |
| 网络错误 | 抛出 `RuntimeError` |

## 维护注意事项

1. 端点路径通过常量 `ENDPOINT` 定义，修改时只需更新常量。
2. `id` 为必需参数，通过路径模板拼接。
3. API 基础地址优先从 `tool_runtime.shared.netmind_api_base_url` 读取。
4. 超时时间支持运行时注入，默认 10 秒。
5. 所有 HTTP 错误均被捕获并返回结构化的错误信息。