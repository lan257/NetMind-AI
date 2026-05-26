"""Shared helpers for local Tool and Skill registry files."""

from __future__ import annotations

from pathlib import Path
from typing import Any


def load_yaml_like(path: Path) -> dict[str, Any]:
    """Load the tiny YAML subset used by local list files."""
    list_key = "skills"
    items: list[dict[str, Any]] = []
    current: dict[str, Any] | None = None
    current_map_key: str | None = None

    for raw_line in path.read_text(encoding="utf-8").splitlines():
        if not raw_line.strip() or raw_line.lstrip().startswith("#"):
            continue
        stripped = raw_line.strip().lstrip("\ufeff")
        if stripped in {"skills:", "tools:"}:
            list_key = stripped[:-1]
            continue
        if stripped.startswith("- "):
            if current:
                items.append(current)
            current = {}
            current_map_key = None
            key, value = split_key_value(stripped[2:])
            current[key] = value
            continue
        if current is None:
            continue
        key, value = split_key_value(stripped)
        if value == "" and key in {"params", "param_descriptions", "policy"}:
            current[key] = {}
            current_map_key = key
            continue
        if current_map_key and raw_line.startswith("      "):
            current[current_map_key][key] = value
            continue
        current[key] = value
        current_map_key = None

    if current:
        items.append(current)
    return {list_key: items}


def split_key_value(text: str) -> tuple[str, str]:
    """Split a YAML-like `key: value` line."""
    key, _, value = text.partition(":")
    return key.strip(), value.strip().strip('"').strip("'")


def is_relative_to(path: Path, parent: Path) -> bool:
    """Return whether `path` is inside `parent`."""
    try:
        path.relative_to(parent)
    except ValueError:
        return False
    return True
