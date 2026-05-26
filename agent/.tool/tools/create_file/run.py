"""
创建文件 Skill

在指定路径创建新文件，支持写入内容或创建空文件
"""

import os
from pathlib import Path
from typing import Any


def run(params: dict[str, Any]) -> dict[str, Any]:
    """
    执行创建文件操作

    Args:
        params: 参数字典
            - file_path: 文件路径（必需）
            - content: 文件内容，默认为空字符串（可选）
            - encoding: 文件编码，默认 'utf-8'（可选）
            - overwrite: 是否覆盖已存在的文件，默认 False（可选）

    Returns:
        dict:
            - success: bool，是否成功
            - file_path: str，创建的文件绝对路径
            - action: str，操作类型（'created'/'overwritten'/'skipped'）
            - size: int，文件大小（字节）
            - error: str，错误信息（如有）
    """
    # 提取参数
    file_path = params.get("file_path")
    if not file_path:
        return {
            "success": False,
            "error": "缺少必需参数: file_path",
            "file_path": "",
            "action": "error",
            "size": 0
        }

    content = params.get("content", "")
    encoding = params.get("encoding", "utf-8")
    overwrite = params.get("overwrite", False)

    # 路径解析
    path = Path(file_path).expanduser().resolve()

    # 安全校验：禁止创建/修改系统关键文件
    forbidden_prefixes = ['/etc', '/usr', '/bin', '/sbin', '/boot', '/dev', '/proc', '/sys', '/System', '/Library']
    forbidden_files = ['/etc/passwd', '/etc/shadow', '/etc/sudoers', '/etc/hosts']
    
    try:
        path_str = str(path)
        for forbidden in forbidden_prefixes:
            if path_str.startswith(forbidden):
                return {
                    "success": False,
                    "error": f"安全限制：禁止在系统目录 {forbidden} 下操作文件",
                    "file_path": path_str,
                    "action": "error",
                    "size": 0
                }
        if path_str in forbidden_files:
            return {
                "success": False,
                "error": f"安全限制：禁止操作系统关键文件 {path.name}",
                "file_path": path_str,
                "action": "error",
                "size": 0
            }
    except Exception:
        pass

    # 检查文件是否已存在
    if path.exists() and not overwrite:
        return {
            "success": False,
            "error": f"文件已存在且 overwrite=false: {file_path}",
            "file_path": str(path),
            "action": "skipped",
            "size": path.stat().st_size
        }

    # 确保父目录存在
    try:
        path.parent.mkdir(parents=True, exist_ok=True)
    except PermissionError:
        return {
            "success": False,
            "error": f"权限不足，无法创建父目录: {path.parent}",
            "file_path": str(path),
            "action": "error",
            "size": 0
        }

    # 写入文件
    try:
        action = "overwritten" if path.exists() else "created"
        
        # 写入内容（如果 content 为空，创建空文件）
        with open(path, 'w', encoding=encoding) as f:
            if content:
                f.write(content)
        
        # 获取文件大小
        size = path.stat().st_size
        
        return {
            "success": True,
            "file_path": str(path),
            "action": action,
            "size": size,
            "error": None
        }
    except PermissionError:
        return {
            "success": False,
            "error": f"权限不足，无法写入文件: {file_path}",
            "file_path": str(path),
            "action": "error",
            "size": 0
        }
    except UnicodeEncodeError as e:
        return {
            "success": False,
            "error": f"编码错误: 内容无法使用 {encoding} 编码。{str(e)}",
            "file_path": str(path),
            "action": "error",
            "size": 0
        }
    except Exception as e:
        return {
            "success": False,
            "error": f"创建文件失败: {str(e)}",
            "file_path": str(path),
            "action": "error",
            "size": 0
        }