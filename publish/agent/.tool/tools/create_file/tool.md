# create_file - 创建文件

## 功能说明

在指定路径创建新文件，支持写入内容，自动创建父目录，可选择是否覆盖已存在的文件。

## 参数说明

| 参数 | 类型 | 必需 | 默认值 | 说明 |
|------|------|------|--------|------|
| `file_path` | string | ✅ | - | 要创建的文件路径，支持 `~` 和相对路径 |
| `content` | string | ❌ | "" | 文件内容 |
| `encoding` | string | ❌ | "utf-8" | 文件编码格式 |
| `overwrite` | boolean | ❌ | false | 如果文件已存在是否覆盖 |

## 返回值说明

```json
{
  "success": true,
  "file_path": "/home/user/my_file.txt",
  "action": "created",
  "size": 1024,
  "error": null
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `success` | boolean | 是否执行成功 |
| `file_path` | string | 规范化后的绝对路径 |
| `action` | string | 操作类型：`'created'` / `'overwritten'` / `'skipped'` / `'error'` |
| `size` | integer | 文件大小（字节） |
| `error` | string | 错误信息，成功时为 null |

## 使用示例

### 创建空文件
```json
{
  "file_path": "./README.md"
}
```

### 创建并写入内容
```json
{
  "file_path": "./hello.txt",
  "content": "Hello, World!"
}
```

### 创建多行内容
```json
{
  "file_path": "./script.py",
  "content": "#!/usr/bin/env python3\n\ndef main():\n    print('Hello')\n\nif __name__ == '__main__':\n    main()"
}
```

### 覆盖已存在的文件
```json
{
  "file_path": "./config.json",
  "content": "{\"version\": \"2.0\"}",
  "overwrite": true
}
```

### 指定编码
```json
{
  "file_path": "./data.txt",
  "content": "中文内容",
  "encoding": "utf-8"
}
```

## 安全限制

| 限制类型 | 说明 |
|----------|------|
| 系统目录 | 禁止在 `/etc`, `/usr`, `/bin`, `/sbin`, `/boot`, `/dev`, `/proc`, `/sys` 下操作 |
| 关键文件 | 禁止操作 `/etc/passwd`, `/etc/shadow`, `/etc/sudoers` |

## 异常说明

| 场景 | 返回值 |
|------|--------|
| 缺少 `file_path` | `success: false`，`error: "缺少必需参数: file_path"` |
| 文件已存在且 `overwrite=false` | `success: false`，`error: "文件已存在且 overwrite=false: xxx"` |
| 权限不足（父目录） | `success: false`，`error: "权限不足，无法创建父目录: xxx"` |
| 权限不足（写入） | `success: false`，`error: "权限不足，无法写入文件: xxx"` |
| 编码错误 | `success: false`，`error: "编码错误: 内容无法使用 {encoding} 编码"` |
| 系统目录限制 | `success: false`，`error: "安全限制：禁止在系统目录下操作文件"` |
| 系统关键文件 | `success: false`，`error: "安全限制：禁止操作系统关键文件"` |

## 维护注意事项

1. **安全第一**：禁止写入系统目录和关键文件
2. **原子写入**：虽然当前实现是直接写入，但考虑了将来改为临时文件+重命名
3. **父目录自动创建**：使用 `path.parent.mkdir(parents=True, exist_ok=True)`
4. **编码处理**：默认 UTF-8，遇到编码错误时给出明确提示
5. **内容类型**：`content` 应为字符串，如需二进制内容需扩展 Tool
6. **大文件**：当前实现将整个内容加载到内存，超大文件场景需考虑流式写入
7. **权限继承**：新文件的权限由系统 umask 决定
