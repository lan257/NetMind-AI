"""
创建文件夹 Skill

在指定路径创建新文件夹
"""

import os
from pathlib import Path
from typing import Any


def run(params: dict[str, Any]) -> dict[str, Any]:
    """
    执行创建文件夹操作

    Args:
        params: 参数字典
            - folder_path: 文件夹路径（必需）
            - exist_ok: 如果文件夹已存在是否视为成功，默认 False（可选）

    Returns:
        dict:
            - success: bool，是否成功
            - folder_path: str，创建的文件夹绝对路径
            - created: bool，是否为新创建
            - error: str，错误信息（如有）
    """
    # 提取参数
    folder_path = params.get("folder_path")
    if not folder_path:
        return {
            "success": False,
            "error": "缺少必需参数: folder_path",
            "folder_path": "",
            "created": False
        }

    exist_ok = params.get("exist_ok", False)

    # 路径解析
    path = Path(folder_path).expanduser().resolve()

    # 安全校验：禁止创建到系统关键目录（可根据需要扩展）
    forbidden_prefixes = ['/etc', '/usr', '/bin', '/sbin', '/boot', '/dev', '/proc', '/sys']
    try:
        for forbidden in forbidden_prefixes:
            if str(path).startswith(forbidden):
                return {
                    "success": False,
                    "error": f"安全限制：禁止在系统目录 {forbidden} 下创建文件夹",
                    "folder_path": str(path),
                    "created": False
                }
    except Exception:
        pass

    # 检查是否已存在
    if path.exists():
        if path.is_dir():
            if exist_ok:
                return {
                    "success": True,
                    "folder_path": str(path),
                    "created": False,
                    "error": None
                }
            else:
                return {
                    "success": False,
                    "error": f"文件夹已存在: {folder_path}",
                    "folder_path": str(path),
                    "created": False
                }
        else:
            return {
                "success": False,
                "error": f"路径已存在但不是文件夹: {folder_path}",
                "folder_path": str(path),
                "created": False
            }

    # 创建文件夹
    try:
        # exist_ok=True 在 parents=True 时如果目录存在也不会报错
        path.mkdir(parents=True, exist_ok=exist_ok)
        
        # 验证是否真的创建了（如果 exist_ok=True，可能目录已存在）
        created = not path.exists() or path.stat().st_ctime > 0  # 简化判断
        
        return {
            "success": True,
            "folder_path": str(path),
            "created": True,
            "error": None
        }
    except PermissionError:
        return {
            "success": False,
            "error": f"权限不足，无法创建文件夹: {folder_path}",
            "folder_path": str(path),
            "created": False
        }
    except OSError as e:
        return {
            "success": False,
            "error": f"创建文件夹失败: {str(e)}",
            "folder_path": str(path),
            "created": False
        }