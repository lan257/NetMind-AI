# 精准文件行数读取 (line_reader)

## 功能说明
读取指定文件的指定行范围内容。支持按行号范围读取，当不指定行号时读取全部内容。适用于大文件遍历或精确获取特定行。

## 参数说明
| 参数名 | 类型 | 必填 | 说明 |
|--------|------|------|------|
| filepath | string | 是 | 要读取的文件路径（绝对路径或工作空间相对路径）|
| start_line | number | 否 | 起始行号（从1开始），不指定则从文件开头读取 |
| end_line | number | 否 | 结束行号（包含），不指定则读取到文件末尾 |

## 返回值说明
成功返回：{"success": true, "content": "读取到的文件内容", "total_lines": 文件总行数, "start_line": 起始行号, "end_line": 结束行号}
失败返回：{"success": false, "error": "错误信息"}

## 使用示例
```python
params = {"filepath": "example.txt", "start_line": 5, "end_line": 10}
result = run(params)
print(result["content"])
```

## 异常说明
- 文件不存在：返回 FileNotFoundError
- 行号超出范围：返回 IndexError
- 编码问题：返回 UnicodeDecodeError（默认使用 utf-8）

## 维护注意事项
- 大文件建议配合 start_line/end_line 分段读取，避免内存溢出
- 文件编码默认 UTF-8，如有其他编码需修改 run.py