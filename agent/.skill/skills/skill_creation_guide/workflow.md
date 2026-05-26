# Skill 创建流程

1. 先判断用户要的是 Skill 还是 Tool。
2. 如果能力需要执行代码、读写文件、调用接口、访问外部系统或返回运行结果，停止创建 Skill，改用 Tool 创建流程。
3. 如果能力只用于补充 AI 的提示、方法、工作流、检查标准或长期行为约束，创建 prompt-only Skill。
4. 为 Skill 选择稳定的 `skill_id`，目录为 `.skill/skills/<skill_id>/`。
5. 一次性准备三件套内容：`skill_definition.json`、`workflow.md`、`maintenance.md`。
6. 在 `skill_definition.json` 中写清 `skill_id`、`skill_name`、`description`、`trigger`、`prompt`、`workflow_path`、`maintenance_path`、`category`、`tags`。
7. 在 `workflow.md` 中写 Agent 使用该 Skill 时的步骤，不写脚本代码。
8. 在 `maintenance.md` 中写修改边界、同步规则、校验方法和禁止事项。
9. 将同一份定义注册到 `.skill/lists/<role>.yaml`，必要时更新 `.skill/domain_skill_bindings.json`。
10. 校验 `.skill` 中不存在 `run.py`，且定义没有 Tool 专用字段。
11. 输出创建结果、注册位置和剩余风险。
