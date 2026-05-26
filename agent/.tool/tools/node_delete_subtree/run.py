"""
删除节点子树 Skill

调用 DELETE /api/nodes/{id}/subtree 删除指定节点及其子孙节点
"""

from __future__ import annotations

import json
import os
import urllib.error
import urllib.parse
import urllib.request
from typing import Any

DEFAULT_TIMEOUT_SECONDS = 10


def run(params: dict[str, Any]) -> dict[str, Any]:
    """Delete a node subtree by node ID.

    Required params:
        id (int): The node ID

    Optional runtime params:
        __runtime.shared.netmind_api_base_url
        __runtime.shared.timeout_seconds
    """
    node_id = params.get("id")
    if node_id is None:
        return {"success": False, "error": "缺少必需参数: id", "status_code": 0, "endpoint": "", "deleted": False, "affectedCount": 0}

    runtime = params.get("__runtime") or {}
    base_url = _read_base_url(runtime)
    endpoint = f"/api/nodes/{node_id}/subtree"
    timeout = _read_timeout(runtime)
    request_url = _join_url(base_url, endpoint)

    status_code, payload = _delete_json(request_url, timeout)
    api_success = bool(payload.get("success")) if isinstance(payload, dict) else False
    api_message = str(payload.get("message") or "") if isinstance(payload, dict) else ""
    data = payload.get("data") if isinstance(payload, dict) else None
    deleted = data.get("deleted", False) if isinstance(data, dict) else False
    affected_count = data.get("affectedCount", 0) if isinstance(data, dict) else 0

    return {
        "success": api_success,
        "message": api_message,
        "status_code": status_code,
        "endpoint": endpoint,
        "deleted": deleted,
        "affectedCount": affected_count,
        "raw_response": payload,
    }


def _read_base_url(runtime: dict[str, Any]) -> str:
    candidate = None
    tool_runtime = runtime.get("tool")
    if isinstance(tool_runtime, dict):
        candidate = tool_runtime.get("netmind_api_base_url")
    if not candidate:
        shared = runtime.get("shared")
        if isinstance(shared, dict):
            candidate = shared.get("netmind_api_base_url")
    if not candidate:
        candidate = runtime.get("netmind_api_base_url")
    if not candidate:
        candidate = os.environ.get("NETMIND_API_BASE_URL")
    if not candidate:
        raise ValueError("Missing NetMind API base URL")
    return _normalize_base_url(candidate)


def _read_timeout(runtime: dict[str, Any]) -> float:
    tool_runtime = runtime.get("tool")
    if isinstance(tool_runtime, dict) and tool_runtime.get("timeout_seconds"):
        return float(tool_runtime["timeout_seconds"])
    shared = runtime.get("shared")
    if isinstance(shared, dict) and shared.get("timeout_seconds"):
        return float(shared["timeout_seconds"])
    if runtime.get("timeout_seconds"):
        return float(runtime["timeout_seconds"])
    return DEFAULT_TIMEOUT_SECONDS


def _delete_json(url: str, timeout: float) -> tuple[int, dict]:
    request = urllib.request.Request(url, method="DELETE", headers={"Accept": "application/json"})
    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            return int(response.status), _decode_json(response.read())
    except urllib.error.HTTPError as exc:
        return int(exc.code), _decode_json(exc.read())
    except urllib.error.URLError as exc:
        raise RuntimeError(f"Failed to delete node subtree: {exc.reason}")


def _decode_json(body: bytes) -> dict:
    if not body:
        return {}
    try:
        payload = json.loads(body.decode("utf-8"))
    except json.JSONDecodeError:
        raise ValueError("Response is not valid JSON")
    if not isinstance(payload, dict):
        raise ValueError("Response JSON must be an object")
    return payload


def _join_url(base: str, endpoint: str) -> str:
    norm_endpoint = endpoint if endpoint.startswith("/") else f"/{endpoint}"
    return urllib.parse.urljoin(f"{base}/", norm_endpoint.lstrip("/"))


def _normalize_base_url(url: str) -> str:
    stripped = url.strip().rstrip("/")
    parsed = urllib.parse.urlparse(stripped)
    if parsed.scheme not in {"http", "https"} or not parsed.netloc:
        raise ValueError("Base URL must be absolute http(s)")
    return stripped
