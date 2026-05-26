# 查询当前时间

## 简介
查询当前系统时间，返回年月日时分秒的详细信息。

## 功能描述
本 Tool 用于获取当前系统时间，并以中文格式返回年月日时分秒，同时提供年、月、日、时、分、秒的单独数值。

## 参数说明
无参数。

## 返回值
| 字段 | 类型 | 说明 |
|------|------|------|
| status | string | 执行状态：success/error |
| result.current_time | string | 当前时间（格式：YYYY年MM月DD日 HH时MM分SS秒） |
| result.year | int | 当前年份 |
| result.month | int | 当前月份 |
| result.day | int | 当前日期 |
| result.hour | int | 当前小时 |
| result.minute | int | 当前分钟 |
| result.second | int | 当前秒数 |
| error | string | 错误信息（仅当status为error时） |

## 使用示例
```python
result = main({})
print(result["result"]["current_time"])
# 输出示例：2024年01月15日 14时30分25秒
```

## 注意事项
- 时间基于系统本地时间
- 无需任何参数即可调用
