\ = @('AssemblyMarker.cs','UnitTest1.cs')
\  = @('C:\Users\manga\ZenoHR\src\','C:\Users\manga\ZenoHR\tests\')
\ = Get-ChildItem -Path \ -Recurse -Filter '*.cs' | Where-Object { \ -notcontains \extglob.Name } | Where-Object { \ = Get-Content \extglob.FullName -Raw -ErrorAction SilentlyContinue; \ -notmatch 'REQ-|CTL-|TC-' } | Select-Object -ExpandProperty FullName
if (\) { \ | ForEach-Object { Write-Output \extglob } } else { Write-Output 'All files contain traceability references.' }
