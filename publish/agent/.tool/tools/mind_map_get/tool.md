# mind_map_get

## 功能说明
按 ID 查询单个思维导图的详细信息，返回导图的标题、ID、创建时间等信息。

## 参数说明
| 参数名 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| id | long | 是 | 导图 ID |

运行时需通过 `__runtime` 或 `params` 提供 NetMind API 基础地址和超时时间。

## 返回值说明
返回统一响应格式：

```json
{
  "success": true,
  "message": "成功",
  "status_code": 200,
  "data": {
    "id": 1,
    "title": "示例导图",
    "rootNodeId": null,
    "createdAt": "2025-01-01T00:00:00",
    "updatedAt": "2025-01-01T00:00:00"
  }
}
```

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| success | boolean | 请求是否成功 |
| message | string | 响应消息 |
| status_code | int | HTTP 状态码 |
| data | object | MindMapDto 对象，失败时为 null |

## 使用示例
```python
result = run({
    "id": 1,
    "__runtime": {
        "shared": {
            "netmind_api_base_url": "http://127.0.0.1:5120",
            "timeout_seconds": 10
        }
    }
})
```

## 异常说明
- 缺少 `id` 参数时返回错误。
- `ValueError`：缺少 NetMind API 基础地址时抛出。
- `urllib.error.URLError`：网络连接失败。
- HTTP 非 2xx 状态码会返回错误信息。

## 维护注意事项
- 基础地址由运行时注入，不要硬编码。
- `id` 参数需为整数，非数字会引发异常。