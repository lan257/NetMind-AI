"""
目录树读取 Skill

生成指定路径的树形目录结构
"""

import os
from pathlib import Path
from typing import Any


def run(params: dict[str, Any]) -> dict[str, Any]:
    """
    执行目录树读取

    Args:
        params: 参数字典
            - target_path: 目标路径（必需）
            - max_depth: 最大深度，默认 3（可选）
            - ignore_hidden: 是否忽略隐藏文件/文件夹，默认 True（可选）
            - ignore_patterns: 忽略的模式列表，默认 ['.git', '__pycache__', '.pyc', '.DS_Store']（可选）

    Returns:
        dict:
            - success: bool，是否成功
            - tree: str，树形文本
            - target_path: str，读取的路径
            - total_count: int，总条目数
            - error: str，错误信息（如有）
    """
    # 提取参数
    target_path = params.get("target_path")
    if not target_path:
        return {
            "success": False,
            "error": "缺少必需参数: target_path",
            "tree": "",
            "target_path": "",
            "total_count": 0
        }

    max_depth = params.get("max_depth", 3)
    ignore_hidden = params.get("ignore_hidden", True)
    ignore_patterns = params.get("ignore_patterns", ['.git', '__pycache__', '.pyc', '.DS_Store'])

    # 路径解析
    path = Path(target_path).expanduser().resolve()

    # 安全校验
    if not path.exists():
        return {
            "success": False,
            "error": f"路径不存在: {target_path}",
            "tree": "",
            "target_path": str(path),
            "total_count": 0
        }

    if not path.is_dir():
        return {
            "success": False,
            "error": f"路径不是目录: {target_path}",
            "tree": "",
            "target_path": str(path),
            "total_count": 0
        }

    # 生成目录树
    try:
        tree_lines = []
        total_count = 0

        def should_ignore(entry_name: str) -> bool:
            """检查是否应该忽略该条目"""
            if ignore_hidden and entry_name.startswith('.'):
                return True
            for pattern in ignore_patterns:
                if pattern in entry_name:
                    return True
            return False

        def generate_tree(current_path: Path, prefix: str = "", depth: int = 0):
            nonlocal total_count
            if depth >= max_depth:
                return

            try:
                entries = sorted(current_path.iterdir(), key=lambda x: (not x.is_dir(), x.name.lower()))
            except PermissionError:
                tree_lines.append(f"{prefix}[权限不足]")
                return

            for idx, entry in enumerate(entries):
                entry_name = entry.name
                if should_ignore(entry_name):
                    continue

                total_count += 1
                is_last = idx == len(entries) - 1
                connector = "└── " if is_last else "├── "
                tree_lines.append(f"{prefix}{connector}{entry_name}")

                if entry.is_dir():
                    extension = "    " if is_last else "│   "
                    generate_tree(entry, prefix + extension, depth + 1)

        # 添加根目录
        tree_lines.append(str(path))
        generate_tree(path, "", 0)

        tree_text = "\n".join(tree_lines)

        return {
            "success": True,
            "tree": tree_text,
            "target_path": str(path),
            "total_count": total_count,
            "error": None
        }

    except Exception as e:
        return {
            "success": False,
            "error": f"生成目录树失败: {str(e)}",
            "tree": "",
            "target_path": str(path),
            "total_count": 0
        }