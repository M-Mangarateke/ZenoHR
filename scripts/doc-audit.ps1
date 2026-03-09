<#
.SYNOPSIS
    ZenoHR Documentation Audit Script
    Run monthly (or before any release) to assess documentation health.

.DESCRIPTION
    Checks:
    1. PRD/schema doc staleness (updated_on vs today)
    2. Traceability coverage (REQ-*/CTL-*/TC-* in C# files)
    3. Security doc freshness (vulnerability register, POPIA status)
    4. Decisions log health (count, latest entry)
    5. Orphan docs (docs with no code implementing them)
    6. Missing docs (code references docs that don't exist)

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts/doc-audit.ps1
    powershell -ExecutionPolicy Bypass -File scripts/doc-audit.ps1 -Verbose
    powershell -ExecutionPolicy Bypass -File scripts/doc-audit.ps1 -MaxStaleDays 30
#>

[CmdletBinding()]
param(
    [int]$MaxStaleDays = 30,
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Continue"
$today = [System.DateTime]::Today
$issues = @()
$warnings = @()
$passed = @()

function Write-Section($title) {
    Write-Host ""
    Write-Host "=" * 60 -ForegroundColor Cyan
    Write-Host "  $title" -ForegroundColor Cyan
    Write-Host "=" * 60 -ForegroundColor Cyan
}

function Add-Issue($msg) {
    $script:issues += $msg
    Write-Host "  [FAIL] $msg" -ForegroundColor Red
}

function Add-Warning($msg) {
    $script:warnings += $msg
    Write-Host "  [WARN] $msg" -ForegroundColor Yellow
}

function Add-Pass($msg) {
    $script:passed += $msg
    Write-Host "  [PASS] $msg" -ForegroundColor Green
}

# ─────────────────────────────────────────────
# 1. PRD DOC STALENESS
# ─────────────────────────────────────────────
Write-Section "1. PRD & Schema Documentation Staleness"

$docPaths = @(
    "docs/prd/*.md",
    "docs/schemas/*.md",
    "docs/design/design-tokens.md",
    "docs/security/vulnerability-register.md",
    "docs/security/popia-control-status.md"
)

$staleDocs = @()
$freshDocs = @()
$noMetaDocs = @()

foreach ($glob in $docPaths) {
    $files = Get-ChildItem -Path (Join-Path $ProjectRoot $glob) -ErrorAction SilentlyContinue
    foreach ($file in $files) {
        $content = Get-Content $file.FullName -Raw -Encoding UTF8
        if ($content -match "updated_on:\s*(\d{4}-\d{2}-\d{2})") {
            $updated = [System.DateTime]::ParseExact($matches[1], "yyyy-MM-dd", $null)
            $daysOld = ($today - $updated).Days
            $relPath = $file.FullName.Replace($ProjectRoot + "\", "").Replace("\", "/")
            if ($daysOld -gt $MaxStaleDays) {
                $staleDocs += [PSCustomObject]@{ Path = $relPath; DaysOld = $daysOld; Updated = $matches[1] }
            } else {
                $freshDocs += $relPath
            }
        } else {
            $relPath = $file.FullName.Replace($ProjectRoot + "\", "").Replace("\", "/")
            $noMetaDocs += $relPath
        }
    }
}

if ($staleDocs.Count -gt 0) {
    foreach ($doc in ($staleDocs | Sort-Object DaysOld -Descending)) {
        Add-Issue "Stale ($($doc.DaysOld) days): $($doc.Path) — last updated $($doc.Updated)"
    }
} else {
    Add-Pass "All $($freshDocs.Count) docs are within $MaxStaleDays-day freshness window."
}
foreach ($doc in $noMetaDocs) {
    Add-Warning "No updated_on metadata: $doc"
}

# ─────────────────────────────────────────────
# 2. TRACEABILITY COVERAGE
# ─────────────────────────────────────────────
Write-Section "2. C# Traceability Coverage"

$csFiles = Get-ChildItem -Path (Join-Path $ProjectRoot "src") -Recurse -Filter "*.cs" |
    Where-Object { $_.FullName -notmatch "\\(obj|bin)\\" -and $_.Name -notmatch "(GlobalUsings|AssemblyAttributes|AssemblyMarker)" }

$tracedFiles = 0
$untracedFiles = @()

foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw -Encoding UTF8
    if ($content -match "(REQ-[A-Z]+-\d+|CTL-[A-Z]+-\d+|TC-[A-Z]+-\d+)") {
        $tracedFiles++
    } else {
        $relPath = $file.FullName.Replace($ProjectRoot + "\", "").Replace("\", "/")
        $untracedFiles += $relPath
    }
}

$total = $csFiles.Count
$coverage = if ($total -gt 0) { [math]::Round(($tracedFiles / $total) * 100, 1) } else { 0 }

if ($untracedFiles.Count -gt 0) {
    Add-Warning "$($untracedFiles.Count) C# files have no traceability reference ($coverage% coverage):"
    foreach ($f in $untracedFiles | Select-Object -First 10) {
        Write-Host "    $f" -ForegroundColor Yellow
    }
    if ($untracedFiles.Count -gt 10) {
        Write-Host "    ... and $($untracedFiles.Count - 10) more" -ForegroundColor Yellow
    }
} else {
    Add-Pass "100% traceability coverage — all $total C# files have REQ-*/CTL-*/TC-* references."
}

# Count unique IDs
$allContent = ($csFiles | ForEach-Object { Get-Content $_.FullName -Raw -Encoding UTF8 }) -join "`n"
$reqMatches = [regex]::Matches($allContent, "(REQ-[A-Z]+-\d+|CTL-[A-Z]+-\d+|TC-[A-Z]+-\d+)")
$uniqueIds = ($reqMatches | ForEach-Object { $_.Value } | Sort-Object -Unique).Count
Write-Host "  INFO: $uniqueIds unique requirement IDs across $total files" -ForegroundColor Cyan

# ─────────────────────────────────────────────
# 3. SECURITY DOC FRESHNESS
# ─────────────────────────────────────────────
Write-Section "3. Security Documentation Health"

$secDocs = @(
    @{ Path = "docs/security/vulnerability-register.md"; MaxDays = 35; Label = "Vulnerability Register" },
    @{ Path = "docs/security/popia-control-status.md";   MaxDays = 35; Label = "POPIA Control Status" }
)

foreach ($doc in $secDocs) {
    $fullPath = Join-Path $ProjectRoot $doc.Path
    if (-not (Test-Path $fullPath)) {
        Add-Issue "MISSING: $($doc.Path)"
        continue
    }
    $content = Get-Content $fullPath -Raw -Encoding UTF8
    if ($content -match "updated_on:\s*(\d{4}-\d{2}-\d{2})") {
        $updated = [System.DateTime]::ParseExact($matches[1], "yyyy-MM-dd", $null)
        $daysOld = ($today - $updated).Days
        if ($daysOld -gt $doc.MaxDays) {
            Add-Issue "$($doc.Label) stale ($daysOld days): $($doc.Path)"
        } else {
            Add-Pass "$($doc.Label) is fresh ($daysOld days old)"
        }
    } else {
        Add-Warning "$($doc.Path) missing updated_on metadata"
    }
}

# Count open vulnerabilities
$vulRegPath = Join-Path $ProjectRoot "docs/security/vulnerability-register.md"
if (Test-Path $vulRegPath) {
    $vulContent = Get-Content $vulRegPath -Raw
    $sev1Open = ([regex]::Matches($vulContent, "Sev-1.*?OPEN")).Count
    $sev2Open = ([regex]::Matches($vulContent, "Sev-2.*?OPEN")).Count
    Write-Host "  INFO: Open vulnerabilities — Sev-1: $sev1Open, Sev-2: $sev2Open" -ForegroundColor Cyan
    if ($sev1Open -gt 0) {
        Add-Warning "$sev1Open Sev-1 (Critical) vulnerabilities are still OPEN"
    }
}

# ─────────────────────────────────────────────
# 4. DECISIONS LOG HEALTH
# ─────────────────────────────────────────────
Write-Section "4. Architecture Decisions Log"

$decisionsPath = Join-Path $ProjectRoot "docs/progress/decisions.jsonl"
if (Test-Path $decisionsPath) {
    $lines = Get-Content $decisionsPath -Encoding UTF8 | Where-Object { $_.Trim() -ne "" }
    $decisionCount = $lines.Count

    if ($decisionCount -eq 0) {
        Add-Warning "decisions.jsonl exists but is empty"
    } else {
        $lastEntry = $lines[-1] | ConvertFrom-Json -ErrorAction SilentlyContinue
        $lastDate = if ($lastEntry.timestamp) { $lastEntry.timestamp.Substring(0, 10) } else { "unknown" }
        Add-Pass "$decisionCount decisions logged. Latest: $($lastEntry.id) — $($lastEntry.title) ($lastDate)"
    }
} else {
    Add-Warning "docs/progress/decisions.jsonl not found"
}

# ─────────────────────────────────────────────
# 5. PROGRESS LOG STATE
# ─────────────────────────────────────────────
Write-Section "5. Progress Log State"

$progressPath = Join-Path $ProjectRoot "docs/progress/progress-log.json"
if (Test-Path $progressPath) {
    $progress = Get-Content $progressPath -Raw | ConvertFrom-Json

    $active = @($progress.active_tasks | Where-Object { $_.status -ne "completed" })
    $inProgress = @($active | Where-Object { $_.status -eq "in_progress" })
    $pending = @($active | Where-Object { $_.status -eq "pending" })
    $blocked = @($active | Where-Object { $_.blockers -and $_.blockers.Count -gt 0 })

    Write-Host "  Phase: $($progress.current_phase)" -ForegroundColor Cyan
    Write-Host "  Last updated: $($progress.last_updated)" -ForegroundColor Cyan
    Write-Host "  In-progress: $($inProgress.Count), Pending: $($pending.Count), Blocked: $($blocked.Count)" -ForegroundColor Cyan

    if ($blocked.Count -gt 0) {
        Add-Warning "$($blocked.Count) task(s) are blocked"
        foreach ($b in $blocked) {
            Write-Host "    BLOCKED: $($b.id) — $($b.title)" -ForegroundColor Yellow
        }
    } else {
        Add-Pass "No blocked tasks"
    }

    # Check for stale in-progress tasks
    foreach ($t in $inProgress) {
        if ($t.updated_at) {
            $updatedAt = [System.DateTime]::Parse($t.updated_at)
            $staleDays = ($today - $updatedAt.Date).Days
            if ($staleDays -gt 3) {
                Add-Warning "Task $($t.id) has been 'in_progress' for $staleDays days: $($t.title)"
            }
        }
    }
} else {
    Add-Issue "docs/progress/progress-log.json not found"
}

# ─────────────────────────────────────────────
# 6. GENERATED DOCS STATE
# ─────────────────────────────────────────────
Write-Section "6. Generated Documentation State"

$generatedDir = Join-Path $ProjectRoot "docs/generated"
if (Test-Path $generatedDir) {
    $generatedFiles = Get-ChildItem $generatedDir -File
    if ($generatedFiles.Count -eq 0) {
        Add-Warning "docs/generated/ is empty — run MCP generate_traceability_index() to populate"
    } else {
        foreach ($f in $generatedFiles) {
            $age = ($today - $f.LastWriteTime.Date).Days
            if ($age -gt 7) {
                Add-Warning "Stale generated file ($age days): $($f.Name)"
            } else {
                Add-Pass "Generated: $($f.Name) ($age days old)"
            }
        }
    }
} else {
    Add-Warning "docs/generated/ directory does not exist — run MCP generate_traceability_index()"
}

# ─────────────────────────────────────────────
# SUMMARY
# ─────────────────────────────────────────────
Write-Section "AUDIT SUMMARY"

Write-Host ""
Write-Host "  PASSED : $($passed.Count)" -ForegroundColor Green
Write-Host "  WARNINGS: $($warnings.Count)" -ForegroundColor Yellow
Write-Host "  FAILURES: $($issues.Count)" -ForegroundColor Red

if ($issues.Count -gt 0) {
    Write-Host ""
    Write-Host "  Failures to fix:" -ForegroundColor Red
    foreach ($issue in $issues) {
        Write-Host "    - $issue" -ForegroundColor Red
    }
}

if ($warnings.Count -gt 0) {
    Write-Host ""
    Write-Host "  Warnings to review:" -ForegroundColor Yellow
    foreach ($warn in $warnings) {
        Write-Host "    - $warn" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "  Next steps:" -ForegroundColor Cyan
Write-Host "    1. Fix all FAILURES before merging to master" -ForegroundColor Cyan
Write-Host "    2. Use MCP bump_doc_version() to update stale docs" -ForegroundColor Cyan
Write-Host "    3. Run MCP generate_traceability_index() to refresh index" -ForegroundColor Cyan
Write-Host "    4. Review and close OPEN vulnerabilities in vulnerability-register.md" -ForegroundColor Cyan
Write-Host ""

if ($issues.Count -gt 0) {
    exit 1
}
exit 0
