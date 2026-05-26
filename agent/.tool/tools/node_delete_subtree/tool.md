# node_delete_subtree - 删除节点子树

## 功能说明

调用 `DELETE /api/nodes/{id}/subtree` 删除指定节点及其所有子孙节点。

## 参数说明

| 参数 | 类型 | 必需 | 默认值 | 说明 |
|------|------|------|--------|------|
| `id` | integer | ✅ | - | 节点 ID |

运行时参数（通过 `__runtime` 注入）：
- `shared.netmind_api_base_url` : API 基础地址
- `shared.timeout_seconds` : 超时秒数，默认 10

## 返回值说明

```json
{
  "success": true,
  "message": "成功",
  "status_code": 200,
  "endpoint": "/api/nodes/42/subtree",
  "deleted": true,
  "affectedCount": 5,
  "raw_response": {}
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| success | boolean | 是否成功 |
| message | string | 响应消息 |
| status_code | number | HTTP 状态码 |
| endpoint | string | 实际请求路径 |
| deleted | boolean | 是否删除成功 |
| affectedCount | number | 影响节点数量 |
| raw_response | object | 原始响应 |

## 使用示例

```json
{
  "id": 42
}
```

## 异常说明

| 场景 | 返回值 |
|------|--------|
| 缺少 id 参数 | success: false, error: "缺少必需参数: id" |
| 节点不存在（404） | success: false, message: "节点不存在", status_code: 404 |
| API 不可用 | 抛出 RuntimeError |

## 维护注意事项

1. 该操作不可逆，注意备份
2. 删除的是节点及其所有子孙节点，影响范围可能很大
3. 依赖 NetMind API 基础地址配置