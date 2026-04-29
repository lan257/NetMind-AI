# AI 大模型配置说明

## P1.2 配置目标

P1.2 开始移除本地模拟清洗，AI 清洗统一通过配置文件中的真实模型接口完成。

- 云模型优先：默认使用 DeepSeek 云接口。
- 本地模型备用：DeepSeek 不可用且未指定 `modelId` 时，回退到本机 Ollama。
- 配置来源：`src/NetMind.WebApi/appsettings.json` 与 `appsettings.Development.json`。
- 密钥来源：优先使用环境变量，不建议将密钥明文写入仓库配置。
- 提示词来源：统一放在 `AiClean:Prompt` 配置段，服务代码只负责读取、替换占位符和发起请求。

## 当前模型配置

`GET /api/ai/models` 返回配置文件中的模型列表。

| id | 名称 | provider | endpoint | 状态 | 说明 |
| --- | --- | --- | --- | --- | --- |
| `deepseek-cloud` | DeepSeek Cloud | `deepseek` | `https://api.deepseek.com/chat/completions` | `enabled` | 默认云模型，使用 OpenAI-compatible Chat Completions 格式。 |
| `ollama-local` | Ollama Local | `ollama` | `http://localhost:11434/api/chat` | `enabled` | 本地备用模型，需要本机 Ollama 已启动并拉取配置的模型。 |

默认选择规则：

- 未传 `modelId` 时，优先使用 `IsDefault=true` 的云模型 `deepseek-cloud`。
- 云模型请求失败且未指定 `modelId` 时，自动尝试本地 `ollama-local`。
- 传入明确 `modelId` 时只调用该模型；模型不存在、未启用或调用失败时直接返回错误。

## 配置结构

```json
{
  "AiClean": {
    "Prompt": {
      "SystemPromptLines": [
        "Return strict JSON only.",
        "Do not wrap the response in markdown."
      ],
      "UserPromptTemplateLines": [
        "You are an expert in knowledge structuring and concept modeling.",
        "User text:",
        "{{naturalLanguage}}"
      ]
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
      },
      {
        "Id": "ollama-local",
        "Provider": "ollama",
        "Endpoint": "http://localhost:11434/api/chat",
        "Model": "Qwen3.5 27b",
        "Enabled": true,
        "TimeoutSeconds": 120
      }
    ]
  }
}
```

## 提示词占位符规范

当前后端支持以下占位符：

- `{{schemaVersion}}`
  用于输出结构版本，当前固定替换为 `netmind.mindmap.v1`。
- `{{naturalLanguage}}`
  用于插入用户原始输入文本。

约束：

- 未在服务端支持的占位符不会被替换。
- 模板中必须保留输出结构要求，否则模型可能返回不可导入的数据。
- `SystemPromptLines` 与 `UserPromptTemplateLines` 至少各配置一行，否则服务启动后调用会报配置错误。

## 提示词编写规范

### 1. 结构要求必须明确

- 必须明确返回 JSON，不允许 Markdown、解释性文本或前后缀。
- 必须明确输出 schema，至少包含 `schemaVersion`、`title`、`nodes`、`relations`。
- 必须明确 `clientId`、`parentClientId`、`sourceClientId`、`targetClientId` 的引用一致性要求。

### 2. 业务规则写在用户模板中

- 层级深度限制、节点数量范围、标题长度、内容粒度、关系类型建议，都应写在 `UserPromptTemplateLines`。
- 不要把这些业务规则散落到服务代码里。

### 3. System Prompt 只放稳定约束

- 适合放模型角色、响应格式、禁止 Markdown 这类稳定要求。
- 不要把频繁调整的业务拆解规则写到 `SystemPromptLines`。

### 4. 模板按“指令块”分段

建议顺序：

- 角色定义
- 输出格式
- 结构化规则
- 质量规则
- 约束和禁止项
- 用户输入区

这样便于后续人工审核和按段调整。

### 5. 一行只表达一个要求

- 配置采用 `...Lines` 数组，每一行应尽量保持单一语义。
- 这样更利于 diff 审查、阶段调优和问题回溯。

### 6. 不把密钥和提示词混写

- API Key 只放模型配置。
- 提示词只放 `Prompt` 配置。
- 不要把密钥、endpoint、模型名称写进提示词文本。

### 7. 变更要求同步记录

每次修改 AI 提示词时，至少同步更新：

- `src/NetMind.WebApi/appsettings.json`
- `src/NetMind.WebApi/appsettings.Development.json`
- `/AI文档/AI大模型配置说明.md`
- `/AI文档/开发日志.md`

## 运行要求

- DeepSeek：运行服务前设置环境变量 `DEEPSEEK_API_KEY`，或在本地开发时按需配置 `ApiKey`。
- Ollama：本地启动 Ollama，并确认模型名与配置中的 `Model` 一致。
- AI 返回必须是 `netmind.mindmap.v1` JSON；后端会继续校验结构版本、节点、父子关系和关联端点。

## 数据库配置

数据库连接字符串同样位于配置文件：

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=netmind;Username=postgres;Password=xxx;"
  }
}
```

运行接口前需要先创建 PostgreSQL 数据库并执行：

```powershell
psql -d netmind -U postgres -f AI文档/SQL/Init.sql
```

P1.2 后端接口只连接 PostgreSQL，不再使用本地内存种子数据。
