# AI 大模型配置说明

## P1.1 配置目标

P1.1 只落地 AI 清洗业务闭环，不接入真实外部大模型网络调用。系统提供模型配置占位、模型列表接口和默认模型选择逻辑，保证后续替换真实模型时前后端接口不需要重做。

## 当前模型列表

当前模型列表由后端 `AiCleanService` 写死维护，`GET /api/ai/models` 返回。

| id | 名称 | provider | endpoint | 状态 | 说明 |
| --- | --- | --- | --- | --- | --- |
| `local-deepseek-placeholder` | DeepSeek Local Placeholder | `local` | `placeholder://local/deepseek` | `placeholder` | P1.1 默认使用第一个模型，占位模拟本地文本结构化清洗能力。 |
| `cloud-model-placeholder` | Cloud Model Placeholder | `cloud` | `placeholder://cloud/api` | `disabled` | 预留给后续付费云模型 API 集成。 |

## 默认选择规则

- 前端启动时调用 `GET /api/ai/models`。
- 后端返回的第一个模型为当前默认模型。
- 如果调用 `POST /api/ai/clean` 时没有传 `modelId`，后端自动使用第一个模型。
- 如果传入未知 `modelId`，后端回退到第一个模型，避免 P1.1 阶段阻断清洗流程。

## P1.1 清洗实现

当前清洗实现为确定性本地规则，不进行真实 AI 请求：

- 输入自然语言按换行和项目符号拆分。
- 第一条有效文本作为导图根节点标题。
- 后续条目扩充为根节点下的子节点。
- 如果只有一段文本，则按句号、问号、感叹号、分号拆分成多个主题项。
- 输出固定为 `netmind.mindmap.v1` 标准结构体。
- 自动生成 `root -> topic-*` 的 `expands_to` 关联。

## 后续真实模型接入要求

真实模型接入时建议保持当前接口不变，只替换 `IAiCleanService` 实现：

- 输入：自然语言、模型 id、可选业务上下文。
- 输出：必须返回可通过 `MindMapTransferService` 校验的 `MindMapTransferDto`。
- 模型必须具备稳定 JSON 输出能力，支持明确 schema 约束。
- 服务端必须继续保留结构校验，不能直接信任模型输出。
- 外部 API Key、Endpoint、超时、重试和模型列表建议移入配置文件或环境变量，避免写死敏感信息。

## 不在 P1.1 范围内

- 不接入真实 DeepSeek/Ollama/OpenAI/其他云 API。
- 不保存 API Key。
- 不实现流式输出。
- 不实现用户级模型配置。
