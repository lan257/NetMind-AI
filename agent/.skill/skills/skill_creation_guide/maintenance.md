# 维护说明

- Skill 是 prompt-only 能力，不要把 `run.py`、权限配置或可执行参数放进 `.skill`。
- 需要执行动作时，新建或修改 `.tool` 下的 Tool。
- `workflow.md` 只保存文本流程，不保存 JSON 对象或脚本实现。
- `skill_definition.json` 与角色列表中的同一条目必须同步。
- 修改字段结构时，同步检查加载器、校验逻辑、管理 UI 和测试。
- 常见错误：把 Tool 的 `tool_definition.json`、`run.py`、`tool.md` 三件套复制成 Skill；这种情况应迁移到 `.tool`。
