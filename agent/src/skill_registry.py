"""Load prompt-only Skill definitions from `.skill`."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from .registry_utils import load_yaml_like
from .schemas import PromptSkillDefinition


PROJECT_ROOT = Path(__file__).resolve().parents[1]
SKILL_ROOT = PROJECT_ROOT / ".skill"


def load_prompt_skill_definitions(domain: str, skill_root: Path | None = None) -> list[PromptSkillDefinition]:
    """Load prompt-only skills for the selected domain."""
    root = skill_root or SKILL_ROOT
    binding_path = root / "domain_skill_bindings.json"
    if not binding_path.exists():
        return []
    with binding_path.open("r", encoding="utf-8-sig") as file:
        payload = json.load(file)
    bindings = payload.get("domain_skill_bindings")
    if not isinstance(bindings, dict) or "default" not in bindings:
        return []
    list_path = root / str(bindings.get(domain, bindings["default"]))
    if not list_path.exists():
        return []

    definitions: list[PromptSkillDefinition] = []
    for raw in load_yaml_like(list_path).get("skills", []):
        _validate_prompt_skill_payload(raw)
        definition = PromptSkillDefinition.from_dict(raw)
        skill_dir = root / "skills" / definition.skill_id
        _validate_prompt_skill_dir(skill_dir, definition.skill_id)
        workflow_path = (skill_dir / definition.workflow_path).resolve()
        if workflow_path.exists():
            definition = PromptSkillDefinition(
                skill_id=definition.skill_id,
                skill_name=definition.skill_name,
                description=definition.description,
                trigger=definition.trigger,
                prompt=definition.prompt,
                available_tools=definition.available_tools,
                priority=definition.priority,
                scope=definition.scope,
                workflow_path=definition.workflow_path,
                maintenance_path=definition.maintenance_path,
                category=definition.category,
                tags=definition.tags,
                workflow_text=workflow_path.read_text(encoding="utf-8"),
            )
        definitions.append(definition)
    return definitions


def summarize_prompt_skills(skill_definitions: list[PromptSkillDefinition]) -> list[dict[str, Any]]:
    """Return prompt-only skill instructions for the model prompt."""
    return [
        {
            "skill_id": item.skill_id,
            "skill_name": item.skill_name,
            "description": item.description,
            "trigger": item.trigger,
            "prompt": item.prompt,
            "workflow": item.workflow_text,
            "available_tools": item.available_tools,
            "priority": item.priority,
            "scope": item.scope,
            "category": item.category,
            "tags": item.tags,
        }
        for item in skill_definitions
    ]


def validate_prompt_skill_tree(skill_root: Path | None = None) -> None:
    """Validate that `.skill` contains only prompt-only Skill files."""
    root = skill_root or SKILL_ROOT
    skills_root = root / "skills"
    if not skills_root.exists():
        return
    offenders = sorted(path for path in skills_root.rglob("run.py") if path.is_file())
    if offenders:
        formatted = ", ".join(str(path.relative_to(root)) for path in offenders)
        raise ValueError(f".skill 只能包含 prompt-only Skill，不允许 run.py: {formatted}")


def _validate_prompt_skill_payload(raw: dict[str, Any]) -> None:
    forbidden = sorted(set(raw) & {"params", "parameters", "permission", "permission_level", "script_path", "runner"})
    if forbidden:
        skill_id = str(raw.get("skill_id") or "<unknown>")
        raise ValueError(f"Prompt-only Skill {skill_id} 不允许声明可执行字段: {', '.join(forbidden)}")


def _validate_prompt_skill_dir(skill_dir: Path, skill_id: str) -> None:
    run_py = skill_dir / "run.py"
    if run_py.exists():
        raise ValueError(f"Prompt-only Skill {skill_id} 不允许包含 run.py")
