# NetMind

NetMind 是一个基于 AI 的知识网络构建与可视化工具，用于把文本、文档或需求整理为标准结构，并以思维导图/知识图谱形式存储、展示和编辑。

## 当前阶段

当前项目处于 P2 收尾阶段。P0 已完成数据闭环 Demo，P1 已完成 AI 结构化能力，P2 已完成前端产品形态重构、Canvas 思维导图、响应式优化、图上编辑、知识卡片 Markdown 展示、两层关联图谱和节点排序校验。

## 技术栈

| 层级 | 技术 |
| --- | --- |
| 后端 | .NET 8 Web API |
| 数据库 | PostgreSQL |
| 前端 | Vue 3 + Vite + Element Plus |
| AI | DeepSeek Cloud / Ollama Local，可通过配置切换 |

## 本地启动

### 1. 准备数据库

1. 安装并启动 PostgreSQL。
2. 创建 `netmind` 数据库。
3. 执行 `AI文档/SQL/Init.sql`。
4. 如需从旧库升级，按文件名顺序执行 `AI文档/SQL/P*.sql` 迁移脚本。

运行前通过 `PGSTR` 环境变量提供完整连接字符串：

```powershell
$env:PGSTR="Host=localhost;Port=5432;Database=netmind;Username=postgres;Password=admin;"
```

### 2. 安装前端依赖

```powershell
cd src\NetMind.Frontend
npm install
```

### 3. 启动后端

```powershell
$env:PGSTR="Host=localhost;Port=5432;Database=netmind;Username=postgres;Password=admin;"
$env:DEEPSEEK_API_KEY="你的 DeepSeek API Key"
dotnet run --project src\NetMind.WebApi\NetMind.WebApi.csproj
```

开发环境下后端会尝试自动启动前端开发服务。访问：

```text
http://localhost:5173
```

接口文档：

```text
http://localhost:5119/swagger
```

## 构建验证

```powershell
dotnet build src\NetMind.sln -c Release --no-restore -v minimal
npm run build --prefix src\NetMind.Frontend
```

## 部署

部署步骤见 `AI文档/部署文档.md`。

## 开发文档

开发前先阅读：

- `AI文档/项目必读.md`
