# Use system-installed .NET SDK (winget), fall back to user profile
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = "C:\Users\manga\.dotnet\dotnet.exe" }
if (-not (Test-Path $dotnet)) { $dotnet = "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" }
Set-Location "C:\Users\manga\ZenoHR"
$out = & $dotnet build ZenoHR.slnx --no-incremental 2>&1
$errors = $out | Where-Object { $_ -match ': error ' }
$summary = $out | Select-Object -Last 5
Write-Host "=== ERRORS ==="
$errors | ForEach-Object { Write-Host $_ }
Write-Host "=== SUMMARY ==="
$summary | ForEach-Object { Write-Host $_ }
