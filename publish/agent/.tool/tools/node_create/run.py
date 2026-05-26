"""
创建节点 Skill

调用 POST /api/nodes 创建新节点
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
    """Create a new node in a mind map.

    Required params:
        mapId (int): 所属导图 ID
        parentId (int or null): 父节点 ID
        title (str): 节点标题
        content (str or null): 节点内容
        orderNo (int): 同级排序号
        positionX (float or null): 画布 X 坐标
        positionY (float or null): 画布 Y 坐标

    Optional runtime params:
        __runtime.shared.netmind_api_base_url
        __runtime.shared.timeout_seconds
    """
    # 提取业务参数
    map_id = params.get("mapId")
    if map_id is None:
        return {"success": False, "error": "缺少必需参数: mapId", "status_code": 0, "endpoint": "", "raw_response": {}}
    parent_id = params.get("parentId")
    title = params.get("title")
    if not title:
        return {"success": False, "error": "缺少必需参数: title", "status_code": 0, "endpoint": "", "raw_response": {}}
    content = params.get("content")
    order_no = params.get("orderNo", 0)
    position_x = params.get("positionX")
    position_y = params.get("positionY")

    # 构造请求体
    body = {
        "mapId": map_id,
        "parentId": parent_id,
        "title": title,
        "content": content,
        "orderNo": order_no,
        "positionX": position_x,
        "positionY": position_y
    }
    # 移除 None 字段
    body = {k: v for k, v in body.items() if v is not None}

    runtime = params.get("__runtime") or {}
    base_url = _read_base_url(runtime)
    endpoint = "/api/nodes"
    timeout = _read_timeout(runtime)
    request_url = _join_url(base_url, endpoint)

    status_code, payload = _post_json(request_url, body, timeout)
    api_success = bool(payload.get("success")) if isinstance(payload, dict) else False
    api_message = str(payload.get("message") or "") if isinstance(payload, dict) else ""
    data = payload.get("data") if isinstance(payload, dict) else None

    return {
        "success": api_success,
        "message": api_message,
        "status_code": status_code,
        "endpoint": endpoint,
        "node": data,
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


def _post_json(url: str, data: dict, timeout: float) -> tuple[int, dict]:
    request = urllib.request.Request(
        url,
        method="POST",
        headers={"Content-Type": "application/json", "Accept": "application/json"},
        data=json.dumps(data).encode("utf-8")
    )
    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            return int(response.status), _decode_json(response.read())
    except urllib.error.HTTPError as exc:
        return int(exc.code), _decode_json(exc.read())
    except urllib.error.URLError as exc:
        raise RuntimeError(f"Failed to create node: {exc.reason}")


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
