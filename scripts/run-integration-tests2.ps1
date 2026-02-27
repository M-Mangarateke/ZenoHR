# Runs integration tests with emulator started inline.
# REQ-OPS-003: Integration tests must always run against the emulator, not production.
# Usage: powershell -ExecutionPolicy Bypass -File scripts/run-integration-tests2.ps1

$env:DOTNET_ROOT = "C:\Users\manga\.dotnet"
$env:PATH = "C:\Users\manga\.dotnet;" + $env:PATH
$env:FIRESTORE_EMULATOR_HOST = "localhost:8080"
$env:FIREBASE_AUTH_EMULATOR_HOST = "localhost:9099"
$RepoRoot = "C:\Users\manga\ZenoHR"

Write-Host "Starting Firestore emulator..." -ForegroundColor Cyan
# firebase is a .cmd file on Windows — invoke via cmd.exe
$emulatorProcess = Start-Process -FilePath "cmd.exe" `
    -ArgumentList "/c", "firebase emulators:start --only firestore --project zenohr-a7ccf" `
    -WorkingDirectory $RepoRoot `
    -PassThru `
    -WindowStyle Hidden

# Wait for emulator to be ready
Write-Host "Waiting for emulator to start on port 8080..." -NoNewline
$ready = $false
for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Seconds 1
    Write-Host "." -NoNewline
    try {
        $null = Invoke-WebRequest -Uri "http://localhost:8080/" -TimeoutSec 1 -ErrorAction SilentlyContinue
        $ready = $true
        break
    } catch { }
}
Write-Host ""

if (-not $ready) {
    Write-Host "ERROR: Emulator did not start within 30 seconds." -ForegroundColor Red
    if ($emulatorProcess -and !$emulatorProcess.HasExited) {
        $emulatorProcess.Kill()
    }
    exit 1
}

Write-Host "Emulator ready. Running integration tests..." -ForegroundColor Green

# Run integration tests
& dotnet test "$RepoRoot\tests\ZenoHR.Integration.Tests\ZenoHR.Integration.Tests.csproj" `
    --no-build `
    --logger "console;verbosity=normal"
$testResult = $LASTEXITCODE

# Stop emulator
if ($emulatorProcess -and !$emulatorProcess.HasExited) {
    $emulatorProcess.Kill()
    Write-Host "Emulator stopped." -ForegroundColor Gray
}

Write-Host ""
if ($testResult -eq 0) {
    Write-Host "All integration tests passed." -ForegroundColor Green
} else {
    Write-Host "Integration tests FAILED (exit code: $testResult)." -ForegroundColor Red
}

exit $testResult
