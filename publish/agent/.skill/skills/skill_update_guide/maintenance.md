# 维护说明

- 更新 Skill 时不要只改角色列表；目录内的 `skill_definition.json` 是 canonical 定义来源。
- `workflow.md` 不应保存脚本代码或 JSON 配置，只保存纯文本流程。
- 如果用户实际要求新增可执行能力，应改为创建或修改 `.tool` 中的 Tool。
- 每次修改后校验 `.skill` 中不存在 `run.py`。
- 新增标签或分类时避免逗号进入单个标签值，保持列表序列化稳定。
- 修改触发条件时注意与 Tool 创建、Tool 修改指南的触发范围不要重叠。
