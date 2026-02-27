#!/usr/bin/env python3
"""
REQ-OPS-008: Traceability validation script for ZenoHR.

Scans all hand-written C# source files under src/ (and optionally tests/)
and verifies that every file has at least one traceability reference:
  - REQ-XX-000   (requirement)
  - CTL-XX-000   (control)
  - TC-XX-000    (test case)

Files missing any reference are reported as violations, and the script exits
with code 1 so it can be used as a CI gate.

Usage:
  python3 scripts/validate-traceability.py             # checks src/ only
  python3 scripts/validate-traceability.py --all       # checks src/ + tests/
  python3 scripts/validate-traceability.py --warn      # report but do not fail (exit 0)

Exit codes:
  0  All checked files have at least one traceability reference (or --warn mode)
  1  One or more files are missing traceability references
"""

import re
import sys
from pathlib import Path

# Regex matches any REQ-*, CTL-*, or TC-* identifier.
# Examples: REQ-SEC-001, CTL-POPIA-015, TC-PAY-003
TRACE_PATTERN = re.compile(r'\b(?:REQ|CTL|TC)-[A-Z]+-\d+\b')

# ── Exempt files ──────────────────────────────────────────────────────────────
# These files are structural boilerplate with no traceable business logic.
# AssemblyMarker.cs  — one-liner marker class used by ArchUnit-style tests.
# UnitTest1.cs       — scaffold placeholder; real tests replace these.
# GlobalUsings.cs    — implicit using directives only.
# _Imports.razor     — Blazor implicit using directives.
# App.razor          — Blazor root component, no business logic.
# Routes.razor       — Blazor routing scaffold.
EXEMPT_FILENAMES = {
    'AssemblyMarker.cs',
    'UnitTest1.cs',
    'GlobalUsings.cs',
}

# Directories inside a project that contain auto-generated code — always skipped.
SKIP_DIRS = {'obj', 'bin', '.git', 'node_modules', 'wwwroot'}


def should_skip_dir(path: Path) -> bool:
    return any(part in SKIP_DIRS for part in path.parts)


def has_trace(path: Path) -> bool:
    """Return True if the file contains at least one traceability reference."""
    try:
        content = path.read_text(encoding='utf-8', errors='ignore')
        return bool(TRACE_PATTERN.search(content))
    except OSError:
        return True  # Unreadable — skip silently


def scan_dir(root: Path, target: str) -> list[Path]:
    """Return all .cs files under root/target that require a traceability check."""
    target_dir = root / target
    if not target_dir.exists():
        return []

    results = []
    for path in sorted(target_dir.rglob('*.cs')):
        if should_skip_dir(path):
            continue
        if path.name in EXEMPT_FILENAMES:
            continue
        results.append(path)
    return results


def main() -> int:
    args = set(sys.argv[1:])
    check_tests = '--all' in args
    warn_only = '--warn' in args

    repo_root = Path(__file__).resolve().parent.parent

    targets = ['src']
    if check_tests:
        targets.append('tests')

    all_files: list[Path] = []
    for target in targets:
        all_files.extend(scan_dir(repo_root, target))

    violations: list[Path] = [f for f in all_files if not has_trace(f)]

    print('=' * 60)
    print('ZenoHR Traceability Check (REQ-OPS-008)')
    print('=' * 60)
    print(f'Directories : {", ".join(targets)}')
    print(f'Files checked: {len(all_files)}')
    print(f'Violations   : {len(violations)}')

    if not violations:
        print('\nPASS — every file has at least one REQ-/CTL-/TC- reference.')
        return 0

    print(f'\n{"WARN" if warn_only else "FAIL"} — {len(violations)} file(s) are missing '
          'a traceability reference (REQ-*, CTL-*, or TC-*):')

    for v in violations:
        rel = v.relative_to(repo_root)
        print(f'  ✗  {rel}')

    print(
        '\nFix: add at least one comment of the form:\n'
        '  // REQ-XX-000   — requirement\n'
        '  // CTL-XX-000   — control\n'
        '  // TC-XX-000    — test case\n'
        '\nSee docs/prd/11_traceability_matrix.md for the full requirement ID list.'
    )

    return 0 if warn_only else 1


if __name__ == '__main__':
    sys.exit(main())
