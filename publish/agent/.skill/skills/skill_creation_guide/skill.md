# Skill 创建指南

## 功能说明

本 Skill 指导 Agent 创建 prompt-only Skill。Skill 是给模型看的长期方法、流程和约束，不负责执行代码，也不直接读写文件或调用外部系统。

Skill 与 Tool 的边界：

| 类型 | 存放位置 | 文件结构 | 适用场景 |
| --- | --- | --- | --- |
| Skill | `.skill/skills/<skill_id>/` | `skill_definition.json`、`workflow.md`、`maintenance.md` | 提示规范、工作流、判断标准、维护方法 |
| Tool | `.tool/tools/<tool_id>/` | `tool_definition.json`、`run.py`、`tool.md` | 可执行动作、文件操作、接口调用、脚本运行 |

## 触发场景

- 用户要创建新的 Skill。
- 用户要把经验沉淀成可复用流程。
- 用户要补充 Agent 的长期行为规范。
- 用户描述的是“如何做”，而不是“执行一个动作并返回结果”。

## 创建规则

- Skill 目录内不得包含 `run.py`。
- Skill 定义不得包含 `params`、`parameters`、`permission`、`permission_level`、`script_path`、`runner` 等 Tool 字段。
- `workflow.md` 写操作流程，保持纯文本。
- `maintenance.md` 写维护边界、校验方法和常见误区。
- `skill_definition.json` 与 `.skill/lists/<role>.yaml` 中的条目必须一致。

## 基本流程

1. 判断需求是否是 prompt-only；如果需要执行能力，改用 Tool 创建指南。
2. 设计稳定的 `skill_id`，使用小写字母、数字和下划线。
3. 编写 `skill_definition.json`，重点描述触发条件和核心提示。
4. 编写 `workflow.md`，列出 Agent 实际遵循的步骤。
5. 编写 `maintenance.md`，说明后续修改时要同步检查哪些内容。
6. 将 Skill 注册到 `.skill/lists/<role>.yaml`。
7. 校验目录、定义文件、角色列表和 prompt-only 边界。

## 验收清单

- 目录名与 `skill_id` 一致。
- 三件套完整：`skill_definition.json`、`workflow.md`、`maintenance.md`。
- 角色列表里的定义与目录内定义一致。
- 没有 `run.py` 和可执行字段。
- 触发条件不会与 Tool 创建或 Tool 修改混淆。
