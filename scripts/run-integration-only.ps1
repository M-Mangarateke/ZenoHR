# Run integration tests only (emulator must already be running on localhost:8080).
# Called by run-integration-tests2.ps1 after emulator is confirmed ready.
$env:DOTNET_ROOT = "C:\Users\manga\.dotnet"
$env:PATH = "C:\Users\manga\.dotnet;" + $env:PATH
$env:FIRESTORE_EMULATOR_HOST = "localhost:8080"
$env:FIREBASE_AUTH_EMULATOR_HOST = "localhost:9099"

& dotnet test "C:\Users\manga\ZenoHR\tests\ZenoHR.Integration.Tests\ZenoHR.Integration.Tests.csproj" `
    --no-build `
    --logger "console;verbosity=normal"
exit $LASTEXITCODE
