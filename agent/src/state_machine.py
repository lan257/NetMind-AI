"""Turn and ToolCall state helpers for Agent Kernel."""

from __future__ import annotations

from datetime import datetime
from typing import Any
from uuid import uuid4


TOP_LEVEL_STATUSES = {"waiting_permission", "running", "final", "error"}


def generate_turn_id(now: datetime | None = None) -> str:
    """Return a stable unique id for one kernel turn."""
    current = now or datetime.now()
    stamp = current.strftime("%Y%m%d_%H%M%S")
    suffix = uuid4().hex[:8]
    return f"turn_{stamp}_{suffix}"


def build_tool_call_id(turn_id: str, index: int) -> str:
    """Return a ToolCall id that is unique across turns."""
    normalized_turn_id = str(turn_id or "").strip() or generate_turn_id()
    return f"tc_{normalized_turn_id.removeprefix('turn_')}_{index:03d}"


def calculate_turn_status(
    tool_calls: list[dict[str, Any]],
    *,
    current_tool_results: list[dict[str, Any]] | None = None,
    had_tool_requests: bool = False,
    model_needs_continuation: bool = False,
    error: str | None = None,
) -> str:
    """Compute the top-level Agent Kernel status from trusted state."""
    if error:
        return "error"

    if any(_execution_status(item) == "waiting_permission" for item in tool_calls):
        return "waiting_permission"

    if any(_execution_status(item) == "running" for item in tool_calls):
        return "running"

    if model_needs_continuation:
        return "running"

    if current_tool_results:
        return "running"

    if had_tool_requests:
        return "running"

    return "final"


def _execution_status(record: dict[str, Any]) -> str:
    execution = record.get("execution")
    if not isinstance(execution, dict):
        return ""
    return str(execution.get("status") or "")
