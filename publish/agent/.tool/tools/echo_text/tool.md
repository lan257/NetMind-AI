# echo_text

## ToolDefinition
该 Tool 的规范定义维护在同目录的 `tool_definition.json` 中；角色列表里的登记必须与该 JSON 完全一致。

```yaml
tool_id: echo_text
tool_name: 文本回显
description: 返回输入文本，用于验证无需权限 Tool 的执行链路
trigger: 当需要测试无需权限 Tool 执行流程时使用
params:
  text: string
permission_level: none
permission_message: 无需权限
script_path: .tool/tools/echo_text/run.py
```

## 功能
返回传入的文本内容。

## 参数
- `text`: 需要回显的文本。

## 返回值
- `text`: 回显文本。

## 维护说明
该 Tool 主要用于验证 Kernel 对 `permission_level=none` 的处理，不承载业务逻辑。

