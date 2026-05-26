"""Helpers for converting runtime values into JSON-safe data."""

from __future__ import annotations

from pathlib import Path
from typing import Any


def make_json_safe(data: Any) -> Any:
    """Return a recursively JSON-serializable representation of data."""
    if data is None or isinstance(data, (str, int, float, bool)):
        return data
    if isinstance(data, Path):
        return str(data)
    if isinstance(data, dict):
        return {str(key): make_json_safe(value) for key, value in data.items()}
    if isinstance(data, (list, tuple)):
        return [make_json_safe(item) for item in data]
    if isinstance(data, set):
        return [make_json_safe(item) for item in sorted(data, key=repr)]
    return str(data)
