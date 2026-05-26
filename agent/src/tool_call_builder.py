"""Permission validation and ToolCallRecord rebuilding."""

from __future__ import annotations

from typing import Any

from .schemas import ToolDefinition
from .state_machine import build_tool_call_id, generate_turn_id
from .tool_param_validator import ToolParamValidationResult, validate_tool_params


def rebuild_tool_call_records(
    drafts: list[dict[str, Any]],
    tool_definitions: list[ToolDefinition],
    turn_id: str | None = None,
) -> list[dict[str, Any]]:
    """Convert untrusted model drafts into trusted ToolCallRecord objects."""
    current_turn_id = turn_id or generate_turn_id()
    definition_map = {definition.tool_id: definition for definition in tool_definitions}
    records: list[dict[str, Any]] = []
    for index, raw_draft in enumerate(drafts, start=1):
        draft = _normalize_tool_call_draft(raw_draft)
        tool_id = str(draft.get("tool_id") or "")
        call_id = build_tool_call_id(current_turn_id, index)
        definition = definition_map.get(tool_id)
        if not definition:
            records.append(_build_unknown_tool_record(call_id, current_turn_id, draft, tool_id))
            continue

        params = dict(draft.get("params") or {})
        validation_result = validate_tool_params(definition, params)
        if not validation_result.ok:
            records.append(
                _build_param_validation_failed_record(
                    call_id,
                    current_turn_id,
                    draft,
                    definition,
                    params,
                    validation_result,
                )
            )
            continue

        permission = _build_permission(definition, params)
        execution_status = _initial_execution_status(definition, permission)
        records.append(
            {
                "turn_id": current_turn_id,
                "call_id": call_id,
                "tool_id": definition.tool_id,
                "tool_name": definition.tool_name,
                "params": params,
                "reason": str(draft.get("reason") or ""),
                "permission": permission,
                "execution": {
                    "status": execution_status,
                    "success": False if execution_status in {"permission_denied", "failed"} else None,
                    "result": None,
                    "error": "Tool 已被禁用" if execution_status == "permission_denied" else None,
                },
                "definition": _record_definition(definition),
            }
        )
    return records


def merge_permission_result(
    history_tool_calls: list[dict[str, Any]],
    confirmed_tool_calls: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    """Merge frontend permission decisions into previous ToolCallRecords."""
    decisions = [_normalize_tool_call_record(item) for item in confirmed_tool_calls]
    decision_map = {item.get("call_id"): item for item in decisions}
    merged_calls: list[dict[str, Any]] = []
    for raw_record in history_tool_calls:
        record = _normalize_tool_call_record(raw_record)
        call_id = record.get("call_id")
        cloned = dict(record)
        cloned["permission"] = dict(record.get("permission") or {})
        cloned["execution"] = dict(record.get("execution") or {})
        if call_id in decision_map and _can_apply_permission_decision(cloned, decision_map[call_id]):
            decision = decision_map[call_id]
            approved = _is_approved(decision.get("approved"))
            cloned["permission"]["approved"] = approved
            cloned["execution"]["status"] = "ready" if approved else "permission_denied"
            if approved:
                cloned["permission"].pop("reject_reason", None)
                cloned["execution"].pop("denied_reason", None)
            else:
                reject_reason = _extract_reject_reason(decision)
                if reject_reason:
                    cloned["permission"]["reject_reason"] = reject_reason
                    cloned["execution"]["denied_reason"] = reject_reason
                else:
                    cloned["permission"].pop("reject_reason", None)
                    cloned["execution"].pop("denied_reason", None)
                cloned["execution"]["success"] = False
                cloned["execution"]["error"] = _build_permission_denied_error(reject_reason)
        merged_calls.append(cloned)
    return merged_calls


def _can_apply_permission_decision(record: dict[str, Any], decision: dict[str, Any]) -> bool:
    """Permission decisions may only update matching waiting_permission records."""
    if record.get("execution", {}).get("status") != "waiting_permission":
        return False
    decision_tool_id = decision.get("tool_id")
    if decision_tool_id is not None and str(decision_tool_id) != str(record.get("tool_id") or ""):
        return False
    return True


def _extract_reject_reason(decision: dict[str, Any]) -> str:
    """Read the caller-provided reason for rejecting a Tool call."""
    value = decision.get("reject_reason")
    return str(value).strip() if value is not None and str(value).strip() else ""


def _is_approved(value: Any) -> bool:
    """Normalize common JSON-ish approval values from callers."""
    if isinstance(value, bool):
        return value
    if isinstance(value, str):
        return value.strip().lower() in {"true", "1", "yes", "y"}
    return bool(value)


def _build_permission_denied_error(reject_reason: str) -> str:
    """Build a user-facing denial error that can be passed back to the model."""
    if reject_reason:
        return f"用户拒绝授权：{reject_reason}"
    return "用户拒绝授权"


def split_executable_tool_calls(
    tool_calls: list[dict[str, Any]],
) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    """Split ready ToolCallRecords from records that should be returned as-is."""
    executable_calls: list[dict[str, Any]] = []
    pending_or_final_calls: list[dict[str, Any]] = []
    for raw_record in tool_calls:
        record = _normalize_tool_call_record(raw_record)
        status = record.get("execution", {}).get("status")
        if status == "ready":
            executable_calls.append(record)
        else:
            pending_or_final_calls.append(record)
    return executable_calls, pending_or_final_calls


def _build_permission(definition: ToolDefinition, params: dict[str, Any]) -> dict[str, Any]:
    """Build permission metadata only from ToolDefinition."""
    if definition.permission_level == "forbidden":
        return {
            "required": False,
            "level": definition.permission_level,
            "approved": False,
            "message": "该 Tool 已被禁用",
        }
    required = definition.permission_level in {"confirm", "high"}
    message = definition.permission_message
    for key, value in params.items():
        message = message.replace("{" + key + "}", str(value))
    return {
        "required": required,
        "level": definition.permission_level,
        "approved": None if required else True,
        "message": message,
        **_build_extra_confirmation(definition),
    }


def _initial_execution_status(definition: ToolDefinition, permission: dict[str, Any]) -> str:
    """Return the initial execution state for a trusted ToolDefinition."""
    if definition.permission_level == "forbidden":
        return "permission_denied"
    return "waiting_permission" if permission["required"] else "ready"


def _build_unknown_tool_record(
    call_id: str,
    turn_id: str,
    draft: dict[str, Any],
    tool_id: str,
) -> dict[str, Any]:
    """Build a standard ToolCallRecord for an AI-requested unknown Tool."""
    return {
        "turn_id": turn_id,
        "call_id": call_id,
        "tool_id": tool_id,
        "tool_name": "未知 Tool",
        "params": dict(draft.get("params") or {}),
        "reason": str(draft.get("reason") or ""),
        "permission": {
            "required": False,
            "level": "forbidden",
            "approved": False,
            "message": f"当前角色不允许调用 Tool: {tool_id}",
        },
        "execution": {
            "status": "failed",
            "success": False,
            "result": None,
            "error": f"未知或未授权 Tool: {tool_id}",
        },
        "definition": {"script_path": None},
    }


def _build_param_validation_failed_record(
    call_id: str,
    turn_id: str,
    draft: dict[str, Any],
    definition: ToolDefinition,
    params: dict[str, Any],
    validation_result: ToolParamValidationResult,
) -> dict[str, Any]:
    """Build a failed record that can be fed back to the model for self-repair."""
    diagnostics = {
        "error_type": "ParamValidationError",
        "error": validation_result.message,
        "failed_tool_id": definition.tool_id,
        "failed_params": params,
        "retry_guidance": (
            "下一轮请根据参数校验错误修正 tool_call_drafts[].params；"
            "不要原样重复同一个 tool_id 和 params。"
        ),
    }
    validation_diagnostics = dict(validation_result.diagnostics)
    validation_error_type = validation_diagnostics.pop("error_type", "")
    diagnostics.update(validation_diagnostics)
    if validation_error_type:
        diagnostics["validation_error_type"] = validation_error_type
    return {
        "turn_id": turn_id,
        "call_id": call_id,
        "tool_id": definition.tool_id,
        "tool_name": definition.tool_name,
        "params": params,
        "reason": str(draft.get("reason") or ""),
        "permission": {
            "required": False,
            "level": definition.permission_level,
            "approved": False,
            "message": "参数校验失败，未请求权限",
        },
        "execution": {
            "status": "failed",
            "success": False,
            "result": None,
            "error": validation_result.message,
            "diagnostics": diagnostics,
        },
        "definition": _record_definition(definition),
    }


def _record_definition(definition: ToolDefinition) -> dict[str, Any]:
    """Store only trusted runner metadata needed after the model turn."""
    payload: dict[str, Any] = {
        "script_path": definition.script_path,
        "policy": dict(definition.policy),
    }
    if definition.timeout_seconds is not None:
        payload["timeout_seconds"] = definition.timeout_seconds
    return payload


def _build_extra_confirmation(definition: ToolDefinition) -> dict[str, Any]:
    """Expose an explicit high-risk confirmation hint without trusting the model."""
    if definition.permission_level != "high":
        return {}
    return {
        "extra_confirmation": {
            "required": True,
            "risk": str(definition.policy.get("risk") or "high"),
            "message": str(
                definition.policy.get("high_risk_message")
                or definition.policy.get("extra_confirmation_message")
                or "高风险 Tool 需要额外确认。"
            ),
        }
    }


def _normalize_tool_call_draft(draft: dict[str, Any]) -> dict[str, Any]:
    if "skill_id" in draft:
        raise ValueError("tool_call_drafts 不支持字段 skill_id")
    normalized = dict(draft)
    normalized["params"] = dict(normalized.get("params") or {})
    return normalized


def _normalize_tool_call_record(record: dict[str, Any]) -> dict[str, Any]:
    forbidden = {"skill_id", "skill_name"} & set(record)
    if forbidden:
        raise ValueError(f"ToolCallRecord 不支持字段: {', '.join(sorted(forbidden))}")
    normalized = dict(record)
    normalized["params"] = dict(normalized.get("params") or {})
    normalized["permission"] = dict(normalized.get("permission") or {})
    normalized["execution"] = dict(normalized.get("execution") or {})
    normalized["definition"] = dict(normalized.get("definition") or {})
    return normalized
