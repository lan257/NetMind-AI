# create_folder - 创建文件夹

## 功能说明

在指定路径创建新文件夹，支持递归创建多级目录（类似 `mkdir -p`）。

## 参数说明

| 参数 | 类型 | 必需 | 默认值 | 说明 |
|------|------|------|--------|------|
| `folder_path` | string | ✅ | - | 要创建的文件夹路径，支持 `~` 和相对路径 |
| `exist_ok` | boolean | ❌ | false | 如果文件夹已存在是否视为成功 |

## 返回值说明

```json
{
  "success": true,
  "folder_path": "/home/user/new_folder",
  "created": true,
  "error": null
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `success` | boolean | 是否执行成功 |
| `folder_path` | string | 规范化后的绝对路径 |
| `created` | boolean | 是否为新创建（false 表示已存在且 exist_ok=true） |
| `error` | string | 错误信息，成功时为 null |

## 使用示例

### 创建单级目录
```json
{
  "folder_path": "./my_project"
}
```

### 递归创建多级目录
```json
{
  "folder_path": "./my_project/src/utils"
}
```

### 目录已存在时不报错
```json
{
  "folder_path": "./existing_folder",
  "exist_ok": true
}
```

### 使用绝对路径
```json
{
  "folder_path": "/home/user/projects/new_project"
}
```

## 安全限制

| 限制 | 说明 |
|------|------|
| 系统目录 | 禁止在 `/etc`, `/usr`, `/bin`, `/sbin`, `/boot`, `/dev`, `/proc`, `/sys` 下创建 |

## 异常说明

| 场景 | 返回值 |
|------|--------|
| 缺少 `folder_path` | `success: false`，`error: "缺少必需参数: folder_path"` |
| 路径已存在但不是文件夹 | `success: false`，`error: "路径已存在但不是文件夹: xxx"` |
| 文件夹已存在且 `exist_ok=false` | `success: false`，`error: "文件夹已存在: xxx"` |
| 权限不足 | `success: false`，`error: "权限不足，无法创建文件夹: xxx"` |
| 系统目录限制 | `success: false`，`error: "安全限制：禁止在系统目录下创建文件夹"` |
| 其他错误 | `success: false`，`error: "创建文件夹失败: {原因}"` |

## 维护注意事项

1. **安全第一**：硬编码禁止写入系统目录，后续可根据需求配置白名单
2. **路径规范化**：使用 `expanduser().resolve()` 处理 `~` 和相对路径，防止路径遍历
3. **原子性**：使用 `path.mkdir(parents=True)` 一次调用完成多级创建
4. **权限继承**：新目录会继承当前进程的 umask 权限设置
5. **跨平台**：Path 对象自动处理 Windows/Unix 路径差异