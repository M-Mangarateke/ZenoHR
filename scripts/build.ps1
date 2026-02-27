$dotnet = "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe"
Set-Location "C:\Users\manga\ZenoHR"
$out = & $dotnet build ZenoHR.slnx --no-incremental 2>&1
$out | Out-File "C:\Users\manga\ZenoHR\build_output.txt" -Encoding utf8
$out | Select-Object -Last 60
