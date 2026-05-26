"""Generic Tool parameter validation from JSON Schema and Tool policy."""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any

from .schemas import ToolDefinition


@dataclass(frozen=True)
class ToolParamValidationResult:
    """Validation result for an untrusted Tool parameter payload."""

    ok: bool
    message: str = ""
    failure_kind: str = ""
    diagnostics: dict[str, Any] = field(default_factory=dict)


def validate_tool_params(definition: ToolDefinition, params: dict[str, Any]) -> ToolParamValidationResult:
    """Validate model-proposed Tool params with schema first, then Tool policy."""
    schema_result = _validate_json_schema(definition, params)
    if not schema_result.ok:
        return schema_result
    return _validate_policy(definition, params)


def _validate_json_schema(definition: ToolDefinition, params: dict[str, Any]) -> ToolParamValidationResult:
    schema = definition.parameters if isinstance(definition.parameters, dict) else {}
    declared = definition.params or {}
    properties = schema.get("properties") if isinstance(schema.get("properties"), dict) else {}
    declared_names = set(properties or declared)

    unknown_params = sorted(set(params) - declared_names)
    if unknown_params:
        return _failure(
            definition,
            "schema",
            f"参数校验失败: 未声明参数: {', '.join(unknown_params)}",
            params,
            {"unknown_params": unknown_params},
        )

    required = schema.get("required") if isinstance(schema.get("required"), list) else []
    missing = [str(name) for name in required if str(name) not in params]
    if missing:
        return _failure(
            definition,
            "schema",
            f"参数校验失败: 缺少必需参数: {', '.join(missing)}",
            params,
            {"missing_params": missing},
        )

    errors: list[str] = []
    for name, value in params.items():
        property_schema = properties.get(name) if isinstance(properties.get(name), dict) else {}
        expected_type = property_schema.get("type") or declared.get(name) or ""
        if expected_type and not _value_matches_declared_type(value, expected_type):
            errors.append(f"{name} 应为 {_format_expected_type(expected_type)}")
            continue
        constraint_error = _validate_schema_constraints(name, value, property_schema)
        if constraint_error:
            errors.append(constraint_error)

    if errors:
        return _failure(
            definition,
            "schema",
            "参数校验失败: " + "; ".join(errors),
            params,
            {"schema_errors": errors},
        )
    return ToolParamValidationResult(ok=True)


def _validate_schema_constraints(name: str, value: Any, schema: dict[str, Any]) -> str:
    if not schema:
        return ""
    if "enum" in schema and value not in schema.get("enum", []):
        return f"{name} 必须是枚举值之一: {', '.join(str(item) for item in schema.get('enum', []))}"
    if isinstance(value, (int, float)) and not isinstance(value, bool):
        if "minimum" in schema and value < schema["minimum"]:
            return f"{name} 必须大于等于 {schema['minimum']}"
        if "maximum" in schema and value > schema["maximum"]:
            return f"{name} 必须小于等于 {schema['maximum']}"
    if isinstance(value, str):
        if "minLength" in schema and len(value) < schema["minLength"]:
            return f"{name} 长度必须大于等于 {schema['minLength']}"
        if "maxLength" in schema and len(value) > schema["maxLength"]:
            return f"{name} 长度必须小于等于 {schema['maxLength']}"
    if isinstance(value, list):
        if "minItems" in schema and len(value) < schema["minItems"]:
            return f"{name} 项数必须大于等于 {schema['minItems']}"
        if "maxItems" in schema and len(value) > schema["maxItems"]:
            return f"{name} 项数必须小于等于 {schema['maxItems']}"
    return ""


def _validate_policy(definition: ToolDefinition, params: dict[str, Any]) -> ToolParamValidationResult:
    policy = definition.policy if isinstance(definition.policy, dict) else {}
    if not policy:
        return ToolParamValidationResult(ok=True)

    required_params = _policy_list(policy.get("required_params"))
    missing = [name for name in required_params if name not in params]
    if missing:
        return _failure(
            definition,
            "policy",
            f"Policy 校验失败: 缺少必需参数: {', '.join(missing)}",
            params,
            {"missing_params": missing},
        )

    start_name = str(policy.get("start_line_param") or "start_line")
    end_name = str(policy.get("end_line_param") or "end_line")
    if start_name in params and end_name in params:
        start_line = params.get(start_name)
        end_line = params.get(end_name)
        if not _is_positive_int(start_line) or not _is_positive_int(end_line):
            return _failure(
                definition,
                "policy",
                f"Policy 校验失败: {start_name} 和 {end_name} 必须是正整数",
                params,
                {"policy": {"start_line_param": start_name, "end_line_param": end_name}},
            )
        if int(start_line) > int(end_line):
            return _failure(
                definition,
                "policy",
                f"Policy 校验失败: {start_name} 不能大于 {end_name}",
                params,
                {"policy": {"start_line_param": start_name, "end_line_param": end_name}},
            )
        max_target_lines = _positive_int(policy.get("max_target_lines"))
        if max_target_lines is not None:
            target_line_count = int(end_line) - int(start_line) + 1
            if target_line_count > max_target_lines:
                return _failure(
                    definition,
                    "policy",
                    f"Policy 校验失败: 单次最多处理 {max_target_lines} 行",
                    params,
                    {"target_line_count": target_line_count, "max_target_lines": max_target_lines},
                )

    content_name = str(policy.get("content_param") or "new_content")
    if content_name in params:
        content = params.get(content_name)
        if not isinstance(content, str):
            return _failure(
                definition,
                "policy",
                f"Policy 校验失败: {content_name} 必须是字符串",
                params,
                {"policy": {"content_param": content_name}},
            )
        max_content_lines = _positive_int(policy.get("max_content_lines"))
        if max_content_lines is not None:
            content_line_count = len(content.splitlines()) or 1
            if content_line_count > max_content_lines:
                return _failure(
                    definition,
                    "policy",
                    f"Policy 校验失败: 单次写入内容最多 {max_content_lines} 行",
                    params,
                    {"content_line_count": content_line_count, "max_content_lines": max_content_lines},
                )
        max_content_chars = _positive_int(policy.get("max_content_chars"))
        if max_content_chars is not None and len(content) > max_content_chars:
            return _failure(
                definition,
                "policy",
                f"Policy 校验失败: 单次写入内容最多 {max_content_chars} 个字符",
                params,
                {"content_chars": len(content), "max_content_chars": max_content_chars},
            )
    return ToolParamValidationResult(ok=True)


def _value_matches_declared_type(value: Any, expected_type: Any) -> bool:
    allowed_types = _normalize_expected_types(expected_type)
    if "null" in allowed_types and value is None:
        return True
    if value is None:
        return False
    if "string" in allowed_types or "str" in allowed_types:
        return isinstance(value, str)
    if "integer" in allowed_types or "int" in allowed_types:
        return isinstance(value, int) and not isinstance(value, bool)
    if allowed_types & {"number", "long", "double", "float"}:
        return isinstance(value, (int, float)) and not isinstance(value, bool)
    if "boolean" in allowed_types or "bool" in allowed_types:
        return isinstance(value, bool)
    if "array" in allowed_types:
        return isinstance(value, list)
    if "object" in allowed_types:
        return isinstance(value, dict)
    return True


def _normalize_expected_types(expected_type: Any) -> set[str]:
    if isinstance(expected_type, list):
        return {str(item).strip().lower() for item in expected_type if str(item).strip()}
    return {item.strip().lower() for item in str(expected_type).split("/") if item.strip()}


def _format_expected_type(expected_type: Any) -> str:
    if isinstance(expected_type, list):
        return "/".join(str(item) for item in expected_type)
    return str(expected_type)


def _policy_list(value: Any) -> list[str]:
    if value is None:
        return []
    if isinstance(value, str):
        return [item.strip() for item in value.split(",") if item.strip()]
    if isinstance(value, list):
        return [str(item).strip() for item in value if str(item).strip()]
    return []


def _positive_int(value: Any) -> int | None:
    if isinstance(value, bool):
        return None
    if isinstance(value, int) and value > 0:
        return value
    if isinstance(value, str) and value.isdigit() and int(value) > 0:
        return int(value)
    return None


def _is_positive_int(value: Any) -> bool:
    return isinstance(value, int) and not isinstance(value, bool) and value > 0


def _failure(
    definition: ToolDefinition,
    kind: str,
    message: str,
    params: dict[str, Any],
    extra: dict[str, Any] | None = None,
) -> ToolParamValidationResult:
    error_type = "SchemaValidationError" if kind == "schema" else "PolicyValidationError"
    display_message = message if message.startswith("参数校验失败") else f"参数校验失败: {message}"
    diagnostics = {
        "error_type": error_type,
        "validation_stage": kind,
        "error": display_message,
        "failed_tool_id": definition.tool_id,
        "failed_params": params,
    }
    diagnostics.update(extra or {})
    return ToolParamValidationResult(False, display_message, kind, diagnostics)
