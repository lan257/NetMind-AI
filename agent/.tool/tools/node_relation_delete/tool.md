# node_relation_delete - 删除关系

## 功能说明

删除指定节点关系，调用后端 `DELETE /api/node-relations/{id}` 接口。

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
  "endpoint": "/api/node-relations/1",
  "relation_id": 1,
  "delete_result": {
    "deleted": true,
    "affectedCount": 1
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
| `relation_id` | long | 被删除的关系 ID |
| `delete_result` | object/null | 删除结果（DeleteResultDto），失败时为 null |
| `raw_response` | object | 完整的 API 响应体 |

## 使用示例

```json
{
  "id": 1
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
2. `id` 为必需参数。
3. API 基础地址优先从 `tool_runtime.shared.netmind_api_base_url` 读取。
4. 超时时间支持运行时注入，默认 10 秒。
5. 所有 HTTP 错误均被捕获并返回结构化的错误信息。
6. 删除操作需要用户确认权限（permission_level=confirm）。