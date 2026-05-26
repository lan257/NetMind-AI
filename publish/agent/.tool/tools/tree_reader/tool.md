# tree_reader - 目录树读取

## 功能说明

读取指定目录的树形结构，返回格式化的目录树文本，用于快速了解项目结构、文件组织等场景。

## 参数说明

| 参数 | 类型 | 必需 | 默认值 | 说明 |
|------|------|------|--------|------|
| `target_path` | string | ✅ | - | 目标目录路径，支持 `~` 和相对路径 |
| `max_depth` | number | ❌ | 3 | 最大遍历深度，防止过深递归 |
| `ignore_hidden` | boolean | ❌ | true | 是否忽略以 `.` 开头的隐藏文件/文件夹 |
| `ignore_patterns` | array | ❌ | [`.git`, `__pycache__`, `.pyc`, `.DS_Store`] | 忽略的模式列表（包含即忽略） |

## 返回值说明

```json
{
  "success": true,
  "tree": "/home/user/project\n├── src\n│   ├── main.py\n│   └── utils.py\n└── README.md",
  "target_path": "/home/user/project",
  "total_count": 4,
  "error": null
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `success` | boolean | 是否执行成功 |
| `tree` | string | 树形格式的目录结构文本 |
| `target_path` | string | 规范化后的绝对路径 |
| `total_count` | integer | 遍历到的总条目数（不含根目录） |
| `error` | string | 错误信息，成功时为 null |

## 使用示例

### 基础用法
```json
{
  "target_path": "/home/user/my_project"
}
```

### 指定深度
```json
{
  "target_path": "/home/user/my_project",
  "max_depth": 5
}
```

### 显示隐藏文件
```json
{
  "target_path": "/home/user/my_project",
  "ignore_hidden": false
}
```

### 自定义忽略模式
```json
{
  "target_path": "/home/user/my_project",
  "ignore_patterns": [".git", "node_modules", "dist", ".env"]
}
```

## 异常说明

| 场景 | 返回值 |
|------|--------|
| 路径不存在 | `success: false`，`error: "路径不存在: xxx"` |
| 路径不是目录 | `success: false`，`error: "路径不是目录: xxx"` |
| 缺少 `target_path` | `success: false`，`error: "缺少必需参数: target_path"` |
| 权限不足访问子目录 | 对应位置显示 `[权限不足]`，不影响其他部分 |
| 其他异常 | `success: false`，`error: "生成目录树失败: {原因}"` |

## 输出示例

```
/home/user/my_project
├── .git
│   ├── HEAD
│   └── config
├── README.md
├── src
│   ├── main.py
│   └── utils.py
└── tests
    ├── test_main.py
    └── test_utils.py
```

## 维护注意事项

1. **安全考虑**：返回路径时会调用 `.resolve()` 规范化，防止路径遍历攻击
2. **大目录处理**：默认 `max_depth=3` 防止递归过深，需更大范围时由调用方指定
3. **权限处理**：遇到权限不足的目录不会中断，而是显示 `[权限不足]` 标记
4. **排序规则**：目录优先，然后文件，均为字母顺序
5. **性能**：对于超大目录（数万文件），需注意性能问题，建议调用方控制深度