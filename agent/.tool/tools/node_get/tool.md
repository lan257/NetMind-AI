# node_get - 查询单个节点

## 功能说明

按 ID 查询节点详情，调用后端 `GET /api/nodes/{id}` 接口。

## 参数说明

| 参数 | 类型 | 必需 | 默认值 | 说明 |
|------|------|------|--------|------|
| `id` | long | ✅ | - | 节点 ID |
| `netmind_api_base_url` | string | ❌ | 运行时配置 | NetMind API 基础地址，例如 `http://127.0.0.1:5120` |
| `timeout_seconds` | number | ❌ | 10 | HTTP 请求超时时间（秒） |

## 返回值说明

```json
{
  "success": true,
  "message": "成功",
  "status_code": 200,
  "endpoint": "/api/nodes/123",
  "node": {
    "id": 123,
    "mapId": 42,
    "title": "示例节点",
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
| `node` | object/null | 节点对象（NodeDto），失败时为 null |
| `raw_response` | object | 完整的 API 响应体 |

## 使用示例

```json
{
  "id": 123
}
```

带超时配置：
```json
{
  "id": 123,
  "timeout_seconds": 15
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

1. 端点路径通过 `ENDPOINT_TEMPLATE` 格式化，修改路径时只需更新常量。
2. API 基础地址优先从 `tool_runtime.shared.netmind_api_base_url` 读取，兼容多种配置方式。
3. 超时时间支持运行时注入，默认 10 秒。
4. 所有 HTTP 错误均被捕获并返回结构化的错误信息，便于上层调试。
5. `raw_response` 保留完整响应体，方便除错和扩展。