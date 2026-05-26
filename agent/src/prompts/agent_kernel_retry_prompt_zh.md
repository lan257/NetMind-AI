# Agent Kernel 结构修复 Prompt 模板

上一轮模型返回未通过 Agent Kernel 结构校验。

## 校验错误
{{validation_error}}

## 上一轮原始返回
{{invalid_content}}

## 原始任务 Prompt
{{original_prompt}}

## 修复要求
请重新输出一个符合 Agent Kernel 输出协议的 JSON 对象。

必须满足：
1. 只输出一个 JSON 对象。
2. 不要输出 Markdown、代码块、解释文字或额外前后缀。
3. 必须包含 `agent_target`、`main_text`、`tool_call_drafts`、`context_update`。
4. `tool_call_drafts` 必须是数组，不需要调用 Tool 时返回空数组。
5. `tool_call_drafts[]` 中使用 `tool_id`，不要使用 `skill_id`。
6. `context_update` 必须包含 `working_memory` 对象和 `summary` 字符串。
7. 可选包含 `needs_continuation`，如果包含则必须是 boolean。
8. 不要添加 `permission`、`execution`、`model_config`、`api_key` 字段。
9. 如果错误信息包含 `Expecting ',' delimiter`、`Invalid control character`、`Unterminated string` 或“错误附近片段”，通常说明 `params.new_content` 中的多行文件内容没有正确转义；必须把真实换行改成 `\n`，双引号改成 `\"`，反斜杠改成 `\\`，不要裸写多行代码块。
10. 特殊符号必须正确转义，尤其是换行、双引号和反斜杠。
11. 如果错误信息来自 `line_modifier` 写入量过大，必须把修改拆成单次不超过 40 行、4000 字符的小块；不要改用整文件写入。
12. 修复时不要改变用户目标；只修正 JSON 语法、字段类型和参数转义。
