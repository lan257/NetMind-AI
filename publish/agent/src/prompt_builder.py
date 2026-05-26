"""Build prompts from Chinese template configuration."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from .json_utils import make_json_safe
from .schemas import AgentRequest, PromptSkillDefinition, ToolDefinition
from .skill_registry import summarize_prompt_skills
from .tool_registry import summarize_tools


PROMPT_DIR = Path(__file__).resolve().parent / "prompts"
PROMPT_TEMPLATE = PROMPT_DIR / "agent_kernel_prompt_zh.md"
RETRY_PROMPT_TEMPLATE = PROMPT_DIR / "agent_kernel_retry_prompt_zh.md"


def build_prompt(
    request: AgentRequest,
    context: dict[str, Any],
    tool_definitions: list[ToolDefinition] | None = None,
    tool_results: list[dict[str, Any]] | None = None,
    prompt_skill_definitions: list[PromptSkillDefinition] | None = None,
) -> str:
    """Build the model prompt without leaking model_config."""
    selected_tools = tool_definitions or []
    previous_tool_results = tool_results or []
    template = _read_template(PROMPT_TEMPLATE)
    replacements = {
        "identity": request.identity,
        "cues": request.cues,
        "user_text": request.user_text,
        "task_state": _to_pretty_json(context.get("task_state") or {}),
        "context": _to_pretty_json(context),
        "available_tools": _to_pretty_json(summarize_tools(selected_tools)),
        "active_skills": _to_pretty_json(summarize_prompt_skills(prompt_skill_definitions or [])),
        "tool_results": _to_pretty_json(previous_tool_results),
        "tool_failure_feedback": _to_pretty_json(_summarize_tool_failures(previous_tool_results)),
        "prompt_skill_summaries": _to_pretty_json(summarize_prompt_skills(prompt_skill_definitions or [])),
    }
    return _replace_template_vars(template, replacements)


def build_retry_prompt(original_prompt: str, invalid_content: str, validation_error: str) -> str:
    """Build a Chinese repair prompt after model output validation failure."""
    template = _read_template(RETRY_PROMPT_TEMPLATE)
    replacements = {
        "original_prompt": original_prompt,
        "invalid_content": invalid_content,
        "validation_error": validation_error,
    }
    return _replace_template_vars(template, replacements)


def _replace_template_vars(template: str, replacements: dict[str, str]) -> str:
    """Replace `{{key}}` placeholders in a prompt template."""
    prompt = template
    for key, value in replacements.items():
        prompt = prompt.replace("{{" + key + "}}", value)
    return prompt


def _read_template(primary_path: Path) -> str:
    """Read a prompt template from src."""
    if primary_path.exists():
        return primary_path.read_text(encoding="utf-8")
    raise FileNotFoundError(f"Prompt 模板不存在: {primary_path}")


def _to_pretty_json(data: Any) -> str:
    """Serialize prompt data as deterministic pretty JSON."""
    return json.dumps(make_json_safe(data), ensure_ascii=False, indent=2, sort_keys=True)


def _summarize_tool_failures(tool_results: list[dict[str, Any]]) -> list[dict[str, Any]]:
    """Extract retry-relevant Tool failure details for the model."""
    failures: list[dict[str, Any]] = []
    for item in tool_results:
        execution = item.get("execution") or {}
        if execution.get("status") not in {"failed", "permission_denied"}:
            continue
        failures.append(
            {
                "call_id": item.get("call_id", ""),
                "tool_id": item.get("tool_id", ""),
                "params": item.get("params") or {},
                "status": execution.get("status", ""),
                "error": execution.get("error") or "",
                "logs": execution.get("logs") or [],
                "diagnostics": execution.get("diagnostics") or {},
            }
        )
    return failures
