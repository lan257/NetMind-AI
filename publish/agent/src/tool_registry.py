"""Load trusted executable Tool definitions from `.tool`."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from .registry_utils import is_relative_to, load_yaml_like
from .schemas import ToolDefinition


PROJECT_ROOT = Path(__file__).resolve().parents[1]
TOOL_ROOT = PROJECT_ROOT / ".tool"


def load_domain_bindings(tool_root: Path | None = None) -> dict[str, str]:
    """Read domain-to-tool-list bindings from `.tool` only."""
    root = tool_root or TOOL_ROOT
    binding_path = root / "domain_tool_bindings.json"
    with binding_path.open("r", encoding="utf-8-sig") as file:
        payload = json.load(file)
    bindings = payload.get("domain_tool_bindings")
    if not isinstance(bindings, dict):
        raise ValueError("domain_tool_bindings.json 格式错误")
    return {str(key): str(value) for key, value in bindings.items()}


def load_tool_definitions(domain: str, tool_root: Path | None = None) -> list[ToolDefinition]:
    """Load executable Tool definitions for the selected domain."""
    root = tool_root or TOOL_ROOT
    bindings = load_domain_bindings(root)
    list_path = root / bindings.get(domain, bindings["default"])
    payload = load_yaml_like(list_path)
    if "tools" not in payload:
        raise ValueError(f"Tool 列表必须使用 tools 顶层字段: {list_path}")
    definitions = [ToolDefinition.from_dict(item) for item in payload.get("tools", [])]
    for definition in definitions:
        _validate_tool_definition_path(root, definition)
    return definitions


def summarize_tools(tool_definitions: list[ToolDefinition]) -> list[dict[str, Any]]:
    """Return executable Tool summaries that are safe to include in prompts."""
    return [
        {
            "tool_id": item.tool_id,
            "tool_name": item.tool_name,
            "description": item.description,
            "trigger": item.trigger,
            "params": item.params,
            "param_descriptions": item.param_descriptions,
            "permission_level": item.permission_level,
        }
        for item in tool_definitions
    ]


def _validate_tool_definition_path(root: Path, definition: ToolDefinition) -> None:
    """Ensure Tool scripts are declared below `.tool/tools`."""
    if not definition.script_path:
        raise ValueError(f"Tool {definition.tool_id} 缺少 script_path")
    resolved_script = _resolve_script_path(root, definition.script_path)
    tools_root = (root / "tools").resolve()
    if not is_relative_to(resolved_script, tools_root):
        raise ValueError(
            f"Tool {definition.tool_id} 的 script_path 必须位于 .tool/tools 下: {definition.script_path}"
        )


def _resolve_script_path(root: Path, script_path: str) -> Path:
    raw_path = Path(script_path)
    if raw_path.is_absolute():
        return raw_path.resolve()
    candidates = [
        (PROJECT_ROOT / raw_path).resolve(),
        (root.parent / raw_path).resolve(),
        (root / raw_path).resolve(),
    ]
    for candidate in candidates:
        if candidate.exists():
            return candidate
    return candidates[1]
