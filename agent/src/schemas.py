"""Shared schema helpers for the Agent Kernel runtime."""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any


PERMISSION_LEVELS = {"none", "confirm", "high", "forbidden"}


@dataclass(frozen=True, init=False)
class ToolDefinition:
    """Trusted executable Tool definition loaded from `.tool/lists/*.yaml`."""

    tool_id: str
    tool_name: str
    description: str
    trigger: str
    params: dict[str, str]
    parameters: dict[str, Any]
    policy: dict[str, Any]
    permission_level: str
    permission_message: str
    script_path: str
    timeout_seconds: float | None
    param_descriptions: dict[str, str]
    category: str
    tags: list[str]

    def __init__(
        self,
        tool_id: str | None = None,
        tool_name: str | None = None,
        description: str = "",
        trigger: str = "",
        params: dict[str, str] | None = None,
        parameters: dict[str, Any] | None = None,
        policy: dict[str, Any] | None = None,
        permission_level: str = "none",
        permission_message: str = "",
        script_path: str = "",
        timeout_seconds: float | int | None = None,
        param_descriptions: dict[str, str] | None = None,
        category: str = "",
        tags: list[str] | None = None,
    ) -> None:
        if not tool_id:
            raise ValueError("ToolDefinition 缺少字段: tool_id")
        if not tool_name:
            raise ValueError("ToolDefinition 缺少字段: tool_name")
        if permission_level not in PERMISSION_LEVELS:
            raise ValueError(f"未知权限等级: {permission_level}")
        object.__setattr__(self, "tool_id", str(tool_id))
        object.__setattr__(self, "tool_name", str(tool_name))
        object.__setattr__(self, "description", str(description))
        object.__setattr__(self, "trigger", str(trigger))
        object.__setattr__(self, "params", dict(params or {}))
        object.__setattr__(self, "parameters", dict(parameters or {}))
        object.__setattr__(self, "policy", dict(policy or {}))
        object.__setattr__(self, "permission_level", str(permission_level))
        object.__setattr__(self, "permission_message", str(permission_message))
        object.__setattr__(self, "script_path", str(script_path))
        object.__setattr__(self, "timeout_seconds", _normalize_timeout_seconds(timeout_seconds))
        object.__setattr__(self, "param_descriptions", dict(param_descriptions or {}))
        object.__setattr__(self, "category", _normalize_category(category))
        object.__setattr__(self, "tags", _normalize_tags(tags))

    @property
    def skill_id(self) -> str:
        """Management tooling still presents tool identifiers as skill IDs."""
        return self.tool_id

    @property
    def skill_name(self) -> str:
        """Management tooling still presents tool names as skill names."""
        return self.tool_name

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> "ToolDefinition":
        """Build a Tool definition from the public tool_* fields."""
        unsupported = sorted({"skill_id", "skill_name"} & set(data))
        if unsupported:
            raise ValueError(f"ToolDefinition 不支持字段: {', '.join(unsupported)}")
        tool_id = data.get("tool_id")
        tool_name = data.get("tool_name")
        permission = data.get("permission") if isinstance(data.get("permission"), dict) else {}
        runner = data.get("runner") if isinstance(data.get("runner"), dict) else {}
        params = data.get("params")
        if params is None and isinstance(data.get("parameters"), dict):
            params = _params_from_json_schema(data["parameters"])
        parameters = data.get("parameters") if isinstance(data.get("parameters"), dict) else {}
        values = {
            "tool_id": tool_id,
            "tool_name": tool_name,
            "description": data.get("description"),
            "trigger": data.get("trigger"),
            "params": params,
            "permission_level": data.get("permission_level", permission.get("level")),
            "permission_message": data.get("permission_message", permission.get("message")),
            "script_path": data.get("script_path", runner.get("script_path")),
        }
        missing_fields = sorted(key for key, value in values.items() if value is None)
        if missing_fields:
            raise ValueError(f"ToolDefinition 缺少字段: {', '.join(missing_fields)}")
        return cls(
            tool_id=str(values["tool_id"]),
            tool_name=str(values["tool_name"]),
            description=str(values["description"]),
            trigger=str(values["trigger"]),
            params=dict(values["params"] or {}),
            parameters=dict(parameters),
            policy=dict(data.get("policy") or {}),
            param_descriptions=dict(data.get("param_descriptions") or {}),
            permission_level=str(values["permission_level"]),
            permission_message=str(values["permission_message"]),
            script_path=str(values["script_path"]),
            timeout_seconds=runner.get("timeout_seconds", data.get("timeout_seconds")),
            category=_normalize_category(data.get("category")),
            tags=_normalize_tags(data.get("tags")),
        )


SkillDefinition = ToolDefinition


@dataclass(frozen=True)
class PromptSkillDefinition:
    """Prompt-only skill definition loaded from `.skill/lists/*.yaml`."""

    skill_id: str
    skill_name: str
    description: str
    trigger: str
    prompt: str
    available_tools: list[str] = field(default_factory=list)
    priority: int = 0
    scope: str = ""
    workflow_path: str = "workflow.md"
    maintenance_path: str = "maintenance.md"
    category: str = ""
    tags: list[str] = field(default_factory=list)
    workflow_text: str = ""

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> "PromptSkillDefinition":
        """Build a prompt-only skill definition from a mapping."""
        required_fields = {
            "skill_id",
            "skill_name",
            "description",
            "trigger",
            "prompt",
        }
        missing_fields = sorted(required_fields - set(data))
        if missing_fields:
            raise ValueError(f"PromptSkillDefinition 缂哄皯瀛楁: {', '.join(missing_fields)}")
        return cls(
            skill_id=str(data["skill_id"]),
            skill_name=str(data["skill_name"]),
            description=str(data["description"]),
            trigger=str(data["trigger"]),
            prompt=str(data["prompt"]),
            available_tools=_normalize_string_list(data.get("available_tools")),
            priority=_normalize_int(data.get("priority"), default=0),
            scope=str(data.get("scope") or "").strip(),
            workflow_path=str(data.get("workflow_path") or "workflow.md"),
            maintenance_path=str(data.get("maintenance_path") or "maintenance.md"),
            category=_normalize_category(data.get("category")),
            tags=_normalize_tags(data.get("tags")),
            workflow_text=str(data.get("workflow_text") or ""),
        )


@dataclass
class AgentRequest:
    """Normalized input structure accepted by the Agent Kernel."""

    conversation_id: str
    user_text: str
    domain: str
    identity: str
    cues: str
    model_config: dict[str, Any] = field(default_factory=dict)
    context: dict[str, Any] = field(default_factory=dict)
    tool_runtime: dict[str, Any] = field(default_factory=dict)
    confirmed_tool_calls: list[dict[str, Any]] = field(default_factory=list)
    history_tool_calls: list[dict[str, Any]] = field(default_factory=list)

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> "AgentRequest":
        """Normalize backend input into the unified kernel request schema."""
        from .api_adapter import normalize_request_payload

        normalized = normalize_request_payload(data)
        return cls(
            conversation_id=str(normalized.get("conversation_id") or "conv_default"),
            user_text=str(normalized.get("user_text") or ""),
            domain=str(normalized.get("domain") or "default"),
            identity=str(normalized.get("identity") or "你是一个 AI Agent 助手"),
            cues=str(normalized.get("cues") or ""),
            model_config=dict(normalized.get("model_config") or {}),
            context=dict(normalized.get("context") or {}),
            tool_runtime=dict(normalized.get("tool_runtime") or {}),
            confirmed_tool_calls=list(normalized.get("confirmed_tool_calls") or []),
            history_tool_calls=list(normalized.get("history_tool_calls") or []),
        )


def _params_from_json_schema(schema: dict[str, Any]) -> dict[str, str]:
    properties = schema.get("properties")
    if not isinstance(properties, dict):
        return {}
    params: dict[str, str] = {}
    for name, raw_definition in properties.items():
        if not isinstance(raw_definition, dict):
            params[str(name)] = "string"
            continue
        raw_type = raw_definition.get("type") or "string"
        if isinstance(raw_type, list):
            raw_type = "/".join(str(item) for item in raw_type)
        params[str(name)] = str(raw_type)
    return params


def _normalize_tags(value: Any) -> list[str]:
    """Normalize optional Skill management tags from JSON or the local YAML subset."""
    return _normalize_string_list(value, field_name="tags")


def _normalize_string_list(value: Any, field_name: str = "available_tools") -> list[str]:
    """Normalize comma-separated or JSON-style string lists."""
    if value is None or value == {}:
        return []
    if isinstance(value, str):
        raw_items = value.split(",")
    elif isinstance(value, list):
        raw_items = value
    else:
        raise ValueError(f"{field_name} 必须是字符串列表或逗号分隔字符串")
    values: list[str] = []
    seen: set[str] = set()
    for item in raw_items:
        text = str(item).strip()
        if not text or text in seen:
            continue
        values.append(text)
        seen.add(text)
    return values


def _normalize_category(value: Any) -> str:
    if value is None or value == {}:
        return ""
    return str(value).strip()


def _normalize_timeout_seconds(value: Any) -> float | None:
    if value is None or value == {} or value == "":
        return None
    try:
        timeout = float(value)
    except (TypeError, ValueError) as exc:
        raise ValueError("timeout_seconds 蹇呴』鏄鏁板瓧") from exc
    if timeout <= 0:
        raise ValueError("timeout_seconds 蹇呴』澶т簬 0")
    return timeout


def _normalize_int(value: Any, default: int = 0) -> int:
    if value is None or value == {} or value == "":
        return default
    try:
        return int(value)
    except (TypeError, ValueError) as exc:
        raise ValueError("priority 必须是整数") from exc

