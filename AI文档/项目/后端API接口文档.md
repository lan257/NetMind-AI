# NetMind 后端 API 接口文档

更新时间：2026-05-14

本文档基于 `src/NetMind.WebApi/Controllers` 与 `src/NetMind.Models/Dtos` 整理，用于快速查看后端接口用途、传参和返回参数。

## 1. 通用说明

- 基础路径：以实际部署地址为准，接口路径均以 `/api` 开头。
- 请求体：除文件上传接口外，均使用 `application/json`。
- 文件上传：`POST /api/mind-map-transfer/file` 使用 `multipart/form-data`。
- 时间字段：`createdAt`、`updatedAt` 为 `DateTimeOffset` 序列化结果。
- 常见状态码：
  - `200`：查询、更新、删除成功。
  - `201`：创建或导入成功。
  - `400`：参数错误、AI 调用错误或业务校验失败。
  - `404`：资源不存在。

### 1.1 统一响应格式

大部分接口返回统一包装：

```json
{
  "success": true,
  "message": "成功",
  "data": {}
}
```

字段说明：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| success | boolean | 是否成功 |
| message | string | 响应消息 |
| data | object/null | 实际返回数据，失败时通常为 null |

`GET /api/mind-map-transfer/{mapId}/file` 与 `GET /api/mind-map-transfer/template` 返回 JSON 文件下载，不使用统一包装。

## 2. 系统接口

### 2.1 健康状态

`GET /api/system/health`

用途：获取 API 与项目运行状态。

传参：无。

返回 `ProjectStatusViewModel`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| projectName | string | 项目名称 |
| phase | string | 当前阶段 |
| runtime | string | 后端运行环境 |
| frontend | string | 前端状态 |

## 3. 思维导图接口

### 3.1 查询导图列表

`GET /api/mind-maps`

用途：查询全部思维导图。

传参：无。

返回：`MindMapDto[]`。

### 3.2 查询单个导图

`GET /api/mind-maps/{id}`

用途：按 ID 查询导图。

路径参数：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| id | long | 导图 ID |

返回：`MindMapDto`。

### 3.3 创建导图

`POST /api/mind-maps`

用途：创建新思维导图。

请求体 `CreateMindMapRequest`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| title | string | 导图标题 |

返回：`MindMapDto`。

### 3.4 更新导图

`PUT /api/mind-maps/{id}`

用途：更新导图标题或根节点。

路径参数：`id`，导图 ID。

请求体 `UpdateMindMapRequest`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| title | string | 导图标题 |
| rootNodeId | long/null | 根节点 ID |

返回：`MindMapDto`。

### 3.5 删除导图

`DELETE /api/mind-maps/{id}`

用途：删除导图，不级联删除关联节点。

路径参数：`id`，导图 ID。

返回：`DeleteResultDto`。

### 3.6 级联删除导图

`DELETE /api/mind-maps/{id}/cascade`

用途：删除导图并级联删除相关节点与关系。

路径参数：`id`，导图 ID。

返回：`DeleteResultDto`。

## 4. 节点接口

### 4.1 查询导图下节点

`GET /api/nodes/by-map/{mapId}`

用途：查询某个导图下的所有节点。

路径参数：`mapId`，导图 ID。

返回：`NodeDto[]`。

### 4.2 搜索节点

`GET /api/nodes/search?mapId={mapId}&keyword={keyword}&limit={limit}`

用途：按关键词搜索节点，可限定导图范围。

查询参数：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| mapId | long/null | 导图 ID，可为空 |
| keyword | string | 搜索关键词 |
| limit | int | 返回数量，默认 10 |

返回：`NodeDto[]`。

### 4.3 查询单个节点

`GET /api/nodes/{id}`

用途：按 ID 查询节点。

路径参数：`id`，节点 ID。

返回：`NodeDto`。

### 4.4 创建节点

`POST /api/nodes`

用途：在导图中创建节点。

请求体 `CreateNodeRequest`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| mapId | long | 所属导图 ID |
| parentId | long/null | 父节点 ID |
| title | string | 节点标题 |
| content | string/null | 节点内容 |
| orderNo | int | 同级排序号 |
| positionX | double/null | 画布 X 坐标 |
| positionY | double/null | 画布 Y 坐标 |

返回：`NodeDto`。

### 4.5 更新节点

`PUT /api/nodes/{id}`

用途：更新节点父级、内容、排序或画布坐标。

路径参数：`id`，节点 ID。

请求体 `UpdateNodeRequest`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| parentId | long/null | 父节点 ID |
| title | string | 节点标题 |
| content | string/null | 节点内容 |
| orderNo | int | 同级排序号 |
| positionX | double/null | 画布 X 坐标 |
| positionY | double/null | 画布 Y 坐标 |

返回：`NodeDto`。

### 4.6 删除单个节点

`DELETE /api/nodes/{id}`

用途：删除指定节点本身。

路径参数：`id`，节点 ID。

返回：`DeleteResultDto`。

### 4.7 删除节点子树

`DELETE /api/nodes/{id}/subtree`

用途：删除指定节点及其子孙节点。

路径参数：`id`，节点 ID。

返回：`DeleteResultDto`。

## 5. 节点关系接口

### 5.1 查询导图下关系

`GET /api/node-relations/by-map/{mapId}`

用途：查询某个导图下的节点关系。

路径参数：`mapId`，导图 ID。

返回：`NodeRelationDto[]`。

### 5.2 查询节点相关关系

`GET /api/node-relations/by-node/{nodeId}`

用途：查询与某节点相关的关系。

路径参数：`nodeId`，节点 ID。

返回：`NodeRelationDto[]`。

### 5.3 查询单个关系

`GET /api/node-relations/{id}`

用途：按 ID 查询节点关系。

路径参数：`id`，关系 ID。

返回：`NodeRelationDto`。

### 5.4 创建关系

`POST /api/node-relations`

用途：创建节点之间的关联。

请求体 `CreateNodeRelationRequest`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| sourceId | long | 起始节点 ID |
| targetId | long | 目标节点 ID |
| relationType | string | 关系类型 |
| weight | double | 权重，默认 1 |
| mapId | long | 所属导图 ID |

返回：`NodeRelationDto`。

### 5.5 更新关系

`PUT /api/node-relations/{id}`

用途：更新关系类型或权重。

路径参数：`id`，关系 ID。

请求体 `UpdateNodeRelationRequest`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| relationType | string | 关系类型 |
| weight | double | 权重，默认 1 |

返回：`NodeRelationDto`。

### 5.6 删除关系

`DELETE /api/node-relations/{id}`

用途：删除指定节点关系。

路径参数：`id`，关系 ID。

返回：`DeleteResultDto`。

### 5.7 删除节点相关关系

`DELETE /api/node-relations/by-node/{nodeId}`

用途：删除某节点相关的所有关系。

路径参数：`nodeId`，节点 ID。

返回：`DeleteResultDto`。

## 6. AI 接口

### 6.1 查询可用模型

`GET /api/ai/models`

用途：查询当前配置的 AI 模型列表。

传参：无。

返回：`AiModelOptionDto[]`。

### 6.2 AI 清洗为导图结构

`POST /api/ai/clean`

用途：将自然语言内容清洗为可导入的导图结构。

请求体 `AiCleanRequest`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| naturalLanguage | string | 待清洗文本 |
| modelId | string/null | 模型 ID |
| apiKey | string/null | 临时 API Key |
| endpoint | string/null | 临时模型端点 |
| provider | string/null | 临时模型提供方 |

返回：`AiCleanResultDto`，包含 `selectedModel`、`prompt`、`transfer`、`warnings`。

### 6.3 需求结构化

`POST /api/ai/requirements/structure`

用途：将需求文本和上下文结构化为导图传输结构。

请求体 `AiRequirementStructureRequest`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| requirement | string | 需求文本 |
| context | string | 上下文 |
| modelId | string/null | 模型 ID |
| apiKey | string/null | 临时 API Key |
| endpoint | string/null | 临时模型端点 |
| provider | string/null | 临时模型提供方 |

返回：`AiRequirementStructureResultDto`，包含 `selectedModel`、`prompt`、`contextSummary`、`wasContextCompressed`、`transfer`、`warnings`。

### 6.4 节点 AI 对话

`POST /api/ai/node-chat`

用途：围绕单个节点进行 AI 对话。

请求体 `AiNodeChatRequest`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| nodeId | long | 节点 ID |
| message | string | 用户消息 |
| context | string | 上下文 |
| conversationId | string/null | 会话 ID，传入后自动保存对话记录 |
| modelId | string/null | 模型 ID |
| apiKey | string/null | 临时 API Key |
| maxContextLength | int | 最大上下文长度，默认 51200 |
| endpoint | string/null | 临时模型端点 |
| provider | string/null | 临时模型提供方 |

返回：`AiNodeChatResult`。

### 6.5 导图 AI 对话

`POST /api/ai/map-chat`

用途：围绕整张导图进行 AI 对话。

请求体 `AiMapChatRequest`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| mapId | long | 导图 ID |
| message | string | 用户消息 |
| context | string | 上下文 |
| conversationId | string/null | 会话 ID，传入后自动保存对话记录 |
| modelId | string/null | 模型 ID |
| apiKey | string/null | 临时 API Key |
| maxContextLength | int | 最大上下文长度，默认 51200 |
| endpoint | string/null | 临时模型端点 |
| provider | string/null | 临时模型提供方 |

返回：`AiMapChatResult`。

### 6.6 Agent 对话

以下接口都使用 `AiAgentChatRequest` 基础字段，并按场景增加目标 ID：

| 方法 | 路径 | 用途 | 额外字段 |
| --- | --- | --- | --- |
| POST | `/api/ai/node-agent-chat` | 节点 Agent 对话 | `nodeId` |
| POST | `/api/ai/map-agent-chat` | 导图 Agent 对话 | `mapId` |
| POST | `/api/ai/global-agent-chat` | 全局 Agent 对话 | 无 |
| POST | `/api/ai/app-help-agent-chat` | 应用帮助 Agent 对话 | 无 |

基础请求字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| message | string | 用户消息 |
| context | string | 上下文 |
| conversationId | string/null | 会话 ID，传入后自动保存对话记录 |
| modelId | string/null | 模型 ID |
| apiKey | string/null | 临时 API Key |
| endpoint | string/null | 临时模型端点 |
| provider | string/null | 临时模型提供方 |
| maxContextLength | int | 最大上下文长度，默认 51200 |
| agentBuildPath | string/null | AgentBuild 路径 |
| domainAndSkillBinding | string/null | 领域与技能绑定配置 |
| agentContext | object/null | Agent 上下文 |
| confirmedSkillCalls | object[] | 已确认的技能调用 |
| historySkillCalls | object[] | 历史技能调用 |

返回：`AiAgentChatResult`，包含 `selectedModel`、`prompt`、`reply`、`status`、`agentTarget`、`skillCalls`、`contextUpdate`、`compressedContext`、`wasContextCompressed`、`contextUsagePercent`、`contextStatus`、`warnings`。

### 6.7 应用帮助对话

`POST /api/ai/app-help-chat`

用途：应用帮助场景的普通 AI 对话。

请求体 `AiAppHelpRequest`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| message | string | 用户消息 |
| context | string | 上下文 |
| conversationId | string/null | 会话 ID，传入后自动保存对话记录 |
| modelId | string/null | 模型 ID |
| apiKey | string/null | 临时 API Key |
| maxContextLength | int | 最大上下文长度，默认 51200 |
| endpoint | string/null | 临时模型端点 |
| provider | string/null | 临时模型提供方 |

返回：`AiAppHelpResult`。

### 6.8 上下文对话

`POST /api/ai/context-chat`

用途：基于通用上下文进行 AI 对话。

请求体 `AiContextChatRequest`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| conversationId | string | 会话 ID，传入后自动保存对话记录 |
| message | string | 用户消息 |
| context | string | 上下文 |
| modelId | string/null | 模型 ID |
| apiKey | string/null | 临时 API Key |
| endpoint | string/null | 临时模型端点 |
| provider | string/null | 临时模型提供方 |

返回：`AiContextChatResultDto`。

## 7. AI 对话记录接口

### 7.1 查询对话记录

`GET /api/ai-conversation-records?conversationId={conversationId}`

用途：查询 AI 对话记录，可按会话 ID 过滤。

查询参数：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| conversationId | string/null | 会话 ID |

返回：`AiConversationRecordDto[]`。

### 7.2 查询单条对话记录

`GET /api/ai-conversation-records/{id}`

用途：按 ID 查询单条 AI 对话记录。

路径参数：`id`，记录 ID。

返回：`AiConversationRecordDto`。

### 7.3 创建对话记录

`POST /api/ai-conversation-records`

用途：手动创建 AI 对话记录。

请求体 `CreateAiConversationRecordRequest`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| conversationId | string | 会话 ID |
| role | string | 角色，如 user、assistant |
| content | string | 消息内容 |
| modelId | string/null | 模型 ID |
| prompt | string/null | 使用的 Prompt |
| contextSummary | string/null | 上下文摘要 |
| wasContextCompressed | boolean | 是否压缩过上下文 |

返回：`AiConversationRecordDto`。

### 7.4 更新对话记录

`PUT /api/ai-conversation-records/{id}`

用途：更新 AI 对话记录。

路径参数：`id`，记录 ID。

请求体 `UpdateAiConversationRecordRequest`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| role | string | 角色 |
| content | string | 消息内容 |
| modelId | string/null | 模型 ID |
| prompt | string/null | 使用的 Prompt |
| contextSummary | string/null | 上下文摘要 |
| wasContextCompressed | boolean | 是否压缩过上下文 |

返回：`AiConversationRecordDto`。

### 7.5 删除对话记录

`DELETE /api/ai-conversation-records/{id}`

用途：删除指定 AI 对话记录。

路径参数：`id`，记录 ID。

返回：`DeleteResultDto`。

## 8. 导图导入导出接口

### 8.1 导出导图结构

`GET /api/mind-map-transfer/{mapId}/structure`

用途：导出导图完整结构，返回统一响应。

路径参数：`mapId`，导图 ID。

返回：`MindMapStructureDto`。

### 8.2 下载导图文件

`GET /api/mind-map-transfer/{mapId}/file`

用途：下载导图传输 JSON 文件。

路径参数：`mapId`，导图 ID。

返回：JSON 文件，文件名格式为 `netmind-map-{mapId}.json`。

### 8.3 导入导图结构

`POST /api/mind-map-transfer/structure`

用途：通过 JSON 请求体导入导图。

请求体 `ImportMindMapRequest`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| mindMap | MindMapTransferDto | 导图传输结构 |
| titleOverride | string/null | 导入时覆盖标题 |

返回：`ImportedMindMapDto`。

### 8.4 上传文件导入导图

`POST /api/mind-map-transfer/file`

用途：上传 JSON 文件导入导图。

表单参数：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| file | file | JSON 导入文件 |
| titleOverride | string/null | 导入时覆盖标题 |

返回：`ImportedMindMapDto`。

### 8.5 下载导入模板

`GET /api/mind-map-transfer/template`

用途：下载导图导入模板。

传参：无。

返回：JSON 文件，文件名为 `netmind-import-template.json`。

## 9. 主要 DTO 字段

### 9.1 MindMapDto

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| id | long | 导图 ID |
| title | string | 导图标题 |
| rootNodeId | long/null | 根节点 ID |
| createdAt | datetime | 创建时间 |
| updatedAt | datetime | 更新时间 |

### 9.2 NodeDto

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| id | long | 节点 ID |
| mapId | long | 所属导图 ID |
| mapTitle | string/null | 所属导图标题 |
| parentId | long/null | 父节点 ID |
| title | string | 节点标题 |
| content | string/null | 节点内容 |
| orderNo | int | 排序号 |
| positionX | double/null | 画布 X 坐标 |
| positionY | double/null | 画布 Y 坐标 |
| createdAt | datetime | 创建时间 |
| updatedAt | datetime | 更新时间 |

### 9.3 NodeRelationDto

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| id | long | 关系 ID |
| sourceId | long | 起始节点 ID |
| sourceTitle | string/null | 起始节点标题 |
| sourceMapId | long/null | 起始节点所属导图 ID |
| targetId | long | 目标节点 ID |
| targetTitle | string/null | 目标节点标题 |
| targetMapId | long/null | 目标节点所属导图 ID |
| relationType | string | 关系类型 |
| weight | double | 权重 |
| mapId | long | 所属导图 ID |
| createdAt | datetime | 创建时间 |

### 9.4 MindMapTransferDto

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| schemaVersion | string | 结构版本，默认 `netmind.mindmap.v1` |
| title | string | 导图标题 |
| nodes | MindMapTransferNodeDto[] | 节点列表 |
| relations | MindMapTransferRelationDto[] | 关系列表 |

### 9.5 MindMapTransferNodeDto

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| clientId | string | 导入导出用客户端节点 ID |
| parentClientId | string/null | 父节点客户端 ID |
| title | string | 节点标题 |
| content | string/null | 节点内容 |
| orderNo | int | 排序号 |
| positionX | double/null | 画布 X 坐标 |
| positionY | double/null | 画布 Y 坐标 |

### 9.6 MindMapTransferRelationDto

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| sourceClientId | string | 起始节点客户端 ID |
| targetClientId | string | 目标节点客户端 ID |
| relationType | string | 关系类型，默认 `relates_to` |
| weight | double | 权重，默认 1 |

### 9.7 MindMapStructureDto

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| map | MindMapDto | 导图信息 |
| nodes | NodeDto[] | 节点列表 |
| relations | NodeRelationDto[] | 关系列表 |
| transfer | MindMapTransferDto | 可导入导出的传输结构 |

### 9.8 ImportedMindMapDto

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| structure | MindMapStructureDto | 导入后的完整导图结构 |
| nodeIdMap | object | clientId 到真实节点 ID 的映射 |

### 9.9 AiModelOptionDto

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| id | string | 模型 ID |
| name | string | 模型名称 |
| provider | string | 提供方 |
| endpoint | string | 调用端点 |
| isDefault | boolean | 是否默认模型 |
| status | string | 状态 |
| notes | string | 备注 |

### 9.10 AI 对话类返回字段

`AiNodeChatResult`、`AiMapChatResult`、`AiAppHelpResult`、`AiContextChatResultDto` 字段大体一致：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| selectedModel | AiModelOptionDto | 实际使用模型 |
| prompt | string | 实际 Prompt |
| reply | string | AI 回复 |
| compressedContext | string | 压缩后的上下文，部分结果包含 |
| contextSummary | string | 上下文摘要，部分结果包含 |
| wasContextCompressed | boolean | 是否压缩上下文 |
| contextUsagePercent | double | 上下文使用比例，部分结果包含 |
| contextStatus | string | 上下文状态，部分结果包含 |
| warnings | string[] | 警告信息 |

### 9.11 AiConversationRecordDto

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| id | long | 记录 ID |
| conversationId | string | 会话 ID |
| role | string | 角色 |
| content | string | 消息内容 |
| modelId | string/null | 模型 ID |
| prompt | string/null | Prompt |
| contextSummary | string/null | 上下文摘要 |
| wasContextCompressed | boolean | 是否压缩上下文 |
| createdAt | datetime | 创建时间 |
| updatedAt | datetime | 更新时间 |

### 9.12 DeleteResultDto

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| deleted | boolean | 是否删除成功 |
| affectedCount | int | 影响数量 |
