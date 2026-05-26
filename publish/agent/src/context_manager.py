"""Context loading and saving placeholders for the Agent Kernel."""

from __future__ import annotations

from typing import Any


CONTINUATION_TEXTS = {"继续上一轮操作。", "继续上一轮操作", "继续", ""}


def load_context(conversation_id: str, request_context: dict[str, Any] | None = None) -> dict[str, Any]:
    """Return request context with the required three context layers."""
    context = dict(request_context or {})
    context.setdefault("conversation_id", conversation_id)
    context.setdefault("long_term_memory", {})
    context.setdefault("working_memory", {})
    context.setdefault("focus_context", {})
    return context


def ensure_task_state(context: dict[str, Any], user_text: str) -> dict[str, Any]:
    """Keep a stable root goal that current-step targets cannot overwrite."""
    task_state = dict(context.get("task_state") or {})
    normalized_text = str(user_text or "").strip()
    if not task_state.get("root_goal") and normalized_text not in CONTINUATION_TEXTS:
        task_state["root_goal"] = normalized_text
        task_state.setdefault("status", "active")
        task_state.setdefault("todo_items", [])
        task_state.setdefault("completed_items", [])
        task_state.setdefault("remaining_items", [])
    if task_state:
        context["task_state"] = task_state
    return task_state


def save_context(conversation_id: str, response: dict[str, Any]) -> dict[str, Any]:
    """Placeholder persistence hook for P1.0."""
    return {
        "conversation_id": conversation_id,
        "saved": True,
        "summary": response.get("context_update", {}).get("summary", ""),
    }

