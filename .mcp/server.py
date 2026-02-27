# /// script
# dependencies = ["fastmcp>=2.0"]
# ///
"""
ZenoHR Context MCP Server
Provides project context, progress tracking, and document access to any agent.
Always call get_context() at the start of every session.
"""

import json
import os
import hashlib
from datetime import datetime, timezone
from pathlib import Path

from fastmcp import FastMCP

# Project root is the parent of .mcp/
PROJECT_ROOT = Path(__file__).parent.parent

mcp = FastMCP(
    name="zenohr-context",
    instructions=(
        "ZenoHR project context server. "
        "ALWAYS call get_context() first in every session before doing any work. "
        "Call update_task_status() after completing any task. "
        "Call log_decision() when an architectural decision is made."
    ),
)

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _read(relative_path: str) -> str:
    """Read a file relative to project root."""
    full = PROJECT_ROOT / relative_path
    if not full.exists():
        return f"[File not found: {relative_path}]"
    return full.read_text(encoding="utf-8")


def _read_json(relative_path: str) -> dict | list:
    """Read and parse a JSON file relative to project root."""
    full = PROJECT_ROOT / relative_path
    if not full.exists():
        return {"error": f"File not found: {relative_path}"}
    return json.loads(full.read_text(encoding="utf-8"))


def _write_json(relative_path: str, data: dict | list) -> None:
    """Write JSON to a file relative to project root."""
    full = PROJECT_ROOT / relative_path
    full.write_text(
        json.dumps(data, indent=2, ensure_ascii=False),
        encoding="utf-8"
    )


# Seed data category → filename mapping
SEED_DATA_MAP = {
    "paye": "docs/seed-data/sars-paye-2025-2026.json",
    "uif": "docs/seed-data/sars-uif-sdl.json",
    "sdl": "docs/seed-data/sars-uif-sdl.json",
    "eti": "docs/seed-data/sars-eti.json",
    "travel": "docs/seed-data/sars-travel-rates.json",
    "taxref": "docs/seed-data/sars-tax-ref-format.json",
    "bcea": "docs/seed-data/bcea-working-time.json",
    "leave": "docs/seed-data/bcea-leave.json",
    "notice": "docs/seed-data/bcea-notice-severance.json",
    "payslip": "docs/seed-data/bcea-section33-payslip-fields.json",
    "holidays": "docs/seed-data/sa-public-holidays-2026.json",
    "validation": "docs/seed-data/sa-id-validation.json",
    "emp201": "docs/seed-data/sars-filing-formats/emp201-field-layout.json",
    "irp5": "docs/seed-data/sars-filing-formats/irp5-it3a-source-codes.json",
}

MOCKUP_MAP = {
    "login": "docs/design/mockups/01-login.html",
    "dashboard": "docs/design/mockups/02-dashboard.html",
    "employees": "docs/design/mockups/03-employees.html",
    "payroll": "docs/design/mockups/04-payroll.html",
    "leave": "docs/design/mockups/05-leave.html",
    "compliance": "docs/design/mockups/06-compliance.html",
    "timesheet": "docs/design/mockups/07-timesheet.html",
    "audit": "docs/design/mockups/08-audit.html",
    "role-management": "docs/design/mockups/09-role-management.html",
    "roles": "docs/design/mockups/09-role-management.html",
    "settings": "docs/design/mockups/10-settings.html",
    "admin": "docs/design/mockups/11-admin.html",
    "shared.css": "docs/design/mockups/shared.css",
}

SCHEMA_MAP = {
    "firestore": "docs/schemas/firestore-collections.md",
    "monetary": "docs/schemas/monetary-precision.md",
}

RBAC_DOC = "docs/prd/15_rbac_screen_access.md"

PRD_MAP = {
    0: "docs/prd/00_manifest.md",
    1: "docs/prd/01_executive_prd.md",
    2: "docs/prd/02_domain_model.md",
    3: "docs/prd/03_architecture.md",
    4: "docs/prd/04_api_contracts.md",
    5: "docs/prd/05_security_privacy.md",
    6: "docs/prd/06_compliance_sars_bcea.md",
    7: "docs/prd/07_caching_performance.md",
    8: "docs/prd/08_testing_quality.md",
    9: "docs/prd/09_observability_ops.md",
    10: "docs/prd/10_rollout_change_mgmt.md",
    11: "docs/prd/11_traceability_matrix.md",
    12: "docs/prd/12_risks_decisions.md",
    13: "docs/prd/13_glossary_and_data_dictionary.md",
    14: "docs/prd/14_gap_resolution.md",
    15: "docs/prd/15_rbac_screen_access.md",
}


# ---------------------------------------------------------------------------
# MCP Tools
# ---------------------------------------------------------------------------

@mcp.tool
def get_context() -> str:
    """
    Returns CLAUDE.md content and a concise current progress summary.
    ALWAYS call this first at the start of every session.
    """
    claude_md = _read("CLAUDE.md")
    progress = _read_json("docs/progress/progress-log.json")

    active = [t for t in progress.get("active_tasks", []) if t.get("status") != "completed"]
    pending = [t for t in active if t.get("status") == "pending"]
    in_progress = [t for t in active if t.get("status") == "in_progress"]
    blocked = [t for t in active if t.get("blockers")]

    summary = f"""
## Current Session Context

**Phase**: {progress.get("current_phase", "Unknown")}
**Last updated**: {progress.get("last_updated", "Unknown")}
**Last session**: {progress.get("last_session_summary", "")}

**In-progress tasks** ({len(in_progress)}):
{chr(10).join(f"  - {t['id']}: {t['title']}" for t in in_progress) or "  None"}

**Blocked tasks** ({len(blocked)}):
{chr(10).join(f"  - {t['id']}: {t['title']} — blockers: {t['blockers']}" for t in blocked) or "  None"}

**Next unblocked pending tasks**:
{chr(10).join(f"  - {t['id']} (P{t.get('priority',0)}): {t['title']}" for t in sorted(pending, key=lambda x: x.get("priority", 99))[:5]) or "  None"}
"""

    return claude_md + "\n\n---\n" + summary


@mcp.tool
def get_progress() -> str:
    """Returns the full progress-log.json content as formatted JSON."""
    return json.dumps(_read_json("docs/progress/progress-log.json"), indent=2)


@mcp.tool
def get_next_tasks() -> str:
    """
    Returns the top 3 unblocked pending tasks sorted by priority.
    Use this after completing a task to know what to work on next.
    """
    progress = _read_json("docs/progress/progress-log.json")
    completed_ids = {t["id"] for t in progress.get("completed_tasks", [])}
    completed_ids |= {
        t["id"]
        for t in progress.get("active_tasks", [])
        if t.get("status") == "completed"
    }

    candidates = [
        t for t in progress.get("active_tasks", [])
        if t.get("status") == "pending"
        and not t.get("blockers")
        and all(dep in completed_ids for dep in t.get("depends_on", []))
    ]

    top = sorted(candidates, key=lambda x: x.get("priority", 99))[:3]

    if not top:
        return "No unblocked pending tasks found. All tasks may be complete or blocked."

    lines = ["## Next Tasks (top 3, unblocked)\n"]
    for t in top:
        reqs = ", ".join(t.get("requirements", [])) or "—"
        lines.append(
            f"**{t['id']}** (Priority {t.get('priority', '?')}): {t['title']}\n"
            f"  Requirements: {reqs}\n"
            f"  Depends on: {', '.join(t.get('depends_on', [])) or 'none'}\n"
        )
    return "\n".join(lines)


@mcp.tool
def update_task_status(task_id: str, status: str, notes: str = "") -> str:
    """
    Updates a task's status in progress-log.json.
    Call this after completing any task — do not manually edit the JSON.

    Args:
        task_id: Task ID (e.g., "TASK-007")
        status: One of "pending", "in_progress", "completed", "blocked"
        notes: Optional notes about completion or blocking reason
    """
    if status not in ("pending", "in_progress", "completed", "blocked"):
        return f"Invalid status '{status}'. Use: pending, in_progress, completed, blocked"

    progress = _read_json("docs/progress/progress-log.json")
    progress["last_updated"] = datetime.now(timezone.utc).isoformat()

    found = False
    for task in progress.get("active_tasks", []):
        if task["id"] == task_id:
            task["status"] = status
            if notes:
                task["notes"] = notes
            if status == "completed":
                task["completed_at"] = datetime.now(timezone.utc).date().isoformat()
            found = True
            break

    if not found:
        return f"Task '{task_id}' not found in active_tasks."

    _write_json("docs/progress/progress-log.json", progress)
    return f"Task {task_id} updated to '{status}'. {notes}"


@mcp.tool
def log_decision(
    decision_id: str,
    title: str,
    decision: str,
    rationale: str,
    req_ref: str = "",
) -> str:
    """
    Appends an architectural decision to decisions.jsonl.
    Call this whenever a significant design or architectural choice is made.

    Args:
        decision_id: Unique ID (e.g., "DEC-010")
        title: Short title of the decision
        decision: What was decided
        rationale: Why this was decided
        req_ref: Related requirement or control ID (e.g., "REQ-SEC-001")
    """
    entry = {
        "id": decision_id,
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "title": title,
        "decision": decision,
        "rationale": rationale,
        "req_ref": req_ref,
    }
    decisions_file = PROJECT_ROOT / "docs/progress/decisions.jsonl"
    with decisions_file.open("a", encoding="utf-8") as f:
        f.write(json.dumps(entry, ensure_ascii=False) + "\n")
    return f"Decision {decision_id} logged: {title}"


@mcp.tool
def get_prd(number: int) -> str:
    """
    Returns the content of a PRD document.

    Args:
        number: PRD number 0–15 (0=manifest, 14=gap_resolution, 15=rbac_screen_access)
    """
    if number not in PRD_MAP:
        return f"Invalid PRD number {number}. Valid range: 0–15."
    return _read(PRD_MAP[number])


@mcp.tool
def get_schema(name: str) -> str:
    """
    Returns a schema document.

    Args:
        name: "firestore" (Firestore collections schema) or "monetary" (MoneyZAR precision rules)
    """
    key = name.lower()
    if key not in SCHEMA_MAP:
        return f"Unknown schema '{name}'. Valid options: firestore, monetary."
    return _read(SCHEMA_MAP[key])


@mcp.tool
def get_seed_data(category: str) -> str:
    """
    Returns statutory seed data as JSON string.

    Args:
        category: One of: paye, uif, sdl, eti, travel, taxref, bcea, leave,
                  notice, payslip, holidays, validation, emp201, irp5
    """
    key = category.lower()
    if key not in SEED_DATA_MAP:
        valid = ", ".join(sorted(SEED_DATA_MAP.keys()))
        return f"Unknown category '{category}'. Valid options: {valid}"
    return _read(SEED_DATA_MAP[key])


@mcp.tool
def get_mockup(screen: str) -> str:
    """
    Returns the HTML mockup for a specific screen. These are the canonical UI designs
    that Blazor components must faithfully implement.

    Args:
        screen: One of: login, dashboard, employees, payroll, leave, compliance,
                timesheet, audit, shared.css
    """
    key = screen.lower()
    if key not in MOCKUP_MAP:
        valid = ", ".join(sorted(MOCKUP_MAP.keys()))
        return f"Unknown screen '{screen}'. Valid options: {valid}"
    return _read(MOCKUP_MAP[key])


@mcp.tool
def get_design_tokens() -> str:
    """
    Returns the design tokens (colors, typography, spacing) and shared CSS.
    Use this before writing any Blazor UI component.
    """
    tokens = _read("docs/design/design-tokens.md")
    css = _read("docs/design/mockups/shared.css")
    return f"# Design Tokens\n\n{tokens}\n\n---\n\n# Shared CSS\n\n```css\n{css}\n```"


@mcp.tool
def validate_traceability(code_snippet: str) -> str:
    """
    Checks whether a code snippet contains traceability references (REQ-*, CTL-*, TC-*).
    Returns a warning if orphan code is detected.

    Args:
        code_snippet: Code or comment text to check
    """
    import re
    patterns = [
        r"REQ-[A-Z]+-\d+",
        r"CTL-[A-Z]+-\d+",
        r"TC-[A-Z]+-\d+",
    ]
    found = []
    for pat in patterns:
        found.extend(re.findall(pat, code_snippet))

    if found:
        return f"Traceability OK. Found references: {', '.join(set(found))}"
    else:
        return (
            "WARNING: No traceability references found (REQ-*, CTL-*, TC-*).\n"
            "This code may be orphan code. Add a comment like:\n"
            "  // REQ-HR-001: <brief description>\n"
            "  // CTL-SARS-001: <brief description>"
        )


@mcp.tool
def get_rbac() -> str:
    """
    Returns the complete RBAC specification: role definitions, screen access matrix,
    navigation per role, field-level access rules, and dynamic role management.

    Use this before implementing:
    - Any Blazor page component (to know which [Authorize(Roles)] to apply)
    - Any API endpoint with role restrictions
    - Any UI that renders conditionally based on role
    - Any Firestore query that must be scoped by department
    """
    return _read(RBAC_DOC)


if __name__ == "__main__":
    mcp.run()
