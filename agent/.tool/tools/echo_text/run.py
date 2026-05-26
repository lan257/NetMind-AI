"""Echo text skill implementation."""

from __future__ import annotations

from typing import Any


def run(params: dict[str, Any]) -> dict[str, Any]:
    """Return the input text for no-permission Skill flow verification."""
    result: dict[str, Any] = {"text": str(params.get("text") or "")}
    if params.get("include_runtime"):
        result["runtime"] = params.get("__runtime")
    return result

