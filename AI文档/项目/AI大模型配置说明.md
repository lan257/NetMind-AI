# AI 大模型配置说明

更新时间：2026-05-22

## P6.1.5 新增：Agent 调用瘦身

P6.1.5 删除 NetMind Agent 调用层中的旧 v1 兼容路径，只保留 Agent Kernel API v2。

- **请求/响应字段**：后端 DTO、Service 和前端续跑只保留 `tool_calls`、`confirmed_tool_calls`、`history_tool_calls` 和 `tool_id`/`tool_name`。
- **运行时配置**：配置键改为 `AiAgent:ToolRuntimeTimeoutSeconds`，继续写入 `tool_runtime.shared.timeout_seconds`。
- **前端展示**：Tool 权限允许显示绿色反馈，拒绝和失败才使用红色反馈，避免把同意操作渲染为错误状态。

## P6.1.3 新增：应用帮助使用技巧自维护

P6.1.3 在应用帮助的追加型学习记录之外，新增一份可持续整理的使用技巧文档，供应用帮助 Agent 沉淀稳定经验。

- **使用技巧配置**：`AiClean:Prompt:PromptFiles:AppHelpUsageTips` 指向 `Config/AiCleanPrompts/app-help-usage-tips.md`。
- **使用技巧传递方式**：后端在 `context.focus_context.usage_tips_absolute_path` 中传入使用技巧文档绝对路径，并附带维护策略。
- **维护方式**：应用帮助 Agent Prompt 允许在确认技巧稳定后，对使用技巧文档调用 `incremental_file_modifier` 做小范围增量维护，可补充、修正和合并技巧。
- **边界**：正式说明书仍只读；`app-help-learning-log.md` 继续承接追加型学习线索，不能被使用技巧维护流程重写。

## P6.0 新增：Agent Kernel API v2 适配

NetMind 调用外部 AgentBuild 内核时已切到 API v2，避免继续依赖旧 `skill_*` 协议。

- **请求协议**：后端显式传入 `api_version=v2`，使用 `domain`、`tool_runtime`、`confirmed_tool_calls` 和 `history_tool_calls`。
- **响应协议**：后端只读取 `tool_calls`，并把 Tool 调用记录交给前端展示和续跑。
- **运行时配置**：NetMind WebAPI 地址和超时写入 `tool_runtime.shared`，由 AgentBuild 在执行 Tool 前注入 `params.__runtime`。
- **Prompt 口径**：Agent 场景提示要求模型输出 `tool_call_drafts` / `tool_id`，不再提示旧 `skill_call_drafts`。

## P5.1 新增：全图问答 Agent 与全局问答 Agent

P5.1 在 P5.0 节点问答 Agent 的基础上，继续接入六种聊天方式中的「全图问答（Agent）」和「全局问答（Agent）」。两种模式都通过 NetMind 后端调用外部 AgentBuild `src.agent_kernel`，沿用统一的模型配置、Tool 权限确认、Agent 记忆和对话历史机制。

- **全图问答（Agent）入口**：知识卡片左侧 AI 浮窗 → 模式选择 →「全图问答（Agent）」。
- **全图问答（Agent）端点**：`POST /api/ai/map-agent-chat`。
- **全图上下文范围**：后端传递当前导图的基础信息、全量节点 `nodes`、全量关联 `relations`、统计信息和对话历史；节点内容、父子层级、排序和关联权重都会进入 `focus_context`。
- **全局问答（Agent）入口**：知识卡片左侧 AI 浮窗 → 模式选择 →「全局问答（Agent）」。
- **全局问答（Agent）端点**：`POST /api/ai/global-agent-chat`。
- **全局上下文范围**：只传递 NetMind 基础应用信息、对话历史、Agent 记忆和上下文预算，不传递任何节点、关联或思维导图数据。
- **Prompt/身份配置**：Agent 身份和补充提示已迁移到 `src/NetMind.WebApi/Config/AiCleanPrompts/*-agent-*.prompt.md`，`appsettings*.json` 只保存 Prompt 文件路径。
- **AgentBuild 参数适配**：按 AgentBuild 当前接口规范，后端传入 `tool_runtime`。该字段不进入 Prompt，只会在 Kernel 执行 Tool 时注入到 `params.__runtime`，用于提供 NetMind WebAPI 地址和 Tool 超时。
- **服务 BaseUrl**：`App:BaseUrl` 同时用于 WebAPI 默认监听地址和 Agent Tool runtime 的 `netmind_api_base_url`。
- **Python 执行器**：默认仍读取 `AiAgent:PythonExecutable`，当前配置为 `py`；若本机 Windows Python Launcher 无法发现 Python，可改为本机 `python.exe` 的绝对路径。

## P5.3 新增：应用帮助 Agent 与入口收束

P5.3 删除前端 AI 浮窗中的「节点问答（聊天）」和「全图问答（聊天）」入口，保留节点问答 Agent、全图问答 Agent、全局问答 Agent 和应用帮助。普通聊天后端端点暂保留兼容，但产品入口转向 Agent。

- **应用帮助入口**：知识卡片左侧 AI 浮窗 → 模式选择 →「应用帮助」。
- **应用帮助 Agent 端点**：`POST /api/ai/app-help-agent-chat`。
- **说明书传递方式**：后端不再把 `directions-help.prompt.md` 原文放入请求上下文，只在 `context.focus_context.manual_absolute_path` 中传入说明书绝对路径，且说明书只读。
- **学习记录传递方式**：后端在 `context.focus_context.learning_log_absolute_path` 中传入应用帮助学习记录绝对路径。
- **持续学习约束**：应用帮助 Agent 可以在对话中学习稳定的软件操作、限制和排障步骤，但只能向学习记录文档追加增量内容，不允许删除、覆盖、重排或改写已有经验；正式说明书由管理员统一筛选维护。
- **新增 Prompt 文件**：`app-help-agent-identity.prompt.md`、`app-help-agent-cues.prompt.md`。

## P5.0 新增：AgentBuild 节点问答 Agent

P5.0 将「节点问答（Agent）」接入独立的 AgentBuild AI Agent 内核脚本。普通节点聊天仍走 NetMind 后端内置 Prompt；Agent 模式由后端调用 AgentBuild 的 `src.agent_kernel`，并把当前节点上下文、模型配置、Tool 权限记录和历史上下文传入内核。

- **前端入口**：知识卡片左侧 AI 浮窗 → 模式选择 →「节点问答（Agent）」。
- **脚本目录配置**：顶部导航栏「设置」→「AgentBuild 脚本设置」，默认 `G:\AAW+\NetMind\AgentBuild`。该目录下必须存在 `src/agent_kernel.py`。
- **后端端点**：`POST /api/ai/node-agent-chat`。
- **默认 Tool 领域**：Kernel API v2 使用 `domain=netmind`。
- **模型配置来源**：沿用全局默认 AI 模型。后端把选中模型转换为 AgentBuild 的 `model_config`，包含 `model_name`、`api_url`、`api_key`、`temperature`、`max_tokens`、`timeout`、`max_retries` 和 JSON 输出格式。
- **Prompt/身份配置**：Agent 身份和补充提示写在 `Config/AiCleanPrompts` 的 Agent Prompt 文件中，不硬编码在业务代码内。
- **权限交互**：AgentBuild 返回 `waiting_permission` 时，前端展示 Tool 权限确认按钮；用户允许或拒绝后，下一轮请求会带回 `confirmed_tool_calls` 与 `history_tool_calls`。

新增后端配置：

```json
{
  "App": {
    "BaseUrl": "http://127.0.0.1:5120"
  },
  "AiAgent": {
    "AgentBuildPath": "G:\\AAW+\\NetMind\\AgentBuild",
    "PythonExecutable": "py",
    "TimeoutSeconds": 120,
    "Temperature": 0.2,
    "MaxTokens": 4096,
    "MaxRetries": 2,
    "ToolRuntimeTimeoutSeconds": 10,
    "PromptFiles": {
      "NodeIdentity": "Config/AiCleanPrompts/node-agent-identity.prompt.md",
      "NodeCues": "Config/AiCleanPrompts/node-agent-cues.prompt.md",
      "MapIdentity": "Config/AiCleanPrompts/map-agent-identity.prompt.md",
      "MapCues": "Config/AiCleanPrompts/map-agent-cues.prompt.md",
      "GlobalIdentity": "Config/AiCleanPrompts/global-agent-identity.prompt.md",
      "GlobalCues": "Config/AiCleanPrompts/global-agent-cues.prompt.md",
      "AppHelpIdentity": "Config/AiCleanPrompts/app-help-agent-identity.prompt.md",
      "AppHelpCues": "Config/AiCleanPrompts/app-help-agent-cues.prompt.md"
    }
  }
}
```

注意：AgentBuild 当前真实模型调用使用 OpenAI-compatible Chat Completions 响应结构；Agent 模式暂不支持 Ollama `/api/chat`。

## P4.4 新增：全局默认模型切换

P4.4 将 AI 模型选择统一为全局默认模型，所有 AI 功能（AI 清洗、节点问答、全图问答、应用帮助等）共用一个全局选择。

- **选择入口**：顶部导航栏「设置」→「全局默认 AI 模型」
- **模型来源**：后端配置模型（`appsettings.json`）+ 前端自定义模型（`localStorage`）合并展示
- **内置模型 API Key 覆盖**：可为后端内置模型设置覆盖 API Key（替代环境变量），同样存储在浏览器 localStorage
- **自定义模型传参**：前端通过请求体的 `endpoint`、`provider`、`apiKey` 字段直传自定义模型配置到后端，后端动态构建临时模型实例
- **API Key 缺失提示**：当模型缺少 API Key 时，后端返回中文引导提示，指导用户在设置中配置

## P4.1 新增：前端自定义模型配置

P4.1 新增前端设置弹窗，支持用户通过浏览器界面自行配置 AI 模型，无需修改后端配置文件：

- **配置入口**：顶部导航栏"设置"按钮
- **配置内容**：模型名称、API 地址、API Key
- **存储方式**：API Key 仅存储在浏览器 `localStorage`，不提交到 Git 仓库，不发送到后端服务器
- **请求流程**：前端自定义模型的 API Key 通过请求体的 `ApiKey` 字段传递给后端，后端优先使用该 Key
- **上下文设置**：支持配置上下文长度（1K~1M，推荐 50K），当前为配置项暂未对接后端

后端配置（`appsettings.json`）与前端自定义模型可并存。前端自定义模型仅在前端使用，不影响后端服务端配置的模型。

## P3.0 配置目标

P3.0 对 AI 配置做安全和可维护性优化：模型参数仍由 `appsettings.json` / `appsettings.Development.json` 管理，真实 API Key 不再写入仓库；长 Prompt 迁移到独立中文文本配置文件，便于直接阅读、编辑和评审。

- 云模型优先：默认使用 DeepSeek 云接口。
- 本地模型备用：DeepSeek 不可用且未指定 `modelId` 时，回退到本机 Ollama。
- 模型配置来源：`src/NetMind.WebApi/appsettings.json` 与 `src/NetMind.WebApi/appsettings.Development.json`。
- 密钥来源：只通过环境变量读取，当前 DeepSeek 使用 `DEEPSEEK_API_KEY`。
- 提示词来源：`src/NetMind.WebApi/Config/AiCleanPrompts/*.prompt.md`。
- 上下文压缩：当用户上下文超过 `ContextCompressionThreshold` 时，先调用同一模型压缩上下文，再进入需求结构化提示词。

## 当前模型配置

`GET /api/ai/models` 返回配置文件中的模型列表。

| id | 名称 | provider | endpoint | 状态 | 说明 |
| --- | --- | --- | --- | --- | --- |
| `deepseek-cloud` | DeepSeek Cloud | `deepseek` | `https://api.deepseek.com/chat/completions` | `enabled` | 默认云模型，使用 OpenAI-compatible Chat Completions 格式。 |
| `ollama-local` / `ollama-local-qwen` | Ollama Local | `ollama` | `http://127.0.0.1:11434/api/chat` | `enabled` | 本地备用模型，需要本机 Ollama 已启动并拉取配置的模型。 |

默认选择规则：

- 未传 `modelId` 时，优先使用 `IsDefault=true` 的云模型 `deepseek-cloud`。
- 云模型请求失败且未指定 `modelId` 时，自动尝试本地 Ollama 模型。
- 传入明确 `modelId` 时只调用该模型；模型不存在、未启用或调用失败时直接返回错误。

## 配置结构

```json
{
  "AiClean": {
    "Prompt": {
      "ContextCompressionThreshold": 4000,
      "PromptFiles": {
        "System": "Config/AiCleanPrompts/system.prompt.md",
        "User": "Config/AiCleanPrompts/mind-map-clean.prompt.md",
        "Requirement": "Config/AiCleanPrompts/requirement-structure.prompt.md",
        "ContextChat": "Config/AiCleanPrompts/context-chat.prompt.md",
        "ContextCompression": "Config/AiCleanPrompts/context-compression.prompt.md"
      }
    },
    "Models": [
      {
        "Id": "deepseek-cloud",
        "Provider": "deepseek",
        "Endpoint": "https://api.deepseek.com/chat/completions",
        "Model": "deepseek-chat",
        "Enabled": true,
        "IsDefault": true,
        "ApiKeyEnvironmentVariable": "DEEPSEEK_API_KEY",
        "TimeoutSeconds": 60
      }
    ]
  }
}
```

后端仍保留旧的 `SystemPromptLines`、`UserPromptTemplateLines` 等数组读取作为兼容路径，但新配置优先使用 `PromptFiles`。

## Prompt 文件

| 配置键 | 文件 | 用途 |
| --- | --- | --- |
| `System` | `system.prompt.md` | 稳定系统约束，例如只返回 JSON、不要 Markdown 包裹。 |
| `User` | `mind-map-clean.prompt.md` | 自然语言清洗为标准导图结构。 |
| `Requirement` | `requirement-structure.prompt.md` | 将不成熟需求结合上下文拆解为结构化导图。 |
| `ContextChat` | `context-chat.prompt.md` | 需求澄清对话回复。 |
| `ContextCompression` | `context-compression.prompt.md` | 长上下文压缩。 |
| `NodeIdentity` | `node-agent-identity.prompt.md` | 节点问答 Agent 身份提示。 |
| `NodeCues` | `node-agent-cues.prompt.md` | 节点问答 Agent 补充提示。 |
| `MapIdentity` | `map-agent-identity.prompt.md` | 全图问答 Agent 身份提示。 |
| `MapCues` | `map-agent-cues.prompt.md` | 全图问答 Agent 补充提示。 |
| `GlobalIdentity` | `global-agent-identity.prompt.md` | 全局问答 Agent 身份提示。 |
| `GlobalCues` | `global-agent-cues.prompt.md` | 全局问答 Agent 补充提示。 |
| `AppHelpIdentity` | `app-help-agent-identity.prompt.md` | 应用帮助 Agent 身份提示。 |
| `AppHelpCues` | `app-help-agent-cues.prompt.md` | 应用帮助 Agent 补充提示。 |
| `AppHelpLearning` | `app-help-learning-log.md` | 应用帮助 Agent 学习记录，只允许追加增量经验。 |
| `AppHelpUsageTips` | `app-help-usage-tips.md` | 应用帮助 Agent 使用技巧，可用 `incremental_file_modifier` 增量维护。 |

Prompt 文件会随 `NetMind.WebApi` 构建复制到输出目录。发布包如果直接运行，也需要保留 `Config/AiCleanPrompts/` 目录。

## 提示词占位符规范

当前后端支持以下占位符：

- `{{schemaVersion}}`：输出结构版本，当前固定替换为 `netmind.mindmap.v1`。
- `{{naturalLanguage}}`：用户原始输入文本。
- `{{requirement}}`：用户不成熟、待拆解的需求文本。
- `{{context}}`：程序从本次对话记录中组装的上下文；当上下文过长时会替换为压缩摘要。
- `{{message}}`：对话弹窗中的最新一条用户消息。

约束：

- 未在服务端支持的占位符不会被替换。
- 模板中必须保留输出结构要求，否则模型可能返回不可导入的数据。
- 每个 Prompt 文件至少需要包含一行有效内容，否则服务调用会报配置错误。

## 密钥管理

真实 API Key 不允许写入仓库内任何 `appsettings*.json` 或发布配置文件。当前推荐做法：

```powershell
$env:DEEPSEEK_API_KEY="你的真实密钥"
```

生产环境应使用服务器环境变量、容器 Secret、CI/CD Secret 或部署平台密钥管理。由于此前仓库配置中出现过明文 Key，合并前建议在 GitHub / DeepSeek 控制台轮换该 Key，并检查远端历史记录是否需要清理。

## 提示词编写规范

1. 结构要求必须明确：必须要求返回 JSON，不允许 Markdown、解释性文本或前后缀。
2. 业务规则写在 Prompt 文件中：层级深度、节点数量、标题长度、内容粒度、关系类型建议都应在文本配置里维护。
3. System Prompt 只放稳定约束：不要把频繁调整的业务拆解规则写到系统约束里。
4. 模板按指令块分段：角色定义、输出格式、结构规则、质量规则、约束和用户输入区。
5. 不把密钥和提示词混写：API Key 只通过模型配置的环境变量名引用，Prompt 不写密钥、endpoint 或真实模型凭证。
6. 变更要求同步记录：修改 AI 配置或 Prompt 后，同步更新本文档和对应开发日志。

## 运行要求

- DeepSeek：运行服务前设置环境变量 `DEEPSEEK_API_KEY`。
- Ollama：本地启动 Ollama，并确认模型名与配置中的 `Model` 一致。
- AI 返回必须是 `netmind.mindmap.v1` JSON；后端会继续校验结构版本、节点、父子关系和关联端点。

## 数据库配置

数据库连接字符串通过 `PGSTR` 环境变量传入：

```powershell
$env:PGSTR="Host=127.0.0.1;Port=5432;Database=netmind;Username=postgres;Password=your_password;"
```

运行接口前需要先创建 PostgreSQL 数据库并执行：

```powershell
psql -d netmind -U postgres -f AI文档/SQL/Init.sql
```
