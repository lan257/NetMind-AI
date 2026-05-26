"""
查询导图下节点 Skill

调用 GET /api/nodes/by-map/{mapId} 获取某个导图下的所有节点。
"""

from __future__ import annotations

import json
import urllib.error
import urllib.parse
import urllib.request
from typing import Any


ENDPOINT_TEMPLATE = "/api/nodes/by-map/{mapId}"
DEFAULT_TIMEOUT_SECONDS = 10


def run(params: dict[str, Any]) -> dict[str, Any]:
    """查询指定导图下的节点列表。

    Args:
        params: 参数字典
            - mapId: 导图 ID（必需）
            - __runtime: 运行时配置（由 AgentBuild 注入）

    Returns:
        dict: 包含 success、nodes、status_code 等
    """
    map_id = params.get("mapId")
    if not map_id:
        return {
            "success": False,
            "error": "缺少必需参数: mapId",
            "nodes": [],
            "status_code": 0
        }

    runtime = _read_runtime(params)
    base_url = _read_base_url(params, runtime)
    timeout_seconds = _read_timeout_seconds(params, runtime)

    endpoint = ENDPOINT_TEMPLATE.format(mapId=map_id)
    request_url = _join_url(base_url, endpoint)

    status_code, payload = _get_json(request_url, timeout_seconds)
    api_success = bool(payload.get("success")) if isinstance(payload, dict) else False
    api_message = str(payload.get("message") or "") if isinstance(payload, dict) else ""
    nodes = payload.get("data") if isinstance(payload, dict) else None

    return {
        "success": api_success,
        "message": api_message,
        "status_code": status_code,
        "endpoint": endpoint,
        "nodes": nodes,
        "raw_response": payload
    }


def _read_runtime(params: dict[str, Any]) -> dict[str, Any]:
    runtime = params.get("__runtime")
    return runtime if isinstance(runtime, dict) else {}


def _read_base_url(params: dict[str, Any], runtime: dict[str, Any]) -> str:
    candidate = _first_string(
        _runtime_value(runtime, "netmind_api_base_url"),
        _runtime_value(runtime, "api_base_url"),
        _runtime_value(runtime, "base_url"),
        params.get("netmind_api_base_url"),
        params.get("api_base_url"),
        params.get("base_url"),
        os.environ.get("NETMIND_API_BASE_URL"),
    )
    if not candidate:
        raise ValueError(
            "Missing NetMind API base URL. Provide "
            "tool_runtime.netmind_api_base_url, tool_runtime.shared.netmind_api_base_url, "
            "or NETMIND_API_BASE_URL."
        )
    return _normalize_base_url(candidate)


def _runtime_value(runtime: dict[str, Any], key: str) -> Any:
    tool_runtime = runtime.get("tool")
    if isinstance(tool_runtime, dict) and tool_runtime.get(key) is not None:
        return tool_runtime.get(key)
    shared_runtime = runtime.get("shared")
    if isinstance(shared_runtime, dict) and shared_runtime.get(key) is not None:
        return shared_runtime.get(key)
    return runtime.get(key)


def _read_timeout_seconds(params: dict[str, Any], runtime: dict[str, Any]) -> float:
    raw_timeout = _first_value(
        _runtime_value(runtime, "timeout_seconds"),
        params.get("timeout_seconds"),
        DEFAULT_TIMEOUT_SECONDS,
    )
    try:
        timeout_seconds = float(raw_timeout)
    except (TypeError, ValueError) as exc:
        raise ValueError("timeout_seconds must be a number") from exc
    if timeout_seconds <= 0:
        raise ValueError("timeout_seconds must be greater than 0")
    return timeout_seconds


def _get_json(url: str, timeout_seconds: float) -> tuple[int, dict[str, Any]]:
    request = urllib.request.Request(
        url,
        method="GET",
        headers={"Accept": "application/json"},
    )
    try:
        with urllib.request.urlopen(request, timeout=timeout_seconds) as response:
            status_code = int(response.status)
            return status_code, _decode_json_response(response.read())
    except urllib.error.HTTPError as exc:
        status_code = int(exc.code)
        payload = _decode_json_response(exc.read())
        if payload:
            return status_code, payload
        return status_code, {
            "success": False,
            "message": f"HTTP {status_code}",
            "data": None
        }
    except urllib.error.URLError as exc:
        raise RuntimeError(f"Failed to call NetMind endpoint: {exc.reason}") from exc


def _decode_json_response(body: bytes) -> dict[str, Any]:
    if not body:
        return {}
    try:
        payload = json.loads(body.decode("utf-8"))
    except json.JSONDecodeError as exc:
        raise ValueError("NetMind endpoint returned non-JSON content") from exc
    if not isinstance(payload, dict):
        raise ValueError("NetMind endpoint JSON response must be an object")
    return payload


def _join_url(base_url: str, endpoint: str) -> str:
    normalized_endpoint = endpoint if endpoint.startswith("/") else f"/{endpoint}"
    return urllib.parse.urljoin(f"{base_url}/", normalized_endpoint.lstrip("/"))


def _normalize_base_url(base_url: str) -> str:
    stripped = base_url.strip().rstrip("/")
    parsed = urllib.parse.urlparse(stripped)
    if parsed.scheme not in {"http", "https"} or not parsed.netloc:
        raise ValueError("NetMind API base URL must be an absolute http(s) URL")
    return stripped


def _first_string(*values: Any) -> str:
    for value in values:
        if isinstance(value, str) and value.strip():
            return value.strip()
    return ""


def _first_value(*values: Any) -> Any:
    for value in values:
        if value is not None:
            return value
    return None
