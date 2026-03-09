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
    "analytics": "docs/design/mockups/12-analytics.html",
    "my-analytics": "docs/design/mockups/13-my-analytics.html",
    "clock-in": "docs/design/mockups/14-clock-in.html",
    "security-ops": "docs/design/mockups/15-security-ops.html",
    "payslip-template": "docs/design/mockups/16-payslip-template.html",
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
    16: "docs/prd/16_payroll_calculation_spec.md",
    17: "docs/prd/17_blazor_component_patterns.md",
    18: "docs/prd/18_navigation_flow.md",
}


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _all_tasks(progress: dict) -> list:
    """Collect tasks from active_tasks and all phase-specific task arrays."""
    tasks: list = list(progress.get("active_tasks", []))
    skip = {"active_tasks", "completed_tasks"}
    for key, value in progress.items():
        if key.endswith("_tasks") and key not in skip and isinstance(value, list):
            tasks.extend(value)
    return tasks


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

    all_tasks = _all_tasks(progress)
    active = [t for t in all_tasks if t.get("status") != "completed"]
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
    all_tasks = _all_tasks(progress)
    completed_ids = {t["id"] for t in progress.get("completed_tasks", [])}
    completed_ids |= {t["id"] for t in all_tasks if t.get("status") == "completed"}

    candidates = [
        t for t in all_tasks
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
    for task in _all_tasks(progress):
        if task["id"] == task_id:
            task["status"] = status
            if notes:
                task["notes"] = notes
            if status == "completed":
                task["completed_at"] = datetime.now(timezone.utc).date().isoformat()
            found = True
            break

    if not found:
        return f"Task '{task_id}' not found in any task array."

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


@mcp.tool
def get_vulnerability_register() -> str:
    """
    Returns the ZenoHR security vulnerability register.
    Use this when reviewing security gaps, planning security hardening work,
    or before implementing any security-sensitive feature.
    """
    return _read("docs/security/vulnerability-register.md")


@mcp.tool
def get_popia_status() -> str:
    """
    Returns the POPIA control implementation status tracker.
    Shows which of the 15 POPIA controls are implemented, partial, or not started.
    Use before implementing any compliance, data subject, or privacy feature.
    """
    return _read("docs/security/popia-control-status.md")


@mcp.tool
def get_doc_staleness() -> str:
    """
    Reports documentation staleness: reads updated_on from all PRD/schema frontmatter
    and compares against today. Flags docs older than 30 days as stale.
    Use at session start to identify which docs need updating after recent code changes.
    """
    import re
    from datetime import date

    today = date.today()
    report_lines = ["## Documentation Staleness Report\n", f"Generated: {today.isoformat()}\n"]

    doc_paths = list(PRD_MAP.values()) + list(SCHEMA_MAP.values()) + [
        "docs/design/design-tokens.md",
        "docs/security/vulnerability-register.md",
        "docs/security/popia-control-status.md",
    ]

    stale, fresh, missing = [], [], []

    for rel_path in doc_paths:
        full = PROJECT_ROOT / rel_path
        if not full.exists():
            missing.append(rel_path)
            continue

        content = full.read_text(encoding="utf-8")
        match = re.search(r"updated_on:\s*(\d{4}-\d{2}-\d{2})", content)
        if not match:
            missing.append(f"{rel_path} (no updated_on field)")
            continue

        updated = date.fromisoformat(match.group(1))
        days_old = (today - updated).days
        entry = f"  {rel_path} — {days_old} days old (updated {updated.isoformat()})"

        if days_old > 30:
            stale.append(entry)
        else:
            fresh.append(entry)

    if stale:
        report_lines.append(f"\n### STALE (>{30} days) — {len(stale)} docs\n")
        report_lines.extend(stale)

    if fresh:
        report_lines.append(f"\n### Fresh (≤30 days) — {len(fresh)} docs\n")
        report_lines.extend(fresh)

    if missing:
        report_lines.append(f"\n### Missing updated_on — {len(missing)} docs\n")
        report_lines.extend(f"  {m}" for m in missing)

    report_lines.append(
        f"\n### Summary\n"
        f"  Total docs checked: {len(doc_paths)}\n"
        f"  Stale: {len(stale)}  |  Fresh: {len(fresh)}  |  No metadata: {len(missing)}\n"
        f"\nTo update a doc version, call bump_doc_version(relative_path, summary)."
    )

    return "\n".join(report_lines)


@mcp.tool
def bump_doc_version(relative_path: str, change_summary: str, bump_type: str = "patch") -> str:
    """
    Bumps the version and updated_on in a document's YAML frontmatter and appends
    the change to a Version History table at the bottom of the file.

    Args:
        relative_path: Path relative to project root (e.g., "docs/prd/05_security_privacy.md")
        change_summary: One-line description of what changed (e.g., "Added CORS policy details")
        bump_type: "patch" (1.0.0 → 1.0.1), "minor" (1.0.0 → 1.1.0), or "major" (1.0.0 → 2.0.0)
    """
    import re
    from datetime import date

    if bump_type not in ("patch", "minor", "major"):
        return f"Invalid bump_type '{bump_type}'. Use: patch, minor, major."

    full = PROJECT_ROOT / relative_path
    if not full.exists():
        return f"[File not found: {relative_path}]"

    content = full.read_text(encoding="utf-8")
    today = date.today().isoformat()

    # Bump version in frontmatter
    version_match = re.search(r"(version:\s*)(\d+)\.(\d+)\.(\d+)", content)
    if version_match:
        major, minor, patch = int(version_match.group(2)), int(version_match.group(3)), int(version_match.group(4))
        if bump_type == "major":
            major, minor, patch = major + 1, 0, 0
        elif bump_type == "minor":
            minor, patch = minor + 1, 0
        else:
            patch += 1
        new_version = f"{major}.{minor}.{patch}"
        content = re.sub(r"(version:\s*)\d+\.\d+\.\d+", f"\\g<1>{new_version}", content)
    else:
        new_version = "1.0.1"
        content = content.replace("---\n", f"---\nversion: {new_version}\n", 1)

    # Update updated_on in frontmatter
    if "updated_on:" in content:
        content = re.sub(r"(updated_on:\s*)\d{4}-\d{2}-\d{2}", f"\\g<1>{today}", content)
    else:
        content = content.replace("---\n", f"---\nupdated_on: {today}\n", 1)

    # Append to Version History table
    version_history_entry = f"| {new_version} | {today} | Agent | {change_summary} |"
    if "## Version History" in content:
        # Find last row and append after it
        lines = content.split("\n")
        insert_idx = len(lines)
        for i, line in enumerate(lines):
            if "## Version History" in line:
                # Find end of table
                for j in range(i + 1, len(lines)):
                    if lines[j].startswith("| "):
                        insert_idx = j + 1
                    elif lines[j].strip() == "" and j > i + 2:
                        break
                break
        lines.insert(insert_idx, version_history_entry)
        content = "\n".join(lines)
    else:
        content += (
            f"\n\n## Version History\n\n"
            f"| Version | Date | Author | Changes |\n"
            f"|---------|------|--------|---------|\n"
            f"{version_history_entry}\n"
        )

    full.write_text(content, encoding="utf-8")
    return f"Bumped {relative_path} to v{new_version} ({today}). Change: {change_summary}"


@mcp.tool
def generate_traceability_index() -> str:
    """
    Scans all C# source files for REQ-*, CTL-*, and TC-* traceability comments.
    Returns a JSON index mapping each requirement ID to the files that implement it.
    Also writes the index to docs/generated/traceability-index.json.
    Use to answer: 'which files implement REQ-HR-003?' or 'is CTL-SARS-001 covered?'
    """
    import re

    pattern = re.compile(r"(REQ-[A-Z]+-\d+|CTL-[A-Z]+-\d+|TC-[A-Z]+-\d+)")
    src_root = PROJECT_ROOT / "src"
    test_root = PROJECT_ROOT / "tests"

    index: dict = {}
    total_refs = 0
    files_scanned = 0

    for search_root in [src_root, test_root]:
        if not search_root.exists():
            continue
        for cs_file in search_root.rglob("*.cs"):
            if "obj" in cs_file.parts or "bin" in cs_file.parts:
                continue
            files_scanned += 1
            try:
                text = cs_file.read_text(encoding="utf-8")
            except Exception:
                continue
            matches = pattern.findall(text)
            rel = str(cs_file.relative_to(PROJECT_ROOT)).replace("\\", "/")
            for m in matches:
                index.setdefault(m, [])
                if rel not in index[m]:
                    index[m].append(rel)
                    total_refs += 1

    # Write to docs/generated/
    generated_dir = PROJECT_ROOT / "docs" / "generated"
    generated_dir.mkdir(parents=True, exist_ok=True)
    out_path = generated_dir / "traceability-index.json"
    out_path.write_text(
        json.dumps({"generated_at": datetime.now(timezone.utc).isoformat(),
                    "files_scanned": files_scanned,
                    "total_references": total_refs,
                    "index": index},
                   indent=2, ensure_ascii=False),
        encoding="utf-8",
    )

    # Build summary
    lines = [
        f"## Traceability Index\n",
        f"Files scanned: {files_scanned}",
        f"Unique requirement IDs found: {len(index)}",
        f"Total references: {total_refs}",
        f"Written to: docs/generated/traceability-index.json\n",
        "### Top 20 Most Referenced IDs",
    ]
    sorted_ids = sorted(index.items(), key=lambda x: len(x[1]), reverse=True)[:20]
    for req_id, files in sorted_ids:
        lines.append(f"  {req_id}: {len(files)} file(s)")

    # Surface unimplemented requirements (in PRD but no code ref)
    prd_reqs: set = set()
    for prd_path in PRD_MAP.values():
        full = PROJECT_ROOT / prd_path
        if full.exists():
            prd_reqs.update(pattern.findall(full.read_text(encoding="utf-8")))
    orphan_reqs = prd_reqs - set(index.keys())
    if orphan_reqs:
        lines.append(f"\n### PRD Requirements With No Code Reference ({len(orphan_reqs)} orphans)")
        for r in sorted(orphan_reqs)[:30]:
            lines.append(f"  MISSING: {r}")
        if len(orphan_reqs) > 30:
            lines.append(f"  ... and {len(orphan_reqs) - 30} more")

    return "\n".join(lines)


if __name__ == "__main__":
    mcp.run()
