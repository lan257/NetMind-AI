# Skill 修改指南

## 功能说明

本 Skill 指导 Agent 修改已有 prompt-only Skill。修改目标是让提示、流程、维护说明和角色列表保持一致，同时守住 Skill 与 Tool 的边界。

## 触发场景

- 用户要求修改已有 Skill 的提示或触发条件。
- 用户要求调整 Skill 的工作流、维护说明、分类或标签。
- 用户指出 Skill 与角色列表不一致。
- 用户想把某个 Skill 从“可执行能力”纠正为 prompt-only 能力。

## 修改规则

- 修改前读取 `skill_definition.json`、`workflow.md`、`maintenance.md` 和引用该 Skill 的 `.skill/lists/*.yaml`。
- 只要修改了定义字段，就同步更新角色列表里的同一条目。
- 如果用户新增的是执行动作、参数、权限或脚本逻辑，应转为 Tool 修改或 Tool 创建。
- 不要在 Skill 目录新增 `run.py`。
- 不要在 Skill 定义中加入 Tool 字段。

## 基本流程

1. 定位目标 Skill 目录和角色列表引用。
2. 读取三件套，理解当前触发条件、提示目标、流程和维护边界。
3. 判断用户变更是否仍属于 prompt-only。
4. 修改受影响文件。
5. 同步 `.skill/lists/<role>.yaml` 中的定义。
6. 校验三件套完整、定义一致、无可执行字段、无 `run.py`。
7. 输出修改摘要和剩余风险。

## 验收清单

- `skill_definition.json` 与角色列表一致。
- `workflow.md` 与 `prompt` 的目标一致。
- `maintenance.md` 反映新的维护边界。
- `.skill/skills/<skill_id>/` 下没有 `run.py`。
- 没有把 Tool 的参数、权限或脚本路径写入 Skill。
