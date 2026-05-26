# 精准文件行数修改 (line_modifier)

## 功能说明
修改指定文件的指定行范围内容，适用于小块、精确修改文件特定行。

该 Tool 禁止用于整文件写入。单次最多修改 40 行，`new_content` 最多 4000 个字符。

## 参数说明
| 参数名 | 类型 | 必填 | 说明 |
|--------|------|------|------|
| filepath | string | 是 | 要修改的文件路径（绝对路径或工作空间相对路径）|
| start_line | number | 是 | 起始行号（从1开始） |
| end_line | number | 是 | 结束行号（包含），单次范围不超过 40 行 |
| new_content | string | 是 | 替换后的新内容，最多 40 行或 4000 个字符 |

## 返回值说明
成功返回：{"success": true, "total_lines_before": 修改前总行数, "total_lines_after": 修改后总行数, "modified_lines": (起始行, 结束行), "written_lines": 写入行数, "written_chars": 写入字符数}
失败返回：{"success": false, "error": "错误信息"}

## 使用示例
```python
params = {"filepath": "example.txt", "start_line": 5, "end_line": 8, "new_content": "替换后的内容"}
result = run(params)
print(result)
```

## 异常说明
- 文件不存在：返回 FileNotFoundError
- 行号超出范围：返回错误信息
- 单次写入超过 40 行或 4000 字符：返回错误信息
- 写入权限问题：返回 PermissionError
- 编码问题：返回 UnicodeDecodeError（默认使用 utf-8）

## 维护注意事项
- 修改操作直接覆写原文件，建议先备份
- 文件编码默认 UTF-8，如有其他编码需修改 run.py
- 大范围修改必须拆分为多次小块修改
