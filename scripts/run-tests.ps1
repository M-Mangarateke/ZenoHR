# .NET 10 SDK installed to user profile via dotnet-install.ps1
$dotnet = "C:\Users\manga\.dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" }
Set-Location "C:\Users\manga\ZenoHR"
& $dotnet test ZenoHR.slnx --no-build --logger "console;verbosity=normal" 2>&1
exit $LASTEXITCODE
