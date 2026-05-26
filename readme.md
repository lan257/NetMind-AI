# NetMind

NetMind 是一个基于 AI 的知识网络构建与可视化工具，用于把文本、文档或需求整理为标准结构，并以思维导图/知识图谱形式存储、展示和编辑。

本仓库主要面向开发者，提供可二次开发的源码、数据库脚本和本地开发说明。普通用户建议使用项目发布页提供的安装包和使用方法。

## 功能概览

- 文本、文档或需求内容的 AI 结构化整理
- 思维导图和知识节点的创建、编辑、删除、导入、导出
- Canvas 思维导图展示、节点拖拽、缩放和平移
- 知识卡片、Markdown 内容展示和节点关联图谱
- 节点问答、全图问答、全局问答和应用帮助等 AI Agent 入口
- PostgreSQL 持久化存储

## 当前状态

当前源码已完成 P5 AI Agent 基础接入，P6 稳定维护与 Agent 能力补全仍在推进中。Agent 功能依赖外部 AgentBuild 脚本目录；未配置时，普通导图、节点、数据库和基础 AI 清洗开发仍可进行。

## 技术栈

| 层级 | 技术 |
| --- | --- |
| 后端 | .NET 8 Web API |
| 数据库 | PostgreSQL |
| 前端 | Vue 3 + Vite + Element Plus |
| AI | DeepSeek Cloud / Ollama Local，可通过配置切换 |
| Agent | 外部 AgentBuild 脚本目录 |

## 开发环境

请先安装：

- .NET 8 SDK
- Node.js 18+
- PostgreSQL 12+
- Python 3.10+（仅 Agent 功能需要）
- 可选：Ollama，本地模型调试时使用

## 本地开发启动

### 1. 准备数据库

创建数据库：

```sql
CREATE DATABASE netmind;
```

执行初始化脚本：

```powershell
psql -h localhost -p 5432 -U postgres -d netmind -f "AI文档/SQL/Init.sql"
```

如果需要从旧库升级，按文件名顺序执行 `AI文档/SQL/P*.sql` 迁移脚本。

运行后端前，通过 `PGSTR` 环境变量提供 PostgreSQL 连接字符串：

```powershell
$env:PGSTR="Host=localhost;Port=5432;Database=netmind;Username=postgres;Password=your_password;"
```

### 2. 安装前端依赖

```powershell
cd src\NetMind.Frontend
npm install
cd ..\..
```

### 3. 配置 AI 模型

使用 DeepSeek Cloud 时，配置 API Key：

```powershell
$env:DEEPSEEK_API_KEY="你的 DeepSeek API Key"
```

使用 Ollama Local 时，请先启动 Ollama 服务，并确认 `src/NetMind.WebApi/appsettings*.json` 中配置的模型名称已在本机拉取。

真实 API Key 不应写入仓库。开发时优先使用环境变量，或在前端设置页为本机浏览器配置自定义模型。

### 4. 配置 AgentBuild

Agent 功能需要外部 AgentBuild 目录，目录内至少应包含：

```text
AgentBuild/
└── src/
    └── agent_kernel.py
```

应用前端可以自行配置 AgentBuild 路径：顶部「设置」→「AgentBuild 脚本设置」。仓库中的默认路径只是开发者本机示例，首次运行时请改成你自己的 AgentBuild 根目录。

后端默认使用 `py` 启动 Python。如果你的机器没有 Windows Python Launcher，或者 `py` 无法找到正确的 Python 版本，请选择其中一种方式处理：

- 将 Python 加入 `PATH`，并确保 `py` 或 `python` 可用。
- 修改 `src/NetMind.WebApi/appsettings.json` 和 `src/NetMind.WebApi/appsettings.Development.json` 中的 `AiAgent:PythonExecutable`，填入你的 `python.exe` 绝对路径。

### 5. 启动后端

```powershell
dotnet run --project src\NetMind.WebApi\NetMind.WebApi.csproj
```

开发环境下，后端会尝试自动启动前端开发服务。默认访问地址：

```text
http://localhost:5173
```

接口文档：

```text
http://localhost:5120/swagger
```

如果端口被占用，可以通过环境变量调整后端监听地址：

```powershell
$env:ASPNETCORE_URLS="http://127.0.0.1:5119"
dotnet run --project src\NetMind.WebApi\NetMind.WebApi.csproj
```

## 常用开发命令

后端构建：

```powershell
dotnet build src\NetMind.sln -c Release -v minimal
```

前端测试：

```powershell
npm run test --prefix src\NetMind.Frontend
```

前端构建：

```powershell
npm run build --prefix src\NetMind.Frontend
```

完整发布产物构建脚本可参考：

```powershell
.\build.ps1
```

## 目录说明

| 路径 | 说明 |
| --- | --- |
| `src/NetMind.sln` | .NET 解决方案入口 |
| `src/NetMind.WebApi/` | 后端 Web API、配置、Swagger、Prompt 文件 |
| `src/NetMind.Services/` | 业务逻辑和 AI/Agent 调用编排 |
| `src/NetMind.Repository/` | PostgreSQL 数据访问 |
| `src/NetMind.Models/` | DTO、实体和 ViewModel |
| `src/NetMind.Common/` | 通用响应和日志抽象 |
| `src/NetMind.Frontend/` | Vue 3 前端项目 |
| `AI文档/SQL/` | 数据库初始化和迁移脚本 |
| `AI文档/项目必读.md` | 项目文档入口 |

## 配置说明

主要配置文件：

- `src/NetMind.WebApi/appsettings.json`
- `src/NetMind.WebApi/appsettings.Development.json`

常用环境变量：

| 变量 | 说明 |
| --- | --- |
| `PGSTR` | PostgreSQL 完整连接字符串，后端运行必需 |
| `DEEPSEEK_API_KEY` | DeepSeek Cloud API Key，使用 DeepSeek 时需要 |
| `ASPNETCORE_URLS` | 可选，覆盖后端监听地址 |

## 开发文档

开发前建议先阅读：

- `AI文档/项目必读.md`
- `AI文档/开发规范.md`
- `AI文档/项目/项目结构速查.md`
- `AI文档/项目/AI大模型配置说明.md`

## 普通用户

普通用户无需拉取源码或自行部署。请使用项目发布页提供的安装包，并按对应版本的使用说明运行。
