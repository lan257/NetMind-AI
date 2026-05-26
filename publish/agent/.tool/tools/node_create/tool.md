# node_create - 创建节点

## 功能说明

在导图中创建新节点。

## 参数说明

| 参数 | 类型 | 必需 | 默认值 | 说明 |
|------|------|------|--------|------|
| `mapId` | number | ✅ | - | 所属导图 ID |
| `parentId` | number | ❌ | null | 父节点 ID |
| `title` | string | ✅ | - | 节点标题 |
| `content` | string | ❌ | null | 节点内容 |
| `orderNo` | number | ❌ | 0 | 同级排序号 |
| `positionX` | number | ❌ | null | 画布 X 坐标 |
| `positionY` | number | ❌ | null | 画布 Y 坐标 |

## 返回值说明

```json
{
  "success": true,
  "message": "成功",
  "status_code": 201,
  "endpoint": "/api/nodes",
  "node": {
    "id": 1,
    "mapId": 123,
    "parentId": null,
    "title": "新节点",
    ...
  },
  "raw_response": {}
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `success` | boolean | 是否成功 |
| `message` | string | 响应消息 |
| `status_code` | integer | HTTP 状态码 |
| `endpoint` | string | 实际调用的 API 路径 |
| `node` | object | 创建的节点对象，失败时为 null |
| `raw_response` | object | 原始 API 响应 |

## 使用示例

```json
{
  "mapId": 123,
  "parentId": null,
  "title": "新节点",
  "content": "节点内容",
  "orderNo": 1
}
```

## 异常说明

| 场景 | 返回值 |
|------|--------|
| 缺少 `mapId` | `success: false`，`error: \"缺少必需参数: mapId\"` |
| 缺少 `title` | `success: false`，`error: \"缺少必需参数: title\"` |
| API 返回错误 | `success: false`，`message` 包含错误信息 |
| 网络异常 | `RuntimeError` |

## 维护注意事项

1. 依赖运行时配置中的 `netmind_api_base_url`
2. 超时时间默认 10 秒
3. 返回的 `node` 结构参考 `NodeDto` 字段
4. 创建后建议刷新节点列表