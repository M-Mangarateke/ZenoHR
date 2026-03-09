$path = "C:\Users\manga\ZenoHR\docs\progress\progress-log.json"
$json = Get-Content $path -Raw | ConvertFrom-Json

# Update top-level phase status
foreach ($p in $json.phases) {
    if ($p.id -eq "phase-2") { $p.status = "completed" }
    if ($p.id -eq "phase-3") { $p.status = "in_progress" }
    if ($p.id -eq "phase-4") { $p.status = "in_progress" }
}

# Phase 2 -- all completed (git: 1a1ebd7, c4ca99f, 537a341)
$phase2done = @("TASK-064","TASK-065","TASK-066","TASK-067","TASK-068","TASK-069","TASK-070","TASK-071","TASK-072","TASK-073")
foreach ($t in $json.phase2_tasks) {
    if ($phase2done -contains $t.id) {
        $t.status = "completed"
        $t | Add-Member -NotePropertyName "completed_at" -NotePropertyValue "2026-03-09" -Force
    }
}

# Phase 3 -- partial (git: d0a1609 + earlier)
$phase3done = @("TASK-081","TASK-082","TASK-083","TASK-084","TASK-085","TASK-086")
foreach ($t in $json.phase3_tasks) {
    if ($phase3done -contains $t.id) {
        $t.status = "completed"
        $t | Add-Member -NotePropertyName "completed_at" -NotePropertyValue "2026-03-09" -Force
    }
}

# Phase 4 -- scaffolded (git: 537a341)
$phase4done = @("TASK-101","TASK-102","TASK-103","TASK-104")
foreach ($t in $json.phase4_tasks) {
    if ($phase4done -contains $t.id) {
        $t.status = "completed"
        $t | Add-Member -NotePropertyName "completed_at" -NotePropertyValue "2026-03-09" -Force
        $t | Add-Member -NotePropertyName "notes" -NotePropertyValue "Scaffolded with auth guards and role annotations. Full implementation pending." -Force
    }
}

# Update meta
$json.current_phase = "Phase 4 - UI (Blazor Server)"
$json.last_updated = (Get-Date -Format "yyyy-MM-ddTHH:mm:ss.000000+00:00")
$json.last_session_summary = "Progress log synced to match git history. Phase 2 complete (TASK-064 thru TASK-073). Phase 3 partial: TASK-081-086 done (payroll repos, orchestration, API, statutory); TASK-087+ (QuestPDF, EMP201, filing) still pending. Phase 4 scaffolded: TASK-101-104 (NavMenu, auth, RBAC, Login). Next: full Blazor UI pages from mockups (TASK-105+) and QuestPDF payslip (TASK-087)."

$json | ConvertTo-Json -Depth 20 | Set-Content $path -Encoding UTF8
Write-Host "Progress log updated successfully."
