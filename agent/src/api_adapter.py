"""API boundary adapters for the public Agent Kernel protocol."""

from __future__ import annotations

from typing import Any

API_VERSION = "v2"
SUPPORTED_REQUEST_FIELDS = {
    "api_version",
    "conversation_id",
    "user_text",
    "domain",
    "identity",
    "cues",
    "model_config",
    "context",
    "tool_runtime",
    "history_tool_calls",
    "confirmed_tool_calls",
}


def normalize_request_payload(raw_request: dict[str, Any] | None) -> dict[str, Any]:
    """Validate and normalize an inbound public API payload."""
    if raw_request is None:
        raise ValueError("request payload is required")
    if not isinstance(raw_request, dict):
        raise ValueError("request payload must be a JSON object")
    raw = dict(raw_request or {})
    unsupported_fields = sorted(set(raw) - SUPPORTED_REQUEST_FIELDS)
    if unsupported_fields:
        raise ValueError(f"unsupported request fields: {', '.join(unsupported_fields)}")

    return {
        **raw,
        "api_version": _read_api_version(raw.get("api_version")),
        "domain": _read_string(raw, "domain", default="default"),
        "tool_runtime": _read_dict(raw, "tool_runtime", default={}),
        "history_tool_calls": _read_tool_call_list(raw, "history_tool_calls"),
        "confirmed_tool_calls": _read_tool_call_list(raw, "confirmed_tool_calls"),
    }


def build_api_response(core_response: dict[str, Any]) -> dict[str, Any]:
    """Adapt the internal Tool response to the public API shape."""
    response = dict(core_response)
    response["api_version"] = API_VERSION
    response["tool_calls"] = _read_response_tool_calls(response)
    return response


def _read_api_version(value: Any) -> str:
    """Validate an optional protocol marker."""
    if value is None or value == "":
        return API_VERSION
    version = str(value or "").strip().lower()
    if version != API_VERSION:
        raise ValueError(f"unsupported api_version: {value}; expected {API_VERSION}")
    return version


def _read_string(raw: dict[str, Any], key: str, default: str) -> str:
    value = raw.get(key, default)
    if not isinstance(value, str):
        raise ValueError(f"{key} must be a string")
    return value.strip() or default


def _read_dict(raw: dict[str, Any], key: str, default: dict[str, Any]) -> dict[str, Any]:
    value = raw.get(key, default)
    if not isinstance(value, dict):
        raise ValueError(f"{key} must be an object")
    return dict(value)


def _read_tool_call_list(raw: dict[str, Any], key: str) -> list[dict[str, Any]]:
    value = raw.get(key, [])
    if not isinstance(value, list):
        raise ValueError(f"{key} must be an array")
    return [_normalize_tool_call_record(item, key) for item in value]


def _read_response_tool_calls(response: dict[str, Any]) -> list[dict[str, Any]]:
    value = response.get("tool_calls", [])
    if not isinstance(value, list):
        raise ValueError("tool_calls must be an array")
    return [_normalize_tool_call_record(item, "tool_calls") for item in value]


def _normalize_tool_call_record(record: Any, field_name: str) -> dict[str, Any]:
    if not isinstance(record, dict):
        raise ValueError(f"{field_name} entries must be objects")
    forbidden = {"skill_id", "skill_name"} & set(record)
    if forbidden:
        raise ValueError(f"{field_name} entries contain unsupported fields: {', '.join(sorted(forbidden))}")
    normalized = dict(record)
    normalized["params"] = dict(normalized.get("params") or {})
    normalized["permission"] = dict(normalized.get("permission") or {})
    normalized["execution"] = dict(normalized.get("execution") or {})
    normalized["definition"] = dict(normalized.get("definition") or {})
    return normalized
