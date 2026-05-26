# node_update - 更新节点

## 功能说明

更新思维导图中指定节点的属性，支持修改父节点、标题、内容、排序号、画布坐标。通过调用后端 `PUT /api/nodes/{id}` 接口实现。

## 参数说明

| 参数 | 类型 | 必需 | 默认值 | 说明 |
|------|------|------|--------|------|
| `node_id` | long | ✅ | - | 要更新的节点 ID |
| `parentId` | long/null | ❌ | null | 新的父节点 ID |
| `title` | string | ❌ | - | 节点标题 |
| `content` | string/null | ❌ | null | 节点内容 |
| `orderNo` | int | ❌ | - | 同级排序号 |
| `positionX` | double/null | ❌ | null | 画布 X 坐标 |
| `positionY` | double/null | ❌ | null | 画布 Y 坐标 |
| `__runtime` | object | ❌ | - | 运行时注入对象，包含 `netmind_api_base_url` 等配置 |

## 返回值说明

```json
{
  "success": true,
  "status_code": 200,
  "endpoint": "/api/nodes/123",
  "data": {"id": 123, "title": "新标题", ...},
  "message": "成功"
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `success` | boolean | 是否执行成功（API 调用本身成功，非业务成功） |
| `status_code` | integer | HTTP 状态码 |
| `endpoint` | string | 请求的 API 路径 |
| `data` | object/null | 接口返回的 `data` 字段（节点详情） |
| `message` | string | 接口返回的 `message` 字段 |
| `error` | string | 当发生本地错误时的描述 |

## 使用示例

### 更新节点标题
```json
{
  "node_id": 123,
  "title": "新的标题"
}
```

### 更新节点位置和内容
```json
{
  "node_id": 456,
  "content": "详细描述",
  "positionX": 150.5,
  "positionY": 200.0
}
```

### 移动节点到其他父节点
```json
{
  "node_id": 789,
  "parentId": 100,
  "orderNo": 1
}
```

## 运行时配置说明

需提供 `__runtime` 对象（由 AgentBuild 注入），结构如下：

```json
{
  "skill": {
    "netmind_api_base_url": "http://127.0.0.1:5120",
    "timeout_seconds": 10
  },
  "shared": {
    "netmind_api_base_url": "http://127.0.0.1:5120"
  }
}
```

或通过环境变量 `NETMIND_API_BASE_URL` 设置。

## 异常说明

| 场景 | 返回值 |
|------|--------|
| 缺少 `node_id` | `success: false`, `error: "缺少必需参数: node_id"` |
| API 返回 4xx/5xx | `success: false`, `status_code: xxx`, `message: "错误描述"` |
| 网络错误 | `success: false`, `error: "异常描述"` |
| 缺少 API 基地址 | 抛出 ValueError |

## 维护注意事项

1. **参数校验**：只发送用户显式提供的字段，避免覆盖未指定属性
2. **安全性**：依赖注入的 `netmind_api_base_url`，不将 API 地址硬编码
3. **超时处理**：默认超时 10 秒，可通过 `__runtime` 覆盖
4. **编码**：所有请求体编码为 UTF-8
5. **错误传递**：将 HTTP 错误状态码和消息原样返回，方便调试
6. **路径参数**：`node_id` 作为 URL 路径的一部分，注意 URL 编码
7. **依赖**：需后端 `PUT /api/nodes/{id}` 接口可用