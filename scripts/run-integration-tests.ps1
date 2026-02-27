# Runs integration tests inside the Firestore emulator.
# REQ-OPS-003: Integration tests must always run against the emulator, not production.
#
# Usage: powershell -ExecutionPolicy Bypass -File scripts/run-integration-tests.ps1
#        Must be run from the ZenoHR repo root OR any directory (script handles paths).

$RepoRoot = "C:\Users\manga\ZenoHR"
$env:DOTNET_ROOT = "C:\Users\manga\.dotnet"
$env:PATH = "C:\Users\manga\.dotnet;" + $env:PATH

Write-Host "Running integration tests against Firestore emulator..." -ForegroundColor Cyan
Write-Host "Repo root: $RepoRoot"

# Start emulator in background
$emulatorJob = Start-Job -ScriptBlock {
    param($root)
    Set-Location $root
    & firebase emulators:start --only firestore,auth --project zenohr-a7ccf 2>&1
} -ArgumentList $RepoRoot

# Wait for emulator to be ready (poll port 8080)
Write-Host "Waiting for emulator to start..." -NoNewline
$timeout = 30
$ready = $false
for ($i = 0; $i -lt $timeout; $i++) {
    Start-Sleep -Seconds 1
    Write-Host "." -NoNewline
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:8080/" -TimeoutSec 1 -ErrorAction SilentlyContinue
        $ready = $true
        break
    } catch { }
}
Write-Host ""

if (-not $ready) {
    Write-Host "Emulator did not start within $timeout seconds" -ForegroundColor Red
    Stop-Job $emulatorJob -ErrorAction SilentlyContinue
    Remove-Job $emulatorJob -ErrorAction SilentlyContinue
    exit 1
}

Write-Host "Emulator ready. Running integration tests..." -ForegroundColor Green

# Set emulator env vars so the test process picks them up
$env:FIRESTORE_EMULATOR_HOST = "localhost:8080"
$env:FIREBASE_AUTH_EMULATOR_HOST = "localhost:9099"

# Run integration tests
& dotnet test "$RepoRoot\tests\ZenoHR.Integration.Tests\ZenoHR.Integration.Tests.csproj" `
    --no-build `
    --logger "console;verbosity=normal"
$testResult = $LASTEXITCODE

# Stop emulator
Stop-Job $emulatorJob -ErrorAction SilentlyContinue
Remove-Job $emulatorJob -ErrorAction SilentlyContinue

exit $testResult
