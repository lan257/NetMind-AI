# mind_map_cascade_delete

## 功能说明
删除导图并级联删除所有关联节点与关系。彻底清理指定导图及其全部子数据。

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
    "deleted": true,
    "affectedCount": 3
  }
}
```

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| success | boolean | 请求是否成功 |
| message | string | 响应消息 |
| status_code | int | HTTP 状态码 |
| data | object | DeleteResultDto，失败时为 null |

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
- 级联删除操作不可逆，请谨慎使用。
- base_url 由运行时注入，不要硬编码。
- 本接口会同时删除导图、节点和关系，影响范围较大。