# Sets PATH to include .NET 10 SDK installed to user profile, then builds
$env:PATH = "C:\Users\manga\.dotnet;" + $env:PATH
$env:DOTNET_ROOT = "C:\Users\manga\.dotnet"
Set-Location "C:\Users\manga\ZenoHR"
Write-Host ".NET version: $(dotnet --version)"
dotnet restore ZenoHR.slnx
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build ZenoHR.slnx --no-restore
exit $LASTEXITCODE
