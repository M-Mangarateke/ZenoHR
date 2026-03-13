---
doc_id: DEV-TROUBLESHOOTING
version: 1.0.0
updated_on: 2026-03-13
---

# Troubleshooting Guide

Common issues, their causes, and solutions for ZenoHR development.

---

## Build Issues

### Build fails with file lock errors

**Symptom**:
```
error MSB3021: Unable to copy file "obj\Debug\net10.0\ZenoHR.Module.Payroll.dll".
The process cannot access the file because it is being used by another process.
```

**Cause**: Zombie `VBCSCompiler.exe` or `dotnet.exe` processes holding file locks. Windows Defender real-time scanning can also cause intermittent locks.

**Fix**:
```powershell
# Kill zombie compiler and dotnet processes
Get-Process VBCSCompiler -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process dotnet -ErrorAction SilentlyContinue | Stop-Process -Force

# Clean and rebuild
dotnet clean
dotnet build
```

**Prevention**: Add the ZenoHR repository folder to Windows Defender exclusions:
1. Open Windows Security
2. Virus & threat protection > Manage settings > Exclusions
3. Add an exclusion > Folder > `C:\Users\<you>\ZenoHR`

---

### CA1822 analyzer warnings on test helper methods

**Symptom**:
```
warning CA1822: Member 'CreateTestEmployee' does not access instance data and can be marked as static
```

**Cause**: The CA1822 analyzer flags test helper methods that could be static. In test classes, this is often intentional (helpers may need to become instance methods later).

**Fix**: Either:

1. Make the method static (preferred if it truly has no instance state):
```csharp
private static Employee CreateTestEmployee() => ...
```

2. Or suppress the warning in the test project's `.csproj`:
```xml
<PropertyGroup>
    <NoWarn>$(NoWarn);CA1822</NoWarn>
</PropertyGroup>
```

---

### Package restore failures on .NET 10

**Symptom**:
```
error NU1102: Unable to find package SomePackage version [x.y.z]
```

**Cause**: Some NuGet packages may not yet have stable .NET 10 compatible versions, or the SDK version is outdated.

**Fix**:

1. Verify your SDK version:
```bash
dotnet --list-sdks
```

2. Update to the latest .NET 10 SDK from https://dotnet.microsoft.com/download

3. Clear the NuGet cache and restore:
```bash
dotnet nuget locals all --clear
dotnet restore
```

4. If a package genuinely lacks .NET 10 support, check for a pre-release version:
```bash
dotnet add package SomePackage --prerelease
```

---

### Solution builds but a specific project has red squiggles in IDE

**Symptom**: IDE (VS Code or Rider) shows errors, but `dotnet build` succeeds from the command line.

**Fix**: Restart the language server:

- **VS Code**: `Ctrl+Shift+P` > "Restart C# Language Server"
- **Rider**: File > Invalidate Caches and Restart

If persistent, delete `obj/` and `bin/` folders and rebuild:
```bash
dotnet clean
dotnet build
```

---

## Runtime Issues

### Firestore emulator connection issues

**Symptom**: Application starts but `/health/ready` returns unhealthy, or Firestore operations fail with connection errors.

**Cause**: Firestore emulator is not running or the application is not configured to use it.

**Fix**:

1. Start the emulator:
```bash
firebase emulators:start --only firestore
```

2. Verify the emulator is accessible:
```bash
curl http://localhost:8080
```

3. Check user secrets:
```bash
cd src/ZenoHR.Api
dotnet user-secrets list
```

Expected output should include:
```
Firestore:EmulatorHost = localhost:8080
Firestore:ProjectId = your-project-id
```

4. If using a non-default port, update the user secret:
```bash
dotnet user-secrets set "Firestore:EmulatorHost" "localhost:YOUR_PORT"
```

---

### Firebase auth token expired in dev

**Symptom**: API returns `401 Unauthorized` even with a valid Firebase account.

**Cause**: Firebase ID tokens expire after 1 hour. In development, you need to refresh the token.

**Fix**:

1. Use the Firebase Admin SDK or Firebase client SDK to get a fresh token:
```javascript
// In a Node.js script or browser console
const auth = firebase.auth();
const user = auth.currentUser;
const token = await user.getIdToken(true); // Force refresh
console.log(token);
```

2. For automated testing, use a long-lived custom token:
```bash
# Generate a custom token via Firebase Admin SDK
firebase auth:export --format=json
```

3. In integration tests, the test harness creates short-lived tokens automatically using the Firebase emulator.

---

### Application fails to start: "Firebase:ProjectId is required"

**Symptom**:
```
System.InvalidOperationException: Firebase:ProjectId is required.
Set it via .NET User Secrets: dotnet user-secrets set Firebase:ProjectId <your-project-id>
```

**Fix**: Set the Firebase project ID in user secrets:
```bash
cd src/ZenoHR.Api
dotnet user-secrets set "Firebase:ProjectId" "your-firebase-project-id"
```

---

### Port already in use

**Symptom**:
```
System.IO.IOException: Failed to bind to address https://127.0.0.1:5001
```

**Fix**: Find and kill the process using the port:

```powershell
# Find what's using port 5001
netstat -ano | findstr :5001

# Kill the process by PID
Stop-Process -Id <PID> -Force
```

Or use a different port:
```bash
dotnet run --project src/ZenoHR.Api --urls "https://localhost:5011;http://localhost:5010"
```

---

## How-To Guides

### How to add a new module / bounded context

1. **Create the project**:
```bash
dotnet new classlib -n ZenoHR.Module.NewModule -o src/ZenoHR.Module.NewModule
```

2. **Set the target framework** in the `.csproj`:
```xml
<PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
</PropertyGroup>
```

3. **Add reference to the shared kernel**:
```bash
dotnet add src/ZenoHR.Module.NewModule reference src/ZenoHR.Domain
```

4. **Add reference from API project**:
```bash
dotnet add src/ZenoHR.Api reference src/ZenoHR.Module.NewModule
```

5. **Create the folder structure**:
```
src/ZenoHR.Module.NewModule/
├── Aggregates/       # Aggregate roots
├── Entities/         # Child entities
├── Events/           # MediatR domain events
└── Enums/            # Module-specific enums
```

6. **Create the Firestore repository** in `src/ZenoHR.Infrastructure/Firestore/`.

7. **Create the test project**:
```bash
dotnet new xunit -n ZenoHR.Module.NewModule.Tests -o tests/ZenoHR.Module.NewModule.Tests
dotnet add tests/ZenoHR.Module.NewModule.Tests reference src/ZenoHR.Module.NewModule
dotnet add tests/ZenoHR.Module.NewModule.Tests package FluentAssertions
dotnet add tests/ZenoHR.Module.NewModule.Tests package NSubstitute
```

8. **Add architecture tests** to verify module boundaries in `tests/ZenoHR.Architecture.Tests/`.

9. **Add traceability comments** (`// REQ-*`) to every class.

---

### How to add a new API endpoint

1. **Create or update the endpoints file** in `src/ZenoHR.Api/Endpoints/`:

```csharp
// REQ-XX-YYY: Description of the endpoint's purpose
public static class NewModuleEndpoints
{
    public static IEndpointRouteBuilder MapNewModuleEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/new-module")
            .RequireAuthorization()
            .RequireRateLimiting("api")
            .WithTags("NewModule");

        group.MapGet("/", ListAsync)
            .WithName("ListNewModuleItems")
            .Produces<IReadOnlyList<ItemDto>>(200);

        return app;
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal user,
        NewModuleRepository repo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var items = await repo.ListByTenantAsync(tenantId, ct);
        return Results.Ok(items);
    }
}
```

2. **Register the endpoints** in `src/ZenoHR.Api/Program.cs`:
```csharp
app.MapNewModuleEndpoints();
```

3. **Apply rate limiting** — choose the appropriate policy:
   - `"api"` (maps to `general-api`) for standard endpoints
   - `"payroll"` (maps to `payroll-ops`) for computation-heavy endpoints

4. **Apply authorization** — choose the appropriate role restriction:
   - `.RequireAuthorization()` for any authenticated user (role checked in handler)
   - `.RequireAuthorization(p => p.RequireRole("Director", "HRManager"))` for restricted endpoints

5. **Add self-access checks** in the handler if employees should access their own data.

6. **Extract tenant_id from JWT**, never from request body:
```csharp
var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
```

7. **Add traceability comments** to the class and all handler methods.

---

### How to add a new background service

1. **Create the service** in `src/ZenoHR.Api/BackgroundServices/`:

```csharp
// REQ-XX-YYY: Description
public sealed partial class MyScheduledService : BackgroundService
{
    private static readonly TimeZoneInfo SastTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");

    private readonly ILogger<MyScheduledService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private DateOnly _lastRunDate = DateOnly.MinValue;

    public MyScheduledService(
        ILogger<MyScheduledService> logger,
        IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var nowSast = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow, SastTimeZone);
            var todaySast = DateOnly.FromDateTime(nowSast);

            // Run at target hour, once per day
            if (nowSast.Hour != 3 || _lastRunDate == todaySast)
                continue;

            _lastRunDate = todaySast;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                // Resolve scoped services from DI
                // Do work...
            }
            catch (Exception ex)
            {
                // Log but don't crash — service will retry next cycle
                _logger.LogError(ex, "MyScheduledService failed");
            }
        }
    }
}
```

2. **Register in `BackgroundServiceRegistration.cs`**:
```csharp
services.AddHostedService<MyScheduledService>();
```

3. **Key patterns**:
   - Use `PeriodicTimer` (not `Task.Delay` or `Timer`)
   - Always use SAST timezone for scheduling (`South Africa Standard Time`)
   - Guard against duplicate runs with `_lastRunDate`
   - Use `IServiceScopeFactory` to resolve scoped services (Firestore repos are scoped)
   - Never let exceptions crash the service — catch and log

---

### How to add a new Firestore collection

1. **Document the collection** in `docs/schemas/firestore-collections.md`.

2. **Create the repository** in `src/ZenoHR.Infrastructure/Firestore/`:
```csharp
// REQ-XX-YYY: Repository for new_collection
public sealed class NewItemRepository
{
    private readonly FirestoreDb _db;

    public NewItemRepository(FirestoreDb db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<Result<NewItem>> GetByIdAsync(
        string tenantId, string itemId, CancellationToken ct)
    {
        var docRef = _db.Collection("new_items").Document(itemId);
        var snapshot = await docRef.GetSnapshotAsync(ct);

        if (!snapshot.Exists)
            return Result<NewItem>.Failure(
                ZenoHrErrorCode.Unknown, $"Item {itemId} not found.");

        // Always verify tenant_id matches
        var storedTenantId = snapshot.GetValue<string>("tenant_id");
        if (storedTenantId != tenantId)
            return Result<NewItem>.Failure(
                ZenoHrErrorCode.Forbidden, "Access denied.");

        // Map snapshot to domain entity
        return Result<NewItem>.Success(MapFromSnapshot(snapshot));
    }
}
```

3. **Register in DI** (in `src/ZenoHR.Infrastructure/Extensions/`).

4. **Key rules**:
   - Every document must have a `tenant_id` field
   - Every query must filter by `tenant_id`
   - Monetary values stored as strings (`MoneyZAR.ToFirestoreString()`)
   - Return `Result<T>` from all repository methods

---

### How to update statutory data

Statutory data (tax rates, leave entitlements) is seeded from JSON files and stored in Firestore.

1. **Update the seed data** in `docs/seed-data/`:
   - `sars-paye-2025-2026.json` — PAYE brackets, rebates
   - `sars-uif-sdl.json` — UIF/SDL rates
   - `sars-eti.json` — ETI rules
   - `bcea-leave.json` — Leave entitlements
   - `bcea-working-time.json` — Working time limits

2. **Never hardcode** the values in C# code. Always load from `StatutoryRuleSet`.

3. **Test with the new data** — property-based tests should cover edge cases.

4. **The HR Manager can also update** provisional values via `PUT /api/settings/statutory/{id}/rule-data` when gazette values are published. All changes are audited.

---

## Getting Help

- **Project conventions**: Read `CLAUDE.md` at the repository root
- **Architecture decisions**: Check `docs/progress/decisions.jsonl`
- **PRD documents**: `docs/prd/` (00-18)
- **Security issues**: Check `docs/security/vulnerability-register.md`
- **POPIA controls**: Check `docs/security/popia-control-status.md`
