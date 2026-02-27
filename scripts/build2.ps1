# .NET 10 SDK installed to user profile via dotnet-install.ps1
$dotnet = "C:\Users\manga\.dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" }
Set-Location "C:\Users\manga\ZenoHR"
$out = & $dotnet build ZenoHR.slnx --no-incremental 2>&1
$errors = $out | Where-Object { $_ -match ': error ' }
$summary = $out | Select-Object -Last 5
Write-Host "=== ERRORS ==="
$errors | ForEach-Object { Write-Host $_ }
Write-Host "=== SUMMARY ==="
$summary | ForEach-Object { Write-Host $_ }
