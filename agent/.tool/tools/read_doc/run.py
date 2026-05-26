"""Read document skill implementation."""

from __future__ import annotations

from pathlib import Path
from typing import Any


def run(params: dict[str, Any]) -> dict[str, Any]:
    """Read a text file and return its content."""
    filepath = str(params.get("filepath") or "")
    if not filepath:
        raise ValueError("filepath 不能为空")
    path = Path(filepath)
    if not path.exists():
        raise FileNotFoundError(f"文件不存在: {filepath}")
    if not path.is_file():
        raise IsADirectoryError(f"路径不是文件: {filepath}")
    return {
        "filepath": str(path),
        "content": path.read_text(encoding="utf-8"),
    }

