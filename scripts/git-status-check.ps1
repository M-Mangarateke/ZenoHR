Set-Location "C:\Users\manga\ZenoHR"
$status = git status --short 2>&1
$untracked = $status | Where-Object { $_ -match '^\?\?' }
$modified = $status | Where-Object { $_ -match '^.M' }
Write-Host "=== STAGED (A) ===" -ForegroundColor Green
$status | Where-Object { $_ -match '^A' } | Select-Object -Last 10
Write-Host ""
Write-Host "=== UNTRACKED (not staged) ===" -ForegroundColor Yellow
$untracked | ForEach-Object { Write-Host $_ }
Write-Host ""
Write-Host "=== MODIFIED UNSTAGED ===" -ForegroundColor Red
$modified | ForEach-Object { Write-Host $_ }
