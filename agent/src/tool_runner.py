"""Execute approved ToolCallRecord entries."""

from __future__ import annotations

import json
import os
import subprocess
import sys
from pathlib import Path
from typing import Any

from .json_utils import make_json_safe
from .registry_utils import is_relative_to


PROJECT_ROOT = Path(__file__).resolve().parents[1]
TOOL_SCRIPTS_ROOT = PROJECT_ROOT / ".tool" / "tools"
RUNTIME_RESERVED_KEYS = {"shared", "tools", "by_tool"}
DEFAULT_TOOL_TIMEOUT_SECONDS = 30.0
SENSITIVE_KEY_PARTS = ("api_key", "apikey", "authorization", "cookie", "password", "secret", "token")


def execute_ready_tool_calls(
    tool_calls: list[dict[str, Any]],
    tool_runtime: dict[str, Any] | None = None,
) -> list[dict[str, Any]]:
    """Execute approved Tool calls and return updated records."""
    executed_records: list[dict[str, Any]] = []
    for raw_record in tool_calls:
        record = _normalize_tool_call_record(raw_record)
        cloned = dict(record)
        cloned["execution"] = dict(record.get("execution") or {})
        if cloned["execution"].get("status") != "ready":
            executed_records.append(cloned)
            continue

        cloned["execution"]["status"] = "running"
        cloned["execution"]["logs"] = [
            f"开始执行 Tool: {cloned.get('tool_id', '')}",
            f"调用参数: {_format_params_for_log(cloned.get('params') or {})}",
        ]
        try:
            result, run_logs = _run_tool_script(cloned, tool_runtime or {})
            cloned["execution"]["logs"].extend(run_logs)
            envelope = _standardize_result_envelope(result)
            if _result_reports_failure(envelope):
                error = _extract_result_error(envelope)
                cloned["execution"]["logs"].append(f"Tool 返回失败: {_redact_text(error)}")
                cloned["execution"].update(
                    {
                        "status": "failed",
                        "success": False,
                        "result": envelope,
                        "error": error,
                        "diagnostics": _build_failure_diagnostics(cloned, "ToolResultError", error),
                    }
                )
            else:
                cloned["execution"]["logs"].append("Tool 执行成功")
                cloned["execution"].update(
                    {
                        "status": "success",
                        "success": True,
                        "result": envelope,
                        "error": None,
                    }
                )
        except subprocess.TimeoutExpired as exc:
            timeout = _read_timeout_seconds(cloned, tool_runtime or {})
            error = f"Tool 执行超时: {timeout:g} 秒"
            cloned["execution"]["logs"].append(error)
            if exc.stdout:
                cloned["execution"]["logs"].append(f"stdout: {_redact_text(_truncate_text(_decode_process_bytes(exc.stdout)))}")
            if exc.stderr:
                cloned["execution"]["logs"].append(f"stderr: {_redact_text(_truncate_text(_decode_process_bytes(exc.stderr)))}")
            envelope = _standardize_result_envelope(
                {
                    "success": False,
                    "error": error,
                    "diagnostics": {"timeout_seconds": timeout},
                }
            )
            cloned["execution"].update(
                {
                    "status": "failed",
                    "success": False,
                    "result": envelope,
                    "error": error,
                    "diagnostics": _build_failure_diagnostics(cloned, "ToolTimeout", error),
                }
            )
        except Exception as exc:  # noqa: BLE001 - convert tool failures into response JSON.
            error = str(exc)
            cloned["execution"]["logs"].append(f"Tool 执行异常: {type(exc).__name__}: {_redact_text(error)}")
            cloned["execution"].update(
                {
                    "status": "failed",
                    "success": False,
                    "result": _standardize_result_envelope({"success": False, "error": error}),
                    "error": error,
                    "diagnostics": _build_failure_diagnostics(cloned, type(exc).__name__, error),
                }
            )
        executed_records.append(cloned)
    return executed_records


def _run_tool_script(record: dict[str, Any], tool_runtime: dict[str, Any]) -> tuple[Any, list[str]]:
    """Run a Python Tool script in a subprocess by its trusted definition path."""
    script_path = _resolve_script_path(str(record["definition"]["script_path"]))
    tool_id = str(record.get("tool_id") or "")
    params = dict(record.get("params") or {})
    params["__runtime"] = _build_runtime_for_tool(tool_runtime, tool_id)
    timeout = _read_timeout_seconds(record, tool_runtime)
    payload = json.dumps({"script_path": str(script_path), "params": params}, ensure_ascii=False)
    completed = subprocess.run(
        [sys.executable, "-c", _SUBPROCESS_RUNNER_CODE],
        input=payload.encode("utf-8"),
        capture_output=True,
        cwd=str(PROJECT_ROOT),
        env=_build_subprocess_env(),
        timeout=timeout,
        check=False,
    )
    stdout = _decode_process_bytes(completed.stdout)
    stderr = _decode_process_bytes(completed.stderr)
    logs = _build_subprocess_logs(stdout, stderr)
    runner_payload = _parse_subprocess_payload(stdout)
    if completed.returncode != 0 and runner_payload is None:
        raise RuntimeError(_redact_text(_truncate_text(stderr or stdout or "Tool subprocess failed")))
    if runner_payload is None:
        raise RuntimeError("Tool subprocess did not return a JSON result")
    if not runner_payload.get("ok"):
        error = str(runner_payload.get("error") or "Tool subprocess failed")
        error_type = str(runner_payload.get("error_type") or "ToolSubprocessError")
        raise RuntimeError(f"{error_type}: {error}")
    return runner_payload.get("result"), logs


def _resolve_script_path(script_path: str) -> Path:
    """Resolve Tool script paths and require the final file to live under `.tool/tools`."""
    candidate = (PROJECT_ROOT / script_path).resolve()
    if not candidate.exists():
        raise FileNotFoundError(f"Tool 脚本不存在: {script_path}")
    if not candidate.is_file():
        raise IsADirectoryError(f"Tool 脚本路径不是文件: {script_path}")
    if not is_relative_to(candidate, TOOL_SCRIPTS_ROOT.resolve()):
        raise PermissionError(f"Tool 脚本必须位于 .tool/tools 内: {script_path}")
    return candidate


def _build_runtime_for_tool(tool_runtime: dict[str, Any], tool_id: str) -> dict[str, Any]:
    """Build the runtime object injected into params['__runtime'] for one Tool."""
    shared_runtime: dict[str, Any] = {
        key: value
        for key, value in tool_runtime.items()
        if key not in RUNTIME_RESERVED_KEYS
    }

    configured_shared = tool_runtime.get("shared")
    if isinstance(configured_shared, dict):
        shared_runtime.update(configured_shared)

    tool_runtime_map = tool_runtime.get("tools")
    if not isinstance(tool_runtime_map, dict):
        tool_runtime_map = tool_runtime.get("by_tool")
    tool_specific = {}
    if isinstance(tool_runtime_map, dict):
        raw_tool_runtime = tool_runtime_map.get(tool_id)
        if isinstance(raw_tool_runtime, dict):
            tool_specific = dict(raw_tool_runtime)

    return {
        "tool_id": tool_id,
        "shared": shared_runtime,
        "tool": tool_specific,
    }


def _result_reports_failure(result: Any) -> bool:
    """Treat standard Tool result envelopes with success=false as execution failures."""
    return isinstance(result, dict) and result.get("success") is False


def _extract_result_error(result: Any) -> str:
    """Extract a useful error message from a failed Tool result envelope."""
    if not isinstance(result, dict):
        return "Tool 返回失败"
    for key in ("error", "message", "reason"):
        value = result.get(key)
        if value is not None and str(value).strip():
            return str(value).strip()
    return "Tool 返回 success=false，但未提供错误原因"


def _build_failure_diagnostics(record: dict[str, Any], error_type: str, error: str) -> dict[str, Any]:
    """Build compact diagnostics for model retry and terminal display."""
    tool_id = str(record.get("tool_id") or "")
    return {
        "error_type": error_type,
        "error": _redact_text(error),
        "failed_tool_id": tool_id,
        "failed_params": _redact_sensitive_value(dict(record.get("params") or {})),
        "retry_guidance": "下一轮不要原样重复同一个 tool_id 和 params；先根据错误修正参数，或改用搜索/目录/读取类 Tool 定位信息。",
    }


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


def _format_params_for_log(params: dict[str, Any]) -> str:
    """Serialize params for logs without risking an oversized response."""
    text = json.dumps(make_json_safe(_redact_sensitive_value(params)), ensure_ascii=False, sort_keys=True)
    return _truncate_text(text, limit=500)


def _read_timeout_seconds(record: dict[str, Any], tool_runtime: dict[str, Any]) -> float:
    definition = record.get("definition") if isinstance(record.get("definition"), dict) else {}
    runtime = _build_runtime_for_tool(tool_runtime, str(record.get("tool_id") or ""))
    candidates = [
        definition.get("timeout_seconds"),
        _runtime_value(runtime, "tool_timeout_seconds"),
        _runtime_value(runtime, "runner_timeout_seconds"),
        DEFAULT_TOOL_TIMEOUT_SECONDS,
    ]
    for value in candidates:
        if value is None:
            continue
        try:
            timeout = float(value)
        except (TypeError, ValueError):
            continue
        if timeout > 0:
            return timeout
    return DEFAULT_TOOL_TIMEOUT_SECONDS


def _runtime_value(runtime: dict[str, Any], key: str) -> Any:
    tool_runtime = runtime.get("tool")
    if isinstance(tool_runtime, dict) and tool_runtime.get(key) is not None:
        return tool_runtime.get(key)
    shared_runtime = runtime.get("shared")
    if isinstance(shared_runtime, dict) and shared_runtime.get(key) is not None:
        return shared_runtime.get(key)
    return runtime.get(key)


def _standardize_result_envelope(result: Any) -> dict[str, Any]:
    """Wrap arbitrary Tool output in the standard result envelope."""
    safe_result = make_json_safe(result)
    if isinstance(safe_result, dict):
        success = safe_result.get("success")
        if not isinstance(success, bool):
            success = True
        envelope = {
            "success": success,
            "data": safe_result.get("data", {key: value for key, value in safe_result.items() if key != "success"}),
            "message": str(safe_result.get("message") or ""),
            "error": safe_result.get("error"),
            "diagnostics": safe_result.get("diagnostics") if isinstance(safe_result.get("diagnostics"), dict) else {},
        }
        for key, value in safe_result.items():
            envelope.setdefault(key, value)
        return envelope
    return {
        "success": True,
        "data": safe_result,
        "message": "",
        "error": None,
        "diagnostics": {},
    }


def _build_subprocess_logs(stdout: str, stderr: str) -> list[str]:
    logs: list[str] = []
    stdout_lines = [line for line in stdout.splitlines() if line.strip()]
    if stdout_lines and _looks_like_runner_payload(stdout_lines[-1]):
        stdout_lines = stdout_lines[:-1]
    if stdout_lines:
        logs.append(f"stdout: {_redact_text(_truncate_text(chr(10).join(stdout_lines)))}")
    if stderr.strip():
        logs.append(f"stderr: {_redact_text(_truncate_text(stderr))}")
    return logs


def _build_subprocess_env() -> dict[str, str]:
    env = dict(os.environ)
    env["PYTHONIOENCODING"] = "utf-8"
    env["PYTHONUTF8"] = "1"
    return env


def _decode_process_bytes(value: bytes | str | None) -> str:
    if value is None:
        return ""
    if isinstance(value, str):
        return value
    try:
        return value.decode("utf-8")
    except UnicodeDecodeError:
        return value.decode("utf-8", errors="replace")


def _looks_like_runner_payload(line: str) -> bool:
    try:
        payload = json.loads(line)
    except json.JSONDecodeError:
        return False
    return isinstance(payload, dict) and "ok" in payload


def _parse_subprocess_payload(stdout: str) -> dict[str, Any] | None:
    for line in reversed(stdout.splitlines()):
        try:
            payload = json.loads(line)
        except json.JSONDecodeError:
            continue
        if isinstance(payload, dict) and "ok" in payload:
            return payload
    return None


def _truncate_text(text: str, limit: int = 1000) -> str:
    if len(text) <= limit:
        return text
    return text[: max(0, limit - 3)] + "..."


def _redact_sensitive_value(value: Any, key: str = "") -> Any:
    if _is_sensitive_key(key):
        return "***"
    if isinstance(value, dict):
        return {item_key: _redact_sensitive_value(item_value, str(item_key)) for item_key, item_value in value.items()}
    if isinstance(value, list):
        return [_redact_sensitive_value(item) for item in value]
    if isinstance(value, tuple):
        return [_redact_sensitive_value(item) for item in value]
    if isinstance(value, str):
        return _redact_text(value)
    return value


def _is_sensitive_key(key: str) -> bool:
    normalized = key.replace("-", "_").lower()
    return any(part in normalized for part in SENSITIVE_KEY_PARTS)


def _redact_text(text: str) -> str:
    redacted = str(text)
    for marker in ("api_key", "apikey", "token", "password", "secret", "cookie", "authorization"):
        redacted = _redact_marker_value(redacted, marker)
    return redacted


def _redact_marker_value(text: str, marker: str) -> str:
    lowered = text.lower()
    start = 0
    pieces: list[str] = []
    while True:
        index = lowered.find(marker, start)
        if index < 0:
            pieces.append(text[start:])
            return "".join(pieces)
        pieces.append(text[start:index])
        end = index + len(marker)
        pieces.append(text[index:end])
        cursor = end
        while cursor < len(text) and text[cursor] in " \t:=\"'":
            pieces.append(text[cursor])
            cursor += 1
        while cursor < len(text) and text[cursor] not in " \t\r\n,;{}[]\"'":
            cursor += 1
        pieces.append("***")
        start = cursor


_SUBPROCESS_RUNNER_CODE = r'''
from __future__ import annotations

import importlib.util
import json
import sys
from pathlib import Path
from typing import Any


def _safe(data: Any) -> Any:
    if data is None or isinstance(data, (str, int, float, bool)):
        return data
    if isinstance(data, Path):
        return str(data)
    if isinstance(data, dict):
        return {str(key): _safe(value) for key, value in data.items()}
    if isinstance(data, (list, tuple)):
        return [_safe(item) for item in data]
    if isinstance(data, set):
        return [_safe(item) for item in sorted(data, key=repr)]
    return str(data)


def _emit(payload: dict[str, Any]) -> None:
    print(json.dumps(_safe(payload), ensure_ascii=False), flush=True)


try:
    request = json.loads(sys.stdin.read() or "{}")
    script_path = str(request.get("script_path") or "")
    params = request.get("params")
    if not isinstance(params, dict):
        raise ValueError("Tool params must be an object")
    spec = importlib.util.spec_from_file_location("agentbuild_tool", script_path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Unable to load Tool script: {script_path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if not hasattr(module, "run"):
        raise RuntimeError(f"Tool script is missing run(params): {script_path}")
    _emit({"ok": True, "result": module.run(params)})
except Exception as exc:  # noqa: BLE001
    _emit({"ok": False, "error_type": type(exc).__name__, "error": str(exc)})
    sys.exit(1)
'''
