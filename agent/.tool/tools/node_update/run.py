"""更新思维导图节点信息。

调用后端 PUT /api/nodes/{id} 接口更新节点属性。
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
    # 读取必选参数
    node_id = params.get("node_id")
    if node_id is None:
        return {"success": False, "error": "缺少必需参数: node_id"}

    # 读取运行时配置
    runtime = params.get("__runtime", {})
    base_url = _read_base_url(runtime, params)
    endpoint = f"/api/nodes/{node_id}"
    request_url = _join_url(base_url, endpoint)
    timeout = _read_timeout(runtime, params)

    # 构建请求体
    body = {}
    for field in ["parentId", "title", "content", "orderNo", "positionX", "positionY"]:
        if field in params and params[field] is not None:
            body[field] = params[field]

    # 发送 PUT 请求
    data = json.dumps(body).encode("utf-8")
    req = urllib.request.Request(
        request_url,
        data=data,
        method="PUT",
        headers={
            "Content-Type": "application/json",
            "Accept": "application/json",
        },
    )
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            status = resp.status
            payload = json.loads(resp.read().decode("utf-8"))
            return {
                "success": True,
                "status_code": status,
                "endpoint": endpoint,
                "data": payload.get("data"),
                "message": payload.get("message", ""),
            }
    except urllib.error.HTTPError as e:
        status = e.code
        try:
            payload = json.loads(e.read().decode("utf-8"))
        except Exception:
            payload = {"success": False, "message": f"HTTP {status}"}
        return {
            "success": False,
            "status_code": status,
            "endpoint": endpoint,
            "data": payload.get("data"),
            "message": payload.get("message", f"HTTP {status}"),
        }
    except Exception as e:
        return {"success": False, "error": str(e)}


def _read_base_url(runtime: dict, params: dict) -> str:
    candidate = (
        runtime.get("skill", {}).get("netmind_api_base_url")
        or runtime.get("shared", {}).get("netmind_api_base_url")
        or params.get("netmind_api_base_url")
        or os.environ.get("NETMIND_API_BASE_URL")
    )
    if not candidate:
        raise ValueError("Missing NetMind API base URL")
    return candidate.rstrip("/")


def _read_timeout(runtime: dict, params: dict) -> float:
    raw = (
        runtime.get("skill", {}).get("timeout_seconds")
        or runtime.get("shared", {}).get("timeout_seconds")
        or params.get("timeout_seconds")
        or DEFAULT_TIMEOUT_SECONDS
    )
    try:
        return float(raw)
    except (TypeError, ValueError):
        return DEFAULT_TIMEOUT_SECONDS


def _join_url(base: str, endpoint: str) -> str:
    if not endpoint.startswith("/"):
        endpoint = "/" + endpoint
    return urllib.parse.urljoin(base + "/", endpoint.lstrip("/"))
