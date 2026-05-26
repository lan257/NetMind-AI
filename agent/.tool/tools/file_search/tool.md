# 文件查找 Tool

## 功能说明
在指定目录中搜索文件名匹配模式的文件，支持递归搜索。

## 参数说明
| 参数 | 类型 | 说明 |
| --- | --- | --- |
| search_dir | string | 要搜索的目录路径 |
| pattern | string | 文件名匹配模式，如 "*.txt" |
| recursive | boolean | 是否递归搜索子目录 |

## 返回值
| 字段 | 类型 | 说明 |
| --- | --- | --- |
| files | list | 匹配文件的完整路径列表 |
| count | int | 匹配文件数量 |
| success | bool | 是否执行成功 |

## 使用示例
```python
run({"search_dir": "/path/to/search", "pattern": "*.py", "recursive": True})
```

## 异常说明
- 如果 `search_dir` 不存在，返回 error 信息。

## 维护注意事项
- 依赖 `os` 和 `fnmatch` 标准库。
- 注意路径分隔符在不同操作系统下的差异。
