# AI 大模型配置说明

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

数据库连接字符串位于配置文件，生产环境建议同样使用环境变量覆盖：

```powershell
$env:ConnectionStrings__Postgres="Host=127.0.0.1;Port=5432;Database=netmind;Username=postgres;Password=your_password;"
```

运行接口前需要先创建 PostgreSQL 数据库并执行：

```powershell
psql -d netmind -U postgres -f AI文档/SQL/Init.sql
```