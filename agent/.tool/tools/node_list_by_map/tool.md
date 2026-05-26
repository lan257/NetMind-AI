# node_list_by_map - 查询导图下节点

## 功能说明

查询某个思维导图下的所有节点。

## 参数说明

| 参数 | 类型 | 必需 | 默认值 | 说明 |
|------|------|------|--------|------|
| `mapId` | number | ✅ | - | 导图 ID |
| `__runtime` | object | ❌ | 无 | 运行时配置（由 AgentBuild 注入） |

## 返回值说明

```json
{
  "success": true,
  "message": "成功",
  "status_code": 200,
  "endpoint": "/api/nodes/by-map/123",
  "nodes": [
    {
      "id": 1,
      "mapId": 123,
      "title": "节点标题",
      ...
    }
  ],
  "raw_response": {}
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `success` | boolean | 是否成功 |
| `message` | string | 响应消息 |
| `status_code` | integer | HTTP 状态码 |
| `endpoint` | string | 实际调用的 API 路径 |
| `nodes` | array | 节点列表，失败时为 null |
| `raw_response` | object | 原始 API 响应 |

## 使用示例

```json
{
  "mapId": 123
}
```

## 异常说明

| 场景 | 返回值 |
|------|--------|
| 缺少 `mapId` | `success: false`，`error: "缺少必需参数: mapId"` |
| API 返回错误 | `success: false`，`message` 包含错误信息 |
| 网络异常 | `RuntimeError` |
| 缺少 API 基础 URL | `ValueError` |

## 维护注意事项

1. 依赖运行时配置中的 `netmind_api_base_url`
2. 超时时间默认 10 秒，可通过 `timeout_seconds` 参数或运行时覆盖
3. 返回的 `nodes` 结构参考 `NodeDto` 字段
4. 建议在调用前先确保导图存在