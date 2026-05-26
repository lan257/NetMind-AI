"""LLM client boundary with JSON validation and retry support."""

from __future__ import annotations

import json
import re
import urllib.error
import urllib.request
from typing import Any

from .prompt_builder import build_retry_prompt
from .schemas import AgentRequest, ToolDefinition


DEFAULT_TIMEOUT = 60
DEFAULT_MAX_RETRIES = 3
FAKE_MODEL_NAMES = {"fake", "mock", "test"}


def call_llm(
    prompt: str,
    request: AgentRequest,
    tool_definitions: list[ToolDefinition] | None = None,
    tool_results: list[dict[str, Any]] | None = None,
) -> dict[str, Any]:
    """Call the configured LLM and return a validated Agent Kernel JSON object."""
    selected_tools = tool_definitions or []
    previous_tool_results = tool_results or []
    model_name = str(request.model_config.get("model_name") or "").lower()
    if model_name in FAKE_MODEL_NAMES:
        return _call_fake_llm(request, selected_tools, previous_tool_results)
    return _call_remote_llm(prompt, request.model_config)


def parse_tool_call_drafts(ai_response: dict[str, Any]) -> list[dict[str, Any]]:
    """Parse Tool call drafts from the model output."""
    drafts = ai_response.get("tool_call_drafts")
    return [dict(draft) for draft in drafts or [] if isinstance(draft, dict)]


def validate_llm_response(data: Any) -> tuple[bool, str]:
    """Validate the model response shape required by Agent Kernel."""
    if not isinstance(data, dict):
        return False, "响应必须是 JSON 对象"
    for field in ("agent_target", "main_text", "context_update"):
        if field not in data:
            return False, f"缺少字段: {field}"
    if not isinstance(data["agent_target"], str):
        return False, "agent_target 必须是字符串"
    if not isinstance(data["main_text"], str):
        return False, "main_text 必须是字符串"
    drafts = data.get("tool_call_drafts")
    if drafts is None:
        return False, "缺少字段: tool_call_drafts"
    if not isinstance(drafts, list):
        return False, "tool_call_drafts 必须是数组"
    normalized_tool_drafts: list[dict[str, Any]] = []
    for index, draft in enumerate(drafts, start=1):
        if not isinstance(draft, dict):
            return False, f"tool_call_drafts[{index}] 必须是对象"
        if "skill_id" in draft:
            return False, f"tool_call_drafts[{index}] 不支持字段 skill_id"
        tool_id = draft.get("tool_id")
        if not isinstance(tool_id, str):
            return False, f"tool_call_drafts[{index}].tool_id 必须是字符串"
        if not isinstance(draft.get("params"), dict):
            return False, f"tool_call_drafts[{index}].params 必须是对象"
        if not isinstance(draft.get("reason"), str):
            return False, f"tool_call_drafts[{index}].reason 必须是字符串"
        if "expected_result" in draft and not isinstance(draft.get("expected_result"), str):
            return False, f"tool_call_drafts[{index}].expected_result 必须是字符串"
        normalized_tool_draft = dict(draft)
        normalized_tool_draft["tool_id"] = tool_id
        normalized_tool_drafts.append(normalized_tool_draft)
    context_update = data["context_update"]
    if not isinstance(context_update, dict):
        return False, "context_update 必须是对象"
    if not isinstance(context_update.get("working_memory", {}), dict):
        return False, "context_update.working_memory 必须是对象"
    if not isinstance(context_update.get("summary", ""), str):
        return False, "context_update.summary 必须是字符串"
    if "needs_continuation" in data and not isinstance(data["needs_continuation"], bool):
        return False, "needs_continuation 必须是 boolean"
    data["tool_call_drafts"] = normalized_tool_drafts
    data["context_update"].setdefault("working_memory", {})
    data["context_update"].setdefault("summary", "")
    data.setdefault("needs_continuation", False)
    return True, ""


def parse_model_content(content: str) -> dict[str, Any]:
    """Parse JSON from model content, including fenced JSON blocks."""
    stripped = content.strip()
    last_decode_error: json.JSONDecodeError | None = None
    try:
        parsed = json.loads(stripped)
        if isinstance(parsed, dict):
            return parsed
    except json.JSONDecodeError as exc:
        last_decode_error = exc

    fenced = re.search(r"```(?:json)?\s*(\{.*?\})\s*```", stripped, re.DOTALL)
    if fenced:
        fenced_content = fenced.group(1)
        try:
            parsed = json.loads(fenced_content)
            if isinstance(parsed, dict):
                return parsed
        except json.JSONDecodeError as exc:
            raise ValueError(_format_json_decode_error(exc, fenced_content)) from exc

    start = stripped.find("{")
    end = stripped.rfind("}")
    if start >= 0 and end > start:
        embedded_content = stripped[start : end + 1]
        try:
            parsed = json.loads(embedded_content)
            if isinstance(parsed, dict):
                return parsed
        except json.JSONDecodeError as exc:
            raise ValueError(_format_json_decode_error(exc, embedded_content)) from exc
    if last_decode_error is not None:
        raise ValueError(_format_json_decode_error(last_decode_error, stripped)) from last_decode_error
    raise ValueError("模型返回内容不是有效 JSON；未找到 JSON 对象边界")


def _format_json_decode_error(exc: json.JSONDecodeError, content: str) -> str:
    """Format JSON decode errors with a local snippet for repair prompts."""
    return (
        "模型返回内容不是有效 JSON: "
        f"{exc.msg}; line {exc.lineno} column {exc.colno} (char {exc.pos}); "
        f"错误附近片段: {_snippet_around(content, exc.pos)}"
    )


def _snippet_around(content: str, pos: int, radius: int = 160) -> str:
    """Return a compact escaped snippet around a character offset."""
    start = max(0, pos - radius)
    end = min(len(content), pos + radius)
    prefix = "..." if start > 0 else ""
    suffix = "..." if end < len(content) else ""
    snippet = content[start:end]
    return prefix + json.dumps(snippet, ensure_ascii=False)[1:-1] + suffix


def _format_invalid_content_for_retry(content: str, limit: int = 6000) -> str:
    """Keep retry prompts focused when the invalid model output is very large."""
    if len(content) <= limit:
        return content
    head_limit = limit // 2
    tail_limit = limit - head_limit
    return (
        content[:head_limit]
        + f"\n\n...（中间 {len(content) - limit} 个字符已省略，避免修复 Prompt 过长）...\n\n"
        + content[-tail_limit:]
    )


def _call_remote_llm(prompt: str, model_config: dict[str, Any]) -> dict[str, Any]:
    """Call a remote OpenAI-compatible chat completion API with retries."""
    api_url = str(model_config.get("api_url") or "")
    api_key = str(model_config.get("api_key") or "")
    model_name = str(model_config.get("model_name") or "")
    if not api_url or not api_key or not model_name:
        return _error_response("model_config 缺少 api_url、api_key 或 model_name")

    max_retries = int(model_config.get("max_retries", DEFAULT_MAX_RETRIES))
    timeout = int(model_config.get("timeout", DEFAULT_TIMEOUT))
    current_prompt = prompt
    last_error = ""
    last_content = ""

    for attempt in range(max_retries + 1):
        try:
            raw_response = _post_chat_completion(
                api_url=api_url,
                api_key=api_key,
                model_config=model_config,
                prompt=current_prompt,
                timeout=timeout,
            )
            content = _extract_chat_content(raw_response)
            last_content = content
            parsed = parse_model_content(content)
            is_valid, error = validate_llm_response(parsed)
            if is_valid:
                return parsed
            last_error = error
        except (ValueError, KeyError, urllib.error.URLError) as exc:
            last_error = str(exc)
        if attempt < max_retries:
            current_prompt = build_retry_prompt(prompt, _format_invalid_content_for_retry(last_content), last_error)

    return _error_response(f"AI 返回结构校验失败: {last_error}")


def _post_chat_completion(
    api_url: str,
    api_key: str,
    model_config: dict[str, Any],
    prompt: str,
    timeout: int,
) -> dict[str, Any]:
    """Post one chat completion request and parse the HTTP JSON body."""
    payload = {
        "model": model_config["model_name"],
        "messages": [{"role": "user", "content": prompt}],
        "temperature": float(model_config.get("temperature", 0.2)),
        "max_tokens": int(model_config.get("max_tokens", 4096)),
    }
    if model_config.get("response_format"):
        payload["response_format"] = model_config["response_format"]
    if model_config.get("thinking") is not None:
        payload["thinking"] = model_config["thinking"]
    if isinstance(model_config.get("extra_body"), dict):
        payload.update(model_config["extra_body"])

    body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
    request = urllib.request.Request(
        api_url,
        data=body,
        headers={
            "Content-Type": "application/json",
            "Authorization": f"Bearer {api_key}",
        },
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=timeout) as response:
        response_body = response.read().decode("utf-8")
    parsed = json.loads(response_body)
    if not isinstance(parsed, dict):
        raise ValueError("大模型 API 响应必须是 JSON 对象")
    return parsed


def _extract_chat_content(raw_response: dict[str, Any]) -> str:
    """Extract `choices[0].message.content` from chat completion response."""
    choices = raw_response.get("choices")
    if not isinstance(choices, list) or not choices:
        raise ValueError(f"大模型 API 响应缺少 choices；响应摘要: {_summarize_api_response(raw_response)}")
    first_choice = choices[0]
    if not isinstance(first_choice, dict):
        raise ValueError(f"大模型 API 响应 choices[0] 必须是对象；响应摘要: {_summarize_api_response(raw_response)}")
    message = first_choice.get("message")
    if not isinstance(message, dict):
        raise ValueError(f"大模型 API 响应缺少 message；响应摘要: {_summarize_api_response(raw_response)}")
    content = message.get("content")
    if not isinstance(content, str) or not content.strip():
        raise ValueError(f"大模型 API 响应缺少 content；响应摘要: {_summarize_api_response(raw_response)}")
    return content


def _summarize_api_response(raw_response: dict[str, Any]) -> str:
    """Build a compact, non-sensitive summary for unexpected model API responses."""
    summary: dict[str, Any] = {
        "object": raw_response.get("object"),
        "model": raw_response.get("model"),
    }
    choices = raw_response.get("choices")
    if isinstance(choices, list):
        summary["choices_len"] = len(choices)
        if choices and isinstance(choices[0], dict):
            choice = choices[0]
            summary["finish_reason"] = choice.get("finish_reason")
            message = choice.get("message")
            if isinstance(message, dict):
                content = message.get("content")
                summary["message_keys"] = sorted(str(key) for key in message.keys())
                summary["content_type"] = type(content).__name__
                summary["content_length"] = len(content) if isinstance(content, str) else None
                summary["has_reasoning_content"] = bool(message.get("reasoning_content"))
                summary["has_tool_calls"] = bool(message.get("tool_calls"))
            else:
                summary["message_type"] = type(message).__name__
    elif choices is not None:
        summary["choices_type"] = type(choices).__name__
    return json.dumps(summary, ensure_ascii=False, sort_keys=True)


def _error_response(message: str) -> dict[str, Any]:
    """Build a unified LLM error response for Agent Kernel."""
    return {
        "error": message,
        "agent_target": "大模型调用失败",
        "main_text": message,
        "tool_call_drafts": [],
        "context_update": {
            "working_memory": {},
            "summary": message,
        },
        "needs_continuation": False,
    }


def _call_fake_llm(
    request: AgentRequest,
    tool_definitions: list[ToolDefinition],
    tool_results: list[dict[str, Any]],
) -> dict[str, Any]:
    """Return a deterministic fake LLM response matching the kernel contract."""
    if any(item.get("execution", {}).get("status") == "permission_denied" for item in tool_results):
        return {
            "agent_target": "处理用户拒绝的 Tool 授权",
            "main_text": "用户已拒绝授权，当前不会执行对应 Tool。",
            "tool_call_drafts": [],
            "context_update": {
                "working_memory": {"last_tool_results": tool_results},
                "summary": "用户拒绝 Tool 授权，本轮不执行该 Tool。",
            },
            "needs_continuation": False,
        }

    if tool_results:
        return {
            "agent_target": "基于已执行 Tool 结果生成回复",
            "main_text": "已完成授权 Tool 的执行，当前阶段返回占位摘要。",
            "tool_call_drafts": [],
            "context_update": {
                "working_memory": {"last_tool_results": tool_results},
                "summary": "使用假模型结果完成一轮 Tool 后处理。",
            },
            "needs_continuation": False,
        }

    read_doc_allowed = any(item.tool_id == "read_doc" for item in tool_definitions)
    filepath = _extract_filepath(request.user_text)
    if read_doc_allowed and filepath:
        return {
            "agent_target": "读取文件并生成摘要",
            "main_text": "我需要先读取文件内容，然后才能继续总结。",
            "tool_call_drafts": [
                {
                    "tool_id": "read_doc",
                    "params": {"filepath": filepath},
                    "reason": "需要读取用户指定文件内容",
                    "expected_result": "获得文件内容",
                }
            ],
            "context_update": {
                "working_memory": {"pending_action": "read_doc"},
                "summary": "等待用户确认文件读取权限。",
            },
            "needs_continuation": False,
        }

    return {
        "agent_target": "直接回复用户",
        "main_text": "这是 P1.0 假模型返回，内核脚本框架已完成。",
        "tool_call_drafts": [],
        "context_update": {
            "working_memory": {},
            "summary": "无需调用 Tool。",
        },
        "needs_continuation": False,
    }


def _extract_filepath(user_text: str) -> str:
    """Extract a likely markdown/text file path from user text."""
    quoted = re.search(r"[“\"]([^”\"]+\.(?:md|txt|json|yaml|yml|py))[”\"]", user_text)
    if quoted:
        return quoted.group(1)
    fallback = re.search(r"([A-Za-z]:\\[^\s\"”]+\.(?:md|txt|json|yaml|yml|py))", user_text)
    return fallback.group(1) if fallback else ""
