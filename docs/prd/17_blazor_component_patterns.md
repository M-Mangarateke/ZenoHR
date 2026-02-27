# PRD-17 — Blazor Server Component Architecture

> **Status**: Final · **Author**: ZenoHR Design · **Date**: 2026-02-23
> **Requirement refs**: REQ-OPS-001 to REQ-OPS-009, REQ-SEC-001 to REQ-SEC-010

---

## Purpose

Defines the canonical Blazor Server patterns for ZenoHR. Every developer must follow these patterns. Deviation requires explicit architectural review.

---

## 1. Component File Structure

```
ZenoHR.Web/
├── Pages/                              # @page route-level components
│   ├── Auth/
│   │   └── Login.razor                 # /login
│   ├── Dashboard.razor                 # /dashboard
│   ├── Employees/
│   │   ├── EmployeeList.razor          # /employees
│   │   ├── EmployeeDetail.razor        # /employees/{id}
│   │   ├── EmployeeForm.razor          # /employees/new, /employees/{id}/edit
│   │   └── MyProfile.razor             # /profile (self-view — own record + account settings)
│   ├── Payroll/
│   │   ├── PayrollRuns.razor           # /payroll
│   │   └── PayrollRunDetail.razor      # /payroll/{runId}
│   ├── Leave/
│   │   ├── LeaveCalendar.razor         # /leave
│   │   └── MyLeave.razor               # /leave/my-requests
│   ├── Compliance/
│   │   └── ComplianceScore.razor       # /compliance
│   ├── Timesheets/
│   │   └── Timesheets.razor            # /timesheets
│   ├── ClockIn/
│   │   └── ClockIn.razor               # /clock-in (employee self + manager team view)
│   ├── Audit/
│   │   └── AuditTrail.razor            # /audit
│   ├── Settings/
│   │   ├── RoleManagement.razor        # /settings/roles
│   │   ├── Departments.razor           # /settings/departments
│   │   ├── Users.razor                 # /settings/users
│   │   └── CompanySettings.razor       # /settings/company
│   ├── Analytics/
│   │   ├── CompanyAnalytics.razor      # /analytics
│   │   └── MyAnalytics.razor           # /my-analytics
│   └── Admin/
│       ├── AdminDashboard.razor        # /admin
│       ├── Tenants.razor               # /admin/tenants
│       ├── SystemHealth.razor          # /admin/health
│       ├── BackgroundJobs.razor        # /admin/jobs
│       ├── SeedData.razor              # /admin/seed
│       ├── FeatureFlags.razor          # /admin/flags
│       ├── PlatformAuditLog.razor      # /admin/audit-log
│       └── SecurityOps.razor           # /admin/security
│
├── Components/                         # Reusable (no @page)
│   ├── Layout/
│   │   ├── MainLayout.razor            # Shell — sidebar + header + <Body/>
│   │   ├── NavMenu.razor               # Role-conditional sidebar nav
│   │   ├── TopBar.razor                # Theme toggle, user avatar, notifications
│   │   └── BottomNav.razor             # ≤640px mobile bottom navigation
│   ├── Forms/
│   │   ├── EditFormSection.razor       # Collapsible form section with dirty tracking
│   │   ├── MoneyInput.razor            # MoneyZAR-aware decimal input (formats as R X,XXX.XX)
│   │   ├── SAIDInput.razor             # SA ID number with Luhn check + DOB extraction
│   │   └── PhoneInput.razor            # SA phone with +27 normalisation
│   ├── Charts/
│   │   └── ChartContainer.razor        # Chart.js wrapper via JS interop
│   ├── Payroll/
│   │   ├── PayslipViewer.razor         # Inline payslip preview
│   │   └── PayrollRunProgress.razor    # Real-time progress during payroll run
│   └── Shared/
│       ├── LoadingBoundary.razor       # Skeleton loading state wrapper
│       ├── StatusBadge.razor           # Coloured status chip
│       ├── AvatarImage.razor           # Profile photo with fallback initials
│       ├── ConfirmDialog.razor         # Reusable confirmation modal
│       └── EmptyState.razor            # Zero-data empty state with action CTA
│
└── Services/                           # Scoped Blazor services (DI)
    ├── TenantContextService.cs         # Tenant + user context (from JWT claims)
    ├── PayrollStateService.cs          # Payroll run coordination state
    ├── ClockInStateService.cs          # Real-time clock status (Firestore listener)
    └── ThemeService.cs                 # Light/dark/system theme preference
```

---

## 2. State Management

**Rule**: No global Redux/Flux store. State lives at the lowest scope that owns it.

### Tenant + User Context (read-only, session-scoped)
```csharp
// Set once at login, injected as CascadingValue throughout the component tree
// TenantContext is immutable after authentication

// In App.razor:
<CascadingValue Value="@_tenantContext">
    <Router AppAssembly="typeof(App).Assembly">
        ...
    </Router>
</CascadingValue>

// In any component:
[CascadingParameter] private TenantContext Tenant { get; set; } = null!;

public sealed record TenantContext(
    string TenantId,
    string UserId,
    string DisplayName,
    string SystemRole,            // "Director" | "HRManager" | "Manager" | "Employee" | "SaasAdmin"
    string[] DepartmentIds,       // Manager: managed departments; Employee: own dept
    string FirebaseIdToken        // refreshed by JS interop before API calls
);
```

### Cross-Component State (scoped DI services)
```csharp
// Services registered as Scoped (per SignalR circuit):
builder.Services.AddScoped<PayrollStateService>();
builder.Services.AddScoped<ClockInStateService>();
builder.Services.AddScoped<ThemeService>();

// Example: PayrollStateService holds state for payroll run orchestration
// Avoids prop-drilling across PayrollRuns.razor → PayrollRunDetail.razor
public sealed class PayrollStateService
{
    public PayrollRunStatus? ActiveRun { get; private set; }
    public event Action? OnChange;

    public void SetActiveRun(PayrollRunStatus run)
    {
        ActiveRun = run;
        OnChange?.Invoke();
    }
}
```

### Component-Local State
```csharp
// Simple UI state (filters, tab selection, form dirty flags) stays private to the component
@code {
    private string _searchTerm = string.Empty;
    private string _activeTab = "personal";
    private bool _isLoading = true;
    private bool _isDirty = false;
}
```

---

## 3. Route Authorization Pattern

Every page component declares its role requirement. The framework enforces this before rendering.

```csharp
// Every @page component must have [Authorize]:
[Authorize(Roles = "Director,HRManager")]
@page "/compliance"

// Mixed-role pages use the most permissive common role:
[Authorize(Roles = "Director,HRManager,Manager,Employee")]
@page "/clock-in"
// Role-specific rendering happens inside the component using AuthorizeView

// _Imports.razor adds global auth requirement (no unauthenticated pages):
@attribute [Authorize]

// App.razor redirect for unauthorized:
<AuthorizeRouteView RouteData="routeData" DefaultLayout="typeof(MainLayout)">
    <NotAuthorized>
        <RedirectToLogin />
    </NotAuthorized>
</AuthorizeRouteView>
```

### Role-Conditional Nav Items
```razor
<!-- NavMenu.razor — items NOT rendered for unauthorized roles (not hidden, not greyed) -->
<AuthorizeView Roles="Director,HRManager">
    <NavLink class="nav-item" href="/compliance">
        <i data-lucide="shield" class="nav-icon"></i>
        <span class="nav-label">Compliance</span>
    </NavLink>
</AuthorizeView>

<AuthorizeView Roles="Director,HRManager,Manager,Employee">
    <NavLink class="nav-item" href="/clock-in">
        <i data-lucide="timer" class="nav-icon"></i>
        <span class="nav-label">Clock In / Out</span>
    </NavLink>
</AuthorizeView>
```

### Role-Conditional Content within Pages
```razor
<!-- Same /clock-in route — renders different UI based on role -->
<AuthorizeView Roles="Employee">
    <SelfClockInPanel />        <!-- large Clock In button, own session summary -->
</AuthorizeView>

<AuthorizeView Roles="Manager">
    <TeamClockStatusPanel />    <!-- team attendance grid + flag buttons -->
</AuthorizeView>
```

---

## 4. Complex Form Pattern (Employee Form — 40+ Fields)

```razor
<!-- EmployeeForm.razor -->
@page "/employees/new"
@page "/employees/{EmployeeId}/edit"
[Authorize(Roles = "Director,HRManager")]

<EditForm Model="_employeeModel" OnValidSubmit="HandleSubmit">
    <FluentValidationValidator />

    <!-- Section tabs — each saves independently -->
    <div class="tab-nav">
        <button @onclick='() => _activeTab = "personal"'>Personal</button>
        <button @onclick='() => _activeTab = "employment"'>Employment</button>
        <button @onclick='() => _activeTab = "banking"'>Banking</button>
        <button @onclick='() => _activeTab = "benefits"'>Benefits</button>
        <button @onclick='() => _activeTab = "nextofkin"'>Next of Kin</button>
    </div>

    @if (_activeTab == "personal")
    {
        <EditFormSection Title="Personal Details"
                         IsDirty="@_sectionDirty["personal"]"
                         OnSave="() => SaveSection("personal")">
            <InputText @bind-Value="_employeeModel.LegalFirstName" />
            <SAIDInput @bind-Value="_employeeModel.NationalId" />
            <!-- ... -->
        </EditFormSection>
    }
</EditForm>
```

```csharp
@code {
    private readonly Dictionary<string, bool> _sectionDirty = new()
    {
        ["personal"] = false,
        ["employment"] = false,
        ["banking"] = false,
        ["benefits"] = false,
        ["nextofkin"] = false
    };

    // Navigation guard — warn on unsaved changes
    private IDisposable? _locationChangingHandler;

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            _locationChangingHandler = NavigationManager.RegisterLocationChangingHandler(
                async ctx =>
                {
                    if (_sectionDirty.Values.Any(d => d))
                    {
                        bool confirmed = await JSRuntime.InvokeAsync<bool>(
                            "confirm", "You have unsaved changes. Leave anyway?");
                        if (!confirmed) ctx.PreventNavigation();
                    }
                });
        }
    }

    private async Task SaveSection(string sectionName)
    {
        // API call for just that section — not one giant PUT
        await EmployeeApi.PatchSectionAsync(EmployeeId, sectionName, _employeeModel);
        _sectionDirty[sectionName] = false;
    }
}
```

---

## 5. Real-Time Patterns (Firestore Listeners)

### Clock-In Status (Manager team view)
```csharp
// ClockInStateService.cs — scoped per circuit
public sealed class ClockInStateService : IAsyncDisposable
{
    private IDisposable? _listener;
    public IReadOnlyList<ClockStatusRow> TeamStatus { get; private set; } = [];
    public event Action? OnStatusChanged;

    public async Task StartListeningAsync(string tenantId, string[] departmentIds)
    {
        var query = _db.Collection("clock_entries")
            .WhereEqualTo("tenant_id", tenantId)
            .WhereEqualTo("date", DateTime.UtcNow.Date.ToString("yyyy-MM-dd"))
            .WhereIn("department_id", departmentIds);

        _listener = query.Listen(snapshot =>
        {
            TeamStatus = MapToClockStatusRows(snapshot.Documents);
            OnStatusChanged?.Invoke();
        });
    }

    // Fallback: if listener silent for >30s, switch to 30s polling
    // Listener resumes automatically on circuit reconnect (Firestore SDK handles WebSocket)

    public async ValueTask DisposeAsync()
    {
        _listener?.Dispose();
    }
}
```

### Payroll Run Progress (no listener — Firestore doc write-once per state transition)
```csharp
// PayrollRunDetail.razor — polls Firestore on reconnect (no long-lived listener)
// PayrollRun status transitions: Draft → Processing → Finalizing → Filed
// On circuit reconnect: re-read PayrollRun document — UI reconstructs from persisted state
// No optimistic updates — always server round-trip before updating status display

protected override async Task OnInitializedAsync()
{
    await LoadPayrollRunAsync();  // read from Firestore
    // Only show "Processing" spinner if Firestore status == "Processing"
    // Background service continues regardless of circuit state
}
```

---

## 6. SignalR Circuit Failure Recovery

Blazor Server relies on a persistent SignalR circuit. ZenoHR's approach to circuit drops:

| Scenario | Recovery |
|----------|----------|
| Payroll run in progress | Background service continues; UI reads Firestore status on reconnect |
| Clock-in listener drops | Firestore SDK auto-reconnects WebSocket; listener resumes |
| Form data entry | Local component state reconstructed from Firestore on reconnect |
| Optimistic UI update | **Never used** for financial data — always server round-trip |

```csharp
// App.razor — custom reconnect UI
<Router>
    ...
</Router>
<div id="reconnect-modal" style="display: none;" class="reconnect-overlay">
    <div class="reconnect-card">
        <i data-lucide="wifi-off" style="width:24px;height:24px;color:var(--warning);"></i>
        <h3>Reconnecting…</h3>
        <p>Your session is being restored. Any in-progress changes are safe.</p>
    </div>
</div>
```

---

## 7. Loading States (Skeleton UI)

```razor
<!-- LoadingBoundary.razor — wraps any async-loading section -->
<LoadingBoundary IsLoading="@_isLoading">
    <Loading>
        <SkeletonTable Rows="8" Columns="6" />    <!-- matches real table shape -->
    </Loading>
    <Loaded>
        @ChildContent
    </Loaded>
</LoadingBoundary>

<!-- ErrorBoundary per page section — isolated errors don't crash the full page -->
<ErrorBoundary>
    <ChildContent>
        <ComplianceScoreCard />
    </ChildContent>
    <ErrorContent Context="ex">
        <div class="error-card">Failed to load compliance score. <button @onclick="StateHasChanged">Retry</button></div>
    </ErrorContent>
</ErrorBoundary>
```

**Rules**:
- **No full-page spinners** — skeleton UI only, matching the shape of real content
- **No loading text** — skeleton placeholder is sufficient UX feedback
- **Error isolation** — each independent card/section has its own `<ErrorBoundary>`
- **Retry button** on every error state — calls `StateHasChanged` to re-trigger `OnInitializedAsync`

---

## 8. MoneyZAR Input Component

```razor
<!-- MoneyInput.razor — ensures decimal input only, formats on blur -->
<div class="money-input-wrapper">
    <span class="money-prefix">R</span>
    <input type="text"
           class="money-input"
           value="@_displayValue"
           @onblur="HandleBlur"
           @oninput="HandleInput"
           placeholder="0.00"
           inputmode="decimal" />
</div>

@code {
    [Parameter] public MoneyZAR Value { get; set; }
    [Parameter] public EventCallback<MoneyZAR> ValueChanged { get; set; }

    private string _displayValue = string.Empty;

    private async Task HandleBlur()
    {
        // Parse, validate, round to 2dp, emit MoneyZAR
        if (decimal.TryParse(_displayValue.Replace(",", ""), out var amount))
        {
            var money = MoneyZAR.FromDecimal(amount);
            _displayValue = money.Display;   // "1 234.56" — formatted
            await ValueChanged.InvokeAsync(money);
        }
    }
}
```

---

## 9. Firebase Auth Integration

```csharp
// Program.cs — validate Firebase JWT on every API request
builder.Services.AddAuthentication("Firebase")
    .AddScheme<FirebaseAuthenticationOptions, FirebaseAuthenticationHandler>(
        "Firebase", options => { });

// FirebaseAuthenticationHandler validates the ID token and enriches claims:
// - tenant_id (from custom claims set at login)
// - system_role
// - dept_ids (array for Manager scope)

// Blazor Server: token refresh via JS interop before API calls
// Firebase SDK auto-refreshes token client-side; Blazor calls JS to get fresh token
```

---

## 10. Accessibility Requirements

| Requirement | Implementation |
|-------------|----------------|
| Keyboard navigation | All interactive elements reachable via Tab; modal traps focus |
| Screen reader | `aria-label` on icon-only buttons; `role="status"` on live regions |
| Colour contrast | All text ≥4.5:1 against background (WCAG AA) |
| Touch targets | ≥44px height for all buttons on mobile (≤640px breakpoint) |
| Form errors | `aria-invalid="true"` + `aria-describedby` on invalid fields |
| Loading states | `aria-busy="true"` on skeleton containers; `aria-live="polite"` on status updates |

---

## 11. Mobile Responsive Breakpoints

| Breakpoint | Behaviour |
|------------|-----------|
| `≤640px` | Sidebar hidden; bottom nav (5 items max); tables → card lists |
| `641–1024px` | Sidebar collapses to icon-only drawer; bottom nav hidden |
| `≥1025px` | Full sidebar always visible; bottom nav hidden |

Bottom nav item limit (≤640px): show 5 most-accessed items for the role.

- **Employee**: Dashboard · My Profile · My Leave · Clock In · My Analytics
- **Manager**: Dashboard · Employees · Leave · Clock In · My Analytics
- **Director/HRManager**: Dashboard · Employees · Payroll · Leave · Compliance

---

## 12. Performance Targets

| Metric | Target | Measurement |
|--------|--------|-------------|
| Time to Interactive (authenticated page) | ≤1.5s | Lighthouse (Azure North) |
| Payroll run (500 employees) | ≤15 min | End-to-end background service |
| Firestore read latency (p95) | ≤200ms | OpenTelemetry trace |
| SignalR reconnect time | ≤5s | Simulated circuit drop test |
| PDF payslip generation | ≤3s per payslip | QuestPDF benchmark |

---

## 13. Dependency Injection Registration (Summary)

```csharp
// Program.cs

// Scoped (per circuit):
builder.Services.AddScoped<TenantContextService>();
builder.Services.AddScoped<PayrollStateService>();
builder.Services.AddScoped<ClockInStateService>();
builder.Services.AddScoped<ThemeService>();

// Singleton (shared across circuits):
builder.Services.AddSingleton<IStatutoryRuleSetCache, FirestoreStatutoryRuleSetCache>();
// Cache invalidated on new StatutoryRuleSet version written to Firestore

// Transient (per-injection):
builder.Services.AddTransient<IPayrollCalculationEngine, PayrollCalculationEngine>();
builder.Services.AddTransient<IPayslipGenerator, QuestPdfPayslipGenerator>();
```
