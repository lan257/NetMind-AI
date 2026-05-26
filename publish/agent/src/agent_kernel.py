"""Agent Kernel orchestration entry point."""

from __future__ import annotations

import json
import sys
from typing import Any

if __package__ in {None, ""}:
    from pathlib import Path

    sys.path.append(str(Path(__file__).resolve().parents[1]))

    from src.api_adapter import build_api_response
    from src.context_manager import ensure_task_state, load_context, save_context
    from src.llm_client import call_llm, parse_tool_call_drafts
    from src.tool_call_builder import (
        merge_permission_result,
        rebuild_tool_call_records,
        split_executable_tool_calls,
    )
    from src.prompt_builder import build_prompt
    from src.schemas import AgentRequest
    from src.skill_registry import load_prompt_skill_definitions
    from src.skill_selector import select_prompt_skills
    from src.state_machine import calculate_turn_status, generate_turn_id
    from src.tool_registry import load_tool_definitions
    from src.tool_runner import execute_ready_tool_calls
else:
    from .api_adapter import build_api_response
    from .context_manager import ensure_task_state, load_context, save_context
    from .llm_client import call_llm, parse_tool_call_drafts
    from .tool_call_builder import (
        merge_permission_result,
        rebuild_tool_call_records,
        split_executable_tool_calls,
    )
    from .prompt_builder import build_prompt
    from .schemas import AgentRequest
    from .skill_registry import load_prompt_skill_definitions
    from .skill_selector import select_prompt_skills
    from .state_machine import calculate_turn_status, generate_turn_id
    from .tool_registry import load_tool_definitions
    from .tool_runner import execute_ready_tool_calls


def run_agent_kernel(raw_request: dict[str, Any]) -> dict[str, Any]:
    """Run one complete Agent Kernel cycle and return frontend JSON."""
    turn_id = generate_turn_id()
    request = AgentRequest.from_dict(raw_request)
    context = load_context(request.conversation_id, request.context)
    task_state = ensure_task_state(context, request.user_text)
    tool_definitions = load_tool_definitions(request.domain)
    prompt_skill_definitions = select_prompt_skills(
        load_prompt_skill_definitions(request.domain),
        request_text=request.user_text,
        context=context,
        tool_definitions=tool_definitions,
    )
    confirmed_calls = merge_permission_result(
        request.history_tool_calls,
        request.confirmed_tool_calls,
    )
    tool_results = execute_ready_tool_calls(confirmed_calls, request.tool_runtime)
    prompt = build_prompt(
        request=request,
        context=context,
        tool_definitions=tool_definitions,
        tool_results=tool_results,
        prompt_skill_definitions=prompt_skill_definitions,
    )
    ai_response = call_llm(
        prompt=prompt,
        request=request,
        tool_definitions=tool_definitions,
        tool_results=tool_results,
    )
    apply_stable_task_state(ai_response, task_state)
    if ai_response.get("error"):
        response = build_api_response(
            build_error_response(ai_response, tool_results, turn_id=turn_id),
        )
        save_context(request.conversation_id, response)
        return response
    drafts = parse_tool_call_drafts(ai_response)
    records = rebuild_tool_call_records(drafts, tool_definitions, turn_id=turn_id)
    ready_records, pending_records = split_executable_tool_calls(records)
    current_tool_results = execute_ready_tool_calls(ready_records, request.tool_runtime)
    response = build_api_response(
        build_frontend_response(
            ai_response,
            pending_records,
            tool_results,
            current_tool_results,
            turn_id=turn_id,
            had_tool_requests=bool(drafts),
            model_needs_continuation=bool(ai_response.get("needs_continuation")),
        )
    )
    save_context(request.conversation_id, response)
    return response


def build_frontend_response(
    ai_response: dict[str, Any],
    tool_calls: list[dict[str, Any]],
    tool_results: list[dict[str, Any]],
    current_tool_results: list[dict[str, Any]] | None = None,
    turn_id: str = "",
    had_tool_requests: bool = False,
    model_needs_continuation: bool = False,
) -> dict[str, Any]:
    """Build the unified JSON output expected by frontend/backend."""
    status_tool_calls = tool_results + (current_tool_results or []) + tool_calls
    response_tool_calls = _build_response_tool_calls(
        previous_tool_results=tool_results,
        current_tool_results=current_tool_results or [],
        pending_tool_calls=tool_calls,
    )
    status = calculate_turn_status(
        status_tool_calls,
        current_tool_results=current_tool_results or [],
        had_tool_requests=had_tool_requests,
        model_needs_continuation=model_needs_continuation,
    )
    return {
        "turn_id": turn_id,
        "status": status,
        "agent_target": ai_response.get("agent_target", ""),
        "main_text": ai_response.get("main_text", ""),
        "tool_calls": response_tool_calls,
        "context_update": ai_response.get("context_update", {"working_memory": {}, "summary": ""}),
    }


def apply_stable_task_state(ai_response: dict[str, Any], task_state: dict[str, Any]) -> None:
    """Preserve the root goal across model-produced context updates."""
    if not task_state:
        return
    context_update = ai_response.setdefault("context_update", {})
    if not isinstance(context_update, dict):
        ai_response["context_update"] = context_update = {}
    updated_task_state = dict(context_update.get("task_state") or {})
    updated_task_state["root_goal"] = task_state.get("root_goal", updated_task_state.get("root_goal", ""))
    updated_task_state.setdefault("status", task_state.get("status", "active"))
    updated_task_state.setdefault("todo_items", task_state.get("todo_items", []))
    updated_task_state.setdefault("completed_items", task_state.get("completed_items", []))
    updated_task_state.setdefault("remaining_items", task_state.get("remaining_items", []))
    context_update["task_state"] = updated_task_state
    working_memory = context_update.setdefault("working_memory", {})
    if isinstance(working_memory, dict):
        working_memory["task_state"] = updated_task_state


def build_error_response(
    ai_response: dict[str, Any],
    tool_results: list[dict[str, Any]],
    turn_id: str = "",
) -> dict[str, Any]:
    """Build the unified error JSON output expected by frontend/backend."""
    return {
        "turn_id": turn_id,
        "status": "error",
        "agent_target": ai_response.get("agent_target", "大模型调用失败"),
        "main_text": ai_response.get("main_text", ai_response.get("error", "")),
        "tool_calls": _active_history_tool_calls(tool_results),
        "context_update": ai_response.get("context_update", {"working_memory": {}, "summary": ""}),
        "error": ai_response.get("error", ""),
    }


def _build_response_tool_calls(
    previous_tool_results: list[dict[str, Any]],
    current_tool_results: list[dict[str, Any]],
    pending_tool_calls: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    """Return only Tool records the caller must carry into the next kernel turn."""
    return _dedupe_tool_calls(
        _active_history_tool_calls(previous_tool_results)
        + current_tool_results
        + pending_tool_calls
    )


def _active_history_tool_calls(tool_calls: list[dict[str, Any]]) -> list[dict[str, Any]]:
    """Keep unresolved historical Tool calls, but drop results already shown to the model."""
    return [
        item
        for item in tool_calls
        if _tool_execution_status(item) in {"waiting_permission", "ready", "running"}
    ]


def _tool_execution_status(tool_call: dict[str, Any]) -> str:
    """Read a ToolCallRecord execution status defensively."""
    execution = tool_call.get("execution")
    if not isinstance(execution, dict):
        return ""
    return str(execution.get("status") or "")


def _dedupe_tool_calls(tool_calls: list[dict[str, Any]]) -> list[dict[str, Any]]:
    """Preserve order while avoiding duplicate ToolCallRecords in continuation state."""
    seen: set[str] = set()
    deduped: list[dict[str, Any]] = []
    for item in tool_calls:
        call_id = str(item.get("call_id") or "")
        if call_id and call_id in seen:
            continue
        if call_id:
            seen.add(call_id)
        deduped.append(item)
    return deduped


def main() -> None:
    """Read JSON from stdin and print Agent Kernel response JSON."""
    raw_input = sys.stdin.read()
    payload = json.loads(raw_input or "{}")
    response = run_agent_kernel(payload)
    print(json.dumps(response, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
