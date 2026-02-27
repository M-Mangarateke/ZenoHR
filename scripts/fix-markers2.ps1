$base = 'C:\Users\manga\ZenoHR\src'
$modules = @(
    'ZenoHR.Module.TimeAttendance',
    'ZenoHR.Module.Leave',
    'ZenoHR.Module.Payroll',
    'ZenoHR.Module.Compliance',
    'ZenoHR.Module.Audit',
    'ZenoHR.Module.Risk'
)
foreach ($m in $modules) {
    $f = "$base\$m\AssemblyMarker.cs"
    $c = [System.IO.File]::ReadAllText($f)
    $c2 = $c.Replace('internal sealed class AssemblyMarker;', 'public sealed class AssemblyMarker;')
    [System.IO.File]::WriteAllText($f, $c2, [System.Text.Encoding]::UTF8)
    Write-Host "Updated: $m"
}
Write-Host "All done."
