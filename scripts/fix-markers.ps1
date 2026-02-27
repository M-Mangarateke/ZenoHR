$modules = @(
    'ZenoHR.Module.Employee',
    'ZenoHR.Module.TimeAttendance',
    'ZenoHR.Module.Leave',
    'ZenoHR.Module.Payroll',
    'ZenoHR.Module.Compliance',
    'ZenoHR.Module.Audit',
    'ZenoHR.Module.Risk'
)

foreach ($module in $modules) {
    $file = "C:\Users\manga\ZenoHR\src\$module\AssemblyMarker.cs"
    $content = Get-Content $file -Raw
    $updated = $content -replace 'internal sealed class AssemblyMarker', 'public sealed class AssemblyMarker'
    Set-Content $file $updated -Encoding utf8NoBOM
    Write-Host "Updated: $file"
}
Write-Host "Done."
