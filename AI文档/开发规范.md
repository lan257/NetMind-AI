# NetMind 开发规范

更新时间：2026-04-29

本文件是 AI 和开发者的开发约束文档。项目入口、文档用途看 `项目必读.md`；代码位置看 `项目结构速查.md`。

## 1. 必须遵守

### 1.1 AI 编码

1. AI 编写或修改代码后，必须写入对应阶段开发日志。
2. 每个大阶段单独维护开发日志，例如 `P0开发日志.md`、`P1开发日志.md`。
3. 子阶段结束后，在开发日志中给出建议分支名、commit 规范和 PR 说明，无需实际提交，每个子阶段字数不能超过300字。
4. 允许在同一子阶段内补充和整理内容，禁止跨子阶段随意改旧记录。
5. AI Prompt 必须写在配置文件中，不允许硬编码到业务代码。
6. AI 配置或 Prompt 变化后，必须同步更新 `AI大模型配置说明.md`。

### 1.2 受限文档

以下文档默认不允许擅自修改，除非用户明确要求，或连续重复三次要求修改：

- `项目必读.md`
- `项目基础说明.md`
- `项目研发说明.md`
- `开发规范.md`

### 1.3 后端分层

当前调用方向：

```text
WebApi -> Services -> Repository -> Models/Common
```

规则：

- Controller 只处理 HTTP 入参、出参、状态码和路由。
- Service 只处理业务编排、业务校验、导入导出、AI 调用流程。
- Repository 只处理数据库访问。
- Models 只放 DTO、Entity、ViewModel。
- Common 只放跨层通用结构或工具。

禁止：

- Controller 写业务逻辑。
- Controller 直接调用 Repository。
- Service 直接写 SQL。
- Repository 写业务判断。
- 跨层反向引用。

### 1.4 配置

必须放在配置文件或环境变量中：

- 数据库连接字符串。
- API Key。
- 外部模型地址。
- AI Prompt。
- 模型名称、超时、开关、默认模型设置。

配置入口：

- `src/NetMind.WebApi/appsettings.json`
- `src/NetMind.WebApi/appsettings.Development.json`

不建议把真实密钥写入仓库，优先使用环境变量，例如 `DEEPSEEK_API_KEY`。

### 1.5 数据库

- 当前数据库为 PostgreSQL。
- 表结构脚本放在 `AI文档/SQL/`。
- 查询默认排除逻辑删除数据。
- 删除优先使用逻辑删除。
- 新增或修改表结构时，必须同步 SQL 脚本和开发日志。

### 1.6 前端

- 当前前端为 Vue3 + Vite。
- Vue3 前端统一使用 Element Plus（Element UI 的 Vue3 版本）作为基础组件库，并引入 `@element-plus/icons-vue` 作为图标来源。
- 新增页面和组件优先使用 Element Plus 的按钮、输入框、弹窗、分段控制、选择器和消息类组件，项目自定义 CSS 只负责页面布局、业务区域和少量品牌化样式。
- 依赖版本必须写入 `package.json` 和 `package-lock.json`，不使用浮动版本；新增依赖后必须执行前端构建验证。
- `App.vue` 只负责页面级编排和全局状态接线，业务区域必须拆分到 `src/components/`，接口与状态复用逻辑优先放到 `src/services/` 和 `src/composables/`。
- 页面提示必须使用悬浮或弹出层样式，不允许插入到普通文档流中挤占列表、画布、表单等核心组件位置。
- API 交互应统一处理错误信息，避免非 JSON 异常直接打断页面。
- 新增关键交互时，保留或补充必要的 `data-testid`。

## 2. 可参考规范

以下规则不是硬性阻塞项，但新代码应尽量遵守。

### 2.1 命名

| 类型 | 建议 |
| --- | --- |
| 类、接口、属性、方法 | PascalCase |
| 局部变量、方法参数 | camelCase |
| 接口 | `I` + PascalCase，例如 `IMindMapService` |
| 异步方法 | `Async` 后缀 |
| Controller | `Controller` 后缀 |
| Service 实现 | `Service` 后缀 |
| Repository 实现 | `Repository` 后缀 |
| DTO | `Dto` 后缀或按现有文件风格命名 |
| ViewModel | `ViewModel` 后缀 |

命名应使用英文，避免拼音、无意义缩写、`tmp`、`data`、`obj` 这类含义模糊的名称。

### 2.2 API 路由

优先使用标准 HTTP 动词：

| 操作 | 动词 | 示例 |
| --- | --- | --- |
| 查询列表 | `GET` | `GET /api/mind-maps` |
| 查询单条 | `GET` | `GET /api/nodes/{id}` |
| 新增 | `POST` | `POST /api/nodes` |
| 修改 | `PUT` | `PUT /api/nodes/{id}` |
| 删除 | `DELETE` | `DELETE /api/nodes/{id}` |
| 导入 | `POST` | `POST /api/mind-map-transfer/structure` |
| 导出 | `GET` | `GET /api/mind-map-transfer/{mapId}/file` |

接口响应优先使用 `ApiResult` 包装。

### 2.3 依赖和异步

- 依赖通过构造函数注入。
- I/O 操作优先使用 `async/await`。
- NuGet/npm 依赖应显式版本，避免浮动版本。
- 不手动拷贝第三方 DLL 到项目中。

### 2.4 注释

- 复杂业务、校验规则、非显而易见的兼容逻辑应补充简短注释。
- 普通赋值、简单转发、明显命名能表达含义的代码不需要注释。

### 2.5 验证

常用命令：

```powershell
dotnet build src\NetMind.sln -c Release --no-restore -v minimal
dotnet run --project src\NetMind.IntegrationTests\NetMind.IntegrationTests.csproj -c Release --no-build
npm run build --prefix src\NetMind.Frontend
```

阶段冒烟脚本在：

```text
AI文档/Smoke/
```

如果本机缺少 PostgreSQL、DeepSeek Key 或 Ollama，导致无法完整验证，需要在开发日志中说明。

### 2.6 Git 和提交说明

分支名建议：

```text
codex/p阶段-功能名
```

commit 建议：

```text
feat: 新功能
fix: 修复问题
docs: 文档变更
style: 格式调整，不影响运行
refactor: 重构
test: 测试
chore: 构建或辅助工具
```

PR 说明建议包含：

- 完成内容。
- 影响范围。
- 验证结果。
- 已知问题或未验证原因。

### 2.7 版本号参考

如后续需要发布版本，优先使用语义化版本：

```text
Major.Minor.Patch[-Suffix]
```

示例：

- `1.0.0`
- `1.1.0-beta`
- `1.1.0-rc.1`

优先级参考：`alpha < beta < rc < 正式版`。
