"""Select prompt-only Skills for the current turn."""

from __future__ import annotations

import re
from dataclasses import dataclass
from typing import Any

from .schemas import PromptSkillDefinition, ToolDefinition


DEFAULT_MAX_SKILLS = 3


@dataclass(frozen=True)
class SkillSelection:
    """A selected prompt-only Skill and its relevance score."""

    definition: PromptSkillDefinition
    score: int
    matched_terms: list[str]


def select_prompt_skills(
    skill_definitions: list[PromptSkillDefinition],
    request_text: str,
    context: dict[str, Any] | None = None,
    tool_definitions: list[ToolDefinition] | None = None,
    max_skills: int = DEFAULT_MAX_SKILLS,
) -> list[PromptSkillDefinition]:
    """Return the most relevant prompt-only Skills for this turn."""
    return [
        item.definition
        for item in rank_prompt_skills(
            skill_definitions=skill_definitions,
            request_text=request_text,
            context=context,
            tool_definitions=tool_definitions,
            max_skills=max_skills,
        )
    ]


def rank_prompt_skills(
    skill_definitions: list[PromptSkillDefinition],
    request_text: str,
    context: dict[str, Any] | None = None,
    tool_definitions: list[ToolDefinition] | None = None,
    max_skills: int = DEFAULT_MAX_SKILLS,
) -> list[SkillSelection]:
    """Score candidate Skills and return the highest-ranking matches."""
    if max_skills <= 0:
        return []
    available_tool_ids = {item.tool_id for item in tool_definitions or []}
    query_text = _selection_query(request_text, context or {})
    scored: list[SkillSelection] = []
    for index, definition in enumerate(skill_definitions):
        relevance, matched_terms = _score_skill(definition, query_text, available_tool_ids)
        if relevance <= 0:
            continue
        tie_breaker = max(0, len(skill_definitions) - index)
        rank_score = relevance * 1_000_000 + int(definition.priority) * 1000 + tie_breaker
        scored.append(SkillSelection(definition, rank_score, matched_terms))
    scored.sort(key=lambda item: item.score, reverse=True)
    return [
        SkillSelection(item.definition, item.score // 1_000_000, item.matched_terms)
        for item in scored[:max_skills]
    ]


def _score_skill(
    definition: PromptSkillDefinition,
    query_text: str,
    available_tool_ids: set[str],
) -> tuple[int, list[str]]:
    haystacks = {
        "id": definition.skill_id,
        "name": definition.skill_name,
        "scope": definition.scope,
        "category": definition.category,
        "tags": " ".join(definition.tags),
        "trigger": definition.trigger,
        "description": definition.description,
        "prompt": definition.prompt,
        "workflow": definition.workflow_text,
        "tools": " ".join(definition.available_tools),
    }
    score = 0
    matched_terms: list[str] = []
    for field, text in haystacks.items():
        if not text:
            continue
        weight = _field_weight(field)
        for term in _candidate_terms(text):
            if term and term in query_text:
                score += weight
                if term not in matched_terms:
                    matched_terms.append(term)
    if score > 0 and available_tool_ids and any(tool_id in available_tool_ids for tool_id in definition.available_tools):
        score += 2
    return score, matched_terms


def _field_weight(field: str) -> int:
    if field in {"id", "name", "scope", "tags", "tools"}:
        return 8
    if field in {"trigger", "category"}:
        return 5
    if field == "description":
        return 3
    return 1


def _candidate_terms(text: str) -> list[str]:
    normalized = text.casefold()
    terms: list[str] = []
    for token in re.findall(r"[a-z0-9_]{3,}", normalized):
        terms.append(token)
    for segment in re.split(r"[\s,，。；;:：、/\\|()\[\]{}<>\"'`]+", normalized):
        segment = segment.strip()
        if len(segment) >= 2 and not re.fullmatch(r"[a-z0-9_]+", segment):
            terms.append(segment)
    return _dedupe(terms)


def _selection_query(request_text: str, context: dict[str, Any]) -> str:
    task_state = context.get("task_state") if isinstance(context.get("task_state"), dict) else {}
    parts = [
        request_text,
        str(task_state.get("root_goal") or ""),
        " ".join(str(item) for item in task_state.get("remaining_items") or []),
    ]
    return "\n".join(parts).casefold()


def _dedupe(values: list[str]) -> list[str]:
    result: list[str] = []
    seen: set[str] = set()
    for value in values:
        if value in seen:
            continue
        result.append(value)
        seen.add(value)
    return result
