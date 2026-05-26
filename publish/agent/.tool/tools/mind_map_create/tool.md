# mind_map_create

## 功能说明
创建新的思维导图，提供标题即可，返回创建的导图信息。

## 参数说明
| 参数名 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| title | string | 是 | 导图标题 |

运行时需通过 `__runtime` 或 `params` 提供 NetMind API 基础地址和超时时间。

## 返回值说明
返回统一响应格式：

```json
{
  "success": true,
  "message": "成功",
  "status_code": 201,
  "data": {
    "id": 1,
    "title": "新建导图",
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
| status_code | int | HTTP 状态码（201 表示创建成功） |
| data | object | MindMapDto 对象，失败时为 null |

## 使用示例
```python
result = run({
    "title": "我的思维导图",
    "__runtime": {
        "shared": {
            "netmind_api_base_url": "http://127.0.0.1:5120",
            "timeout_seconds": 10
        }
    }
})
```

## 异常说明
- 缺少 `title` 参数或类型错误时返回错误。
- `ValueError`：缺少 NetMind API 基础地址时抛出。
- `urllib.error.URLError`：网络连接失败。
- HTTP 非 2xx 状态码会返回错误信息。

## 维护注意事项
- 基础地址由运行时注入，不要硬编码。
- 标题不能为空，后端通常有长度限制。