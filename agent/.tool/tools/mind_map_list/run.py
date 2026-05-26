from __future__ import annotations

import json
import urllib.error
import urllib.parse
import urllib.request
from typing import Any

MIND_MAP_LIST_ENDPOINT = "/api/mind-maps"
DEFAULT_TIMEOUT_SECONDS = 10

def run(params: dict[str, Any]) -> dict[str, Any]:
    """查询全部思维导图列表。

    调用后端 GET /api/mind-maps 接口，返回 MindMapDto[]。
    """
    runtime = params.get("__runtime", {})
    base_url = _get_base_url(runtime, params)
    timeout = _get_timeout(runtime, params)
    url = urllib.parse.urljoin(f"{base_url}/", MIND_MAP_LIST_ENDPOINT.lstrip("/"))

    request = urllib.request.Request(
        url,
        method="GET",
        headers={"Accept": "application/json"},
    )

    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            status_code = response.status
            payload = json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        return {
            "success": False,
            "message": f"HTTP {e.code}: {e.reason}",
            "status_code": e.code,
            "data": None,
        }
    except Exception as e:
        return {
            "success": False,
            "message": str(e),
            "status_code": 0,
            "data": None,
        }

    return {
        "success": payload.get("success", False),
        "message": payload.get("message", ""),
        "status_code": status_code,
        "data": payload.get("data"),
    }

def _get_base_url(runtime: dict[str, Any], params: dict[str, Any]) -> str:
    candidates = [
        runtime.get("shared", {}).get("netmind_api_base_url"),
        runtime.get("skill", {}).get("netmind_api_base_url"),
        params.get("netmind_api_base_url"),
    ]
    for url in candidates:
        if url and isinstance(url, str):
            return url.rstrip("/")
    raise ValueError("Missing NetMind API base URL. Provide __runtime or params.")

def _get_timeout(runtime: dict[str, Any], params: dict[str, Any]) -> float:
    timeout = (
        runtime.get("shared", {}).get("timeout_seconds")
        or runtime.get("skill", {}).get("timeout_seconds")
        or params.get("timeout_seconds")
        or DEFAULT_TIMEOUT_SECONDS
    )
    try:
        return float(timeout)
    except (TypeError, ValueError):
        return DEFAULT_TIMEOUT_SECONDS