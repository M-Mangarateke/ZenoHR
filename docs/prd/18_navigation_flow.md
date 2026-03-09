---
doc_id: PRD-18-NAV
version: 1.0.0
owner: Director / HRManager
updated_on: 2026-03-09
applies_to: [ZenoHR.Web, Blazor Server components, NavMenu, MainLayout]
depends_on: [PRD-15_rbac_screen_access, PRD-17_blazor_component_patterns]
requirements: [REQ-SEC-002, REQ-SEC-003, REQ-OPS-001]
---

# ZenoHR — Navigation Flow Specification (PRD-18)

> **Single source of truth for all navigation decisions.**
> Every Blazor page component, back button, mobile bottom-nav item, and sidebar section
> must be implemented exactly as specified here. No navigation decisions should be made
> outside this document.

---

## 1. Route Registry (All 26 Routes)

Every route in the application is listed below with its full navigation context.

| # | Route | Component File | Auth Roles | Nav Type | Parent | Back Action |
|---|-------|---------------|------------|----------|--------|-------------|
| R01 | `/login` | `Auth/Login.razor` | Public | Full-page | — | — (no back) |
| R02 | `/` | (redirect) | All | Redirect → `/dashboard` | — | — |
| R03 | `/dashboard` | `Dashboard/Dashboard.razor` | All auth | Full-page | — | No back (root) |
| R04 | `/employees` | `Employees/EmployeeList.razor` | Dir, HRM, Mgr, Emp | List + sticky detail panel | `/dashboard` | Sidebar only |
| R05 | `/employees/new` | `Employees/EmployeeForm.razor` | Dir, HRM | Full-page form | `/employees` | Back button → `/employees` + dirty-state guard |
| R06 | `/employees/{id}/edit` | `Employees/EmployeeForm.razor` | Dir, HRM | Full-page form | `/employees` | Back button → `/employees` + dirty-state guard |
| R07 | `/profile` | `Profile/MyProfile.razor` | All auth | Full-page | `/dashboard` | Sidebar only |
| R08 | `/payroll` | `Payroll/PayrollRuns.razor` | Dir, HRM, Mgr, Emp | Split panel (no route change on selection) | `/dashboard` | Sidebar only |
| R09 | `/payroll/my-payslips` | `Payroll/MyPayslips.razor` | Mgr, Emp | List view | `/payroll` | Back button → `/payroll` |
| R10 | `/leave` | `Leave/LeaveCalendar.razor` | Dir, HRM, Mgr, Emp | Calendar + queue | `/dashboard` | Sidebar only |
| R11 | `/leave/my-requests` | `Leave/MyLeave.razor` | All auth | List view | `/leave` | Back button → `/leave` |
| R12 | `/compliance` | `Compliance/ComplianceDashboard.razor` | Dir, HRM | Dashboard | `/dashboard` | Sidebar only |
| R13 | `/timesheets` | `Timesheets/Timesheets.razor` | Dir, HRM, Mgr | List + week picker | `/dashboard` | Sidebar only |
| R14 | `/audit` | `Audit/AuditTrail.razor` | Dir, HRM, SaasAdmin | Filtered table | `/dashboard` | Sidebar only |
| R15 | `/settings` | `Settings/SettingsPage.razor` | Dir, HRM | Tab-based page | `/dashboard` | Sidebar only |
| R16 | `/settings/roles` | (redirect to `/settings?tab=roles`) | Dir, HRM | Tab redirect | `/settings` | Sidebar only |
| R17 | `/analytics` | `Analytics/CompanyAnalytics.razor` | Dir, HRM, Mgr | Dashboard | `/dashboard` | Sidebar only |
| R18 | `/my-analytics` | `Analytics/MyAnalytics.razor` | All auth | Personal dashboard | `/dashboard` | Sidebar only |
| R19 | `/clock-in` | `ClockIn/ClockIn.razor` | All auth | Role-conditional UI | `/dashboard` | Sidebar only |
| R20 | `/admin` | `Admin/AdminDashboard.razor` | SaasAdmin | Platform overview | — | No back (SaasAdmin root) |
| R21 | `/admin/tenants` | `Admin/Tenants.razor` | SaasAdmin | List + sticky panel | `/admin` | Breadcrumb → `/admin` |
| R22 | `/admin/health` | `Admin/SystemHealth.razor` | SaasAdmin | Health dashboard | `/admin` | Breadcrumb → `/admin` |
| R23 | `/admin/seed` | `Admin/SeedData.razor` | SaasAdmin | Versioned list | `/admin` | Breadcrumb → `/admin` |
| R24 | `/admin/flags` | `Admin/FeatureFlags.razor` | SaasAdmin | Toggle panel | `/admin` | Breadcrumb → `/admin` |
| R25 | `/admin/audit-log` | `Admin/PlatformAuditLog.razor` | SaasAdmin | Filtered table | `/admin` | Breadcrumb → `/admin` |
| R26 | `/admin/security` | `Admin/SecurityOps.razor` | SaasAdmin | Security dashboard | `/admin` | Breadcrumb → `/admin` |

**Unauthorized access**: Any route accessed without the required role → redirect to `/login` (not a 403 page).

---

## 2. Navigation Patterns (4 Canonical Types)

### Pattern A — Full-Page Form with Dirty-State Guard

**Used by**: R05 `/employees/new`, R06 `/employees/{id}/edit`

```
[List page: /employees]
        │
        │  "Add Employee" button or row kebab → "Edit"
        ▼
[Full-page form: /employees/new or /employees/{id}/edit]
        │
        ├── User clicks "Back" button (explicit button in page header)
        │       └── If form is dirty → show UnsavedChangesModal
        │               ├── "Leave anyway" → NavigateTo("/employees")
        │               └── "Stay" → dismiss modal, keep editing
        │
        ├── User clicks sidebar link
        │       └── Same dirty-state check via RegisterLocationChangingHandler
        │
        ├── User presses browser Back button
        │       └── Same dirty-state check via RegisterLocationChangingHandler
        │
        └── User clicks "Save" successfully
                └── NavigateTo("/employees") automatically — no confirmation needed
```

**Dirty-state tracking** (per PRD-17 §4):
```csharp
// In EmployeeForm.razor @code:
private readonly Dictionary<string, bool> _dirtyTabs = new();

protected override void OnInitialized()
{
    _locationChangingHandler = NavigationManager.RegisterLocationChangingHandler(
        async context =>
        {
            if (_dirtyTabs.Values.Any(d => d))
            {
                context.PreventNavigation();
                await ShowUnsavedChangesModal(context.TargetLocation);
            }
        });
}
```

---

### Pattern B — Sticky Panel (No Route Change)

**Used by**: Employee detail on `/employees` (R04), Run selection on `/payroll` (R08), Role detail on `/settings` roles tab (R15)

```
[List component — left panel or full width]
        │
        │  User clicks row
        ▼
[Detail panel — right panel (desktop) or bottom sheet (mobile)]
        │
        ├── DESKTOP: right panel slides in alongside list
        │       ├── No URL change
        │       ├── No browser history entry
        │       ├── Clicking another row updates panel in-place
        │       └── Clicking outside or pressing Esc closes panel → panel hidden, list shows full width
        │
        └── MOBILE (≤640px): bottom sheet slides up
                ├── No URL change
                ├── Close (X) button in sheet header closes it
                ├── Swipe down to close
                └── Browser Back button: closes the bottom sheet (intercepted — NOT a route navigation)
```

**State management**:
```csharp
// In EmployeeList.razor @code:
private string? _selectedEmployeeId;

private void OnRowClick(string employeeId)
{
    _selectedEmployeeId = employeeId == _selectedEmployeeId ? null : employeeId;
}
```

---

### Pattern C — Tab-Based Single Route

**Used by**: `/settings` (R15) — 4 tabs: Users, Departments, Roles, Company

```
[/settings — tab header sticky at top]
        │
        ├── Tab: Users     ──┐
        ├── Tab: Departments  ├── Tabs switch content in-place
        ├── Tab: Roles       ──┤  NO URL change, NO history entry
        └── Tab: Company   ──┘  Instant re-render (no async)

        Back button/sidebar click → navigate to /dashboard
        (tabs are NOT history entries — back never goes "back one tab")
```

**Tab state**:
```csharp
// In SettingsPage.razor @code:
private string _activeTab = "users"; // default tab

private void SetTab(string tab) => _activeTab = tab;

// /settings/roles redirects here with tab preset:
[Parameter][SupplyParameterFromQuery] public string? Tab { get; set; }

protected override void OnParametersSet()
{
    if (Tab is not null) _activeTab = Tab;
}
```

**Dirty-state per tab**: If a tab has unsaved changes (e.g., Company settings form edited), apply Pattern A dirty-state guard when switching to a different tab.

---

### Pattern D — In-Page Action (No Navigation)

**Used by**: Leave approval buttons, Timesheet approval, Audit event detail, Payslip preview

```
[Page with action buttons or clickable rows]
        │
        ├── Approve/Reject button → POST to API → in-place update (no navigation)
        │
        └── "View detail" or row click → modal overlay
                ├── Modal appears over current page (backdrop)
                ├── Close (X) button or Esc → closes modal
                ├── No URL change
                ├── No browser history entry
                └── State preserved when modal closes
```

---

## 3. Back Button Rules (Definitive)

| Situation | Back button shown? | Where it goes | Dirty-state guard? |
|-----------|-------------------|---------------|-------------------|
| On `/dashboard`, `/employees`, `/payroll`, `/leave`, `/compliance`, `/timesheets`, `/audit`, `/analytics`, `/my-analytics`, `/clock-in`, `/profile` | **No** (sidebar is always visible) | N/A | N/A |
| On `/settings` | **No** | N/A | N/A |
| On `/employees/new` | **Yes** — in page header | `/employees` | **Yes** |
| On `/employees/{id}/edit` | **Yes** — in page header | `/employees` | **Yes** |
| On `/payroll/my-payslips` | **Yes** — in page header | `/payroll` | No |
| On `/leave/my-requests` | **Yes** — in page header | `/leave` | No |
| On any `/admin/*` subpage | **Yes** — as breadcrumb | `/admin` | No |
| Employee sticky panel (desktop) | **No** — panel is dismissed by clicking outside or Esc | N/A | N/A |
| Employee bottom sheet (mobile) | **Yes** — Close (X) in sheet header | Closes sheet, returns to list | No |
| Settings tabs | **No** — tabs are not history entries | N/A | N/A |
| Modal overlays (audit detail, payslip viewer, leave detail) | **Yes** — Close (X) in modal header or Esc | Closes modal | No |

**Browser Back button interception rules**:
- On full-page forms (R05, R06): intercepted by `RegisterLocationChangingHandler` → dirty-state check
- On mobile bottom sheets (Pattern B): back closes the sheet (`JSInterop` back-button trap)
- On all other routes: browser back navigates normally (no interception)

---

## 4. Sidebar Navigation (Desktop)

### Structure — Director / HRManager view (full sidebar)

```
ZenoHR Logo + wordmark
────────────────────────
MAIN
  📊 Dashboard          → /dashboard
  👥 Employees          → /employees
  ⏱  Timesheets         → /timesheets
  🌴 Leave              → /leave          [badge: pending count]

FINANCE & COMPLIANCE
  💰 Payroll            → /payroll
  ✅ Compliance         → /compliance     [badge: issue count]
  🔗 Audit Trail        → /audit

SETTINGS
  🔑 Role Management    → /settings?tab=roles
  🏢 Departments        → /settings?tab=departments
  👤 Users              → /settings?tab=users
  ⚙  Company Settings   → /settings?tab=company

INSIGHTS
  📈 Analytics          → /analytics
  📉 My Analytics       → /my-analytics

ACCOUNT
  🙍 My Profile         → /profile

────────────────────────
[Avatar] [Name]          [Logout]
[Theme toggle]
```

### Structure — Manager view (restricted)

```
MAIN
  📊 Dashboard          → /dashboard
  👥 Employees          → /employees      (team scope only)
  ⏱  Timesheets         → /timesheets     (team scope only, approve)
  🌴 Leave              → /leave          (team scope, approve)

MY SELF-SERVICE
  💰 My Payslips        → /payroll/my-payslips
  🕐 Clock In/Out       → /clock-in

INSIGHTS
  📉 My Analytics       → /my-analytics

ACCOUNT
  🙍 My Profile         → /profile
```

Settings, Compliance, Audit → **absent entirely** (not greyed out, not disabled — not rendered).

### Structure — Employee view (minimal)

```
MAIN
  📊 Dashboard          → /dashboard
  🌴 My Leave           → /leave/my-requests

MY SELF-SERVICE
  💰 My Payslips        → /payroll/my-payslips
  🕐 Clock In/Out       → /clock-in

INSIGHTS
  📉 My Analytics       → /my-analytics

ACCOUNT
  🙍 My Profile         → /profile
```

### Structure — SaasAdmin view (completely different layout)

```
ZenoHR Admin Console
────────────────────────
PLATFORM
  📊 Dashboard          → /admin
  🏢 Tenants            → /admin/tenants
  💊 System Health      → /admin/health
  📦 Seed Data          → /admin/seed
  🚩 Feature Flags      → /admin/flags

SECURITY
  📋 Platform Audit Log → /admin/audit-log
  🛡  Security Ops       → /admin/security
```

Tenant screens (`/employees`, `/payroll`, etc.) → **absent entirely**.

---

## 5. Mobile Bottom Navigation (≤640px)

The sidebar is hidden at ≤640px. A bottom navigation bar with 5 items replaces it.

| Role | Item 1 | Item 2 | Item 3 | Item 4 | Item 5 |
|------|--------|--------|--------|--------|--------|
| **Employee** | Dashboard `/` | Clock In `/clock-in` | My Leave `/leave/my-requests` | My Payslips `/payroll/my-payslips` | My Analytics `/my-analytics` |
| **Manager** | Dashboard `/` | Employees `/employees` | Leave `/leave` | Clock In `/clock-in` | My Analytics `/my-analytics` |
| **Director / HRManager** | Dashboard `/` | Employees `/employees` | Payroll `/payroll` | Leave `/leave` | Compliance `/compliance` |
| **SaasAdmin** | Dashboard `/admin` | Tenants `/admin/tenants` | Health `/admin/health` | Seed `/admin/seed` | Security `/admin/security` |

**Active state**: Bottom nav item with current route gets `brand-primary` background and primary text color.

**Overflow nav** (items not in bottom bar): Accessible via a hamburger icon that opens a modal drawer showing the full sidebar.

---

## 6. Breadcrumbs

Breadcrumbs are shown **only** on Admin subpages (`/admin/*`).

| Current route | Breadcrumb |
|--------------|-----------|
| `/admin/tenants` | `Admin > Tenants` |
| `/admin/health` | `Admin > System Health` |
| `/admin/seed` | `Admin > Seed Data` |
| `/admin/flags` | `Admin > Feature Flags` |
| `/admin/audit-log` | `Admin > Platform Audit Log` |
| `/admin/security` | `Admin > Security Operations` |

Format: plain text links. `Admin` is a clickable link → `/admin`. Current page is non-clickable.

Breadcrumbs are **not** shown on the main tenant UI — the active sidebar item provides sufficient context.

**Exception**: Full-page forms (`/employees/new`, `/employees/{id}/edit`) show a back arrow + parent page name in the page header — not a breadcrumb bar.

---

## 7. Authorization Guards

Every Blazor page component must declare:

```csharp
// Director/HRManager-only page
[Authorize(Roles = "Director,HRManager")]
@page "/compliance"

// Multi-role page
[Authorize(Roles = "Director,HRManager,Manager,Employee")]
@page "/employees"

// Public page (login)
@page "/login"
// (no [Authorize] attribute)
```

`Routes.razor` must wrap the `<Router>` in `<CascadingAuthenticationState>` and use `<AuthorizeRouteView>` with a `<NotAuthorized>` redirect to `/login`.

Role-conditional content within a page:

```razor
<AuthorizeView Roles="Director,HRManager">
    <PayrollSummaryWidget />
</AuthorizeView>

<AuthorizeView Roles="Manager">
    <TeamHeadcountWidget DepartmentIds="@_userDepartmentIds" />
</AuthorizeView>
```

**Rule**: Never show a greyed-out disabled nav item. Either the element exists or it does not exist in the DOM.

---

## 8. State Preserved During Navigation

| Scenario | State preserved? | How |
|----------|-----------------|-----|
| Navigate away from `/employees` and return | No — list reloads | Use `@key` for list refresh |
| Navigate between payroll runs on `/payroll` | Yes — `_selectedRunId` persists | Scoped `PayrollStateService` |
| Switch Settings tabs | Yes — `_activeTab` in local state | Component-local field |
| Return from `/employees/{id}/edit` to `/employees` | No — list reloads | Intentional: ensures fresh data after edit |
| Circuit reconnect on `/clock-in` | Yes — reconnects Firestore listener | `ClockInStateService.StartListeningAsync()` |
| Navigate away from form with unsaved changes | Blocked — dirty-state guard fires | `RegisterLocationChangingHandler` |

---

## 9. Page-Level Subpage Trees (Drill-Down Map)

### Employees (/employees)

```
/employees (list + sticky detail panel)
├── Click row → sticky panel opens (NO route change)
│   ├── View profile (read-only)
│   ├── "Edit" button → /employees/{id}/edit (ROUTE CHANGE)
│   └── "View Payslips" link → /payroll/my-payslips?employeeId={id} (Director/HRM only)
├── "Add Employee" button → /employees/new (ROUTE CHANGE)
└── No other subpages
```

### Payroll (/payroll)

```
/payroll (split panel — left: run list, right: run detail)
├── Click run in left panel → right panel updates (NO route change)
│   ├── View run statistics (gross, net, tax)
│   ├── View employee results table
│   │   └── Click employee row → inline payslip preview (modal, Pattern D)
│   ├── Download payslip PDF link (opens in new tab / download)
│   ├── Export CSV button (download, no navigation)
│   └── Approve/Reject/Finalize actions (Pattern D — in-place)
└── No subpages with route changes
```

### Leave (/leave)

```
/leave (calendar view + approval queue)
├── Calendar cell click → highlights that date's requests (NO route change)
├── Request row in queue → approval detail modal (Pattern D)
│   ├── Approve button → POST → in-place update
│   └── Reject button → modal with reason field → POST
└── No subpages with route changes

/leave/my-requests (employee self-service)
├── "Submit Leave Request" button → modal form (Pattern D)
└── Cancel own pending request → in-place (Pattern D)
```

### Settings (/settings)

```
/settings (tab-based page)
├── Tab: Users
│   ├── Invite user → modal form (Pattern D)
│   ├── Click user row → row expands or edit modal (Pattern D)
│   └── Revoke/Restore role → inline action (Pattern D)
├── Tab: Departments
│   ├── Add department → modal form (Pattern D)
│   └── Edit department → inline row edit (Pattern D)
├── Tab: Roles (same content as /settings/roles)
│   ├── Left panel: role list
│   └── Right panel: role detail / create form (Pattern B — no route change)
└── Tab: Company Settings
    └── Edit form → save in-place (Pattern D with dirty-state guard within the tab)
```

### Admin (/admin/*)

```
/admin (platform dashboard)
├── /admin/tenants (list + sticky right panel)
│   ├── Click tenant → sticky panel (Pattern B)
│   │   ├── View config, user count, seed version
│   │   └── Actions: Reset, Archive, Resync (Pattern D)
│   └── "Create Tenant" button → right panel switches to create form (Pattern B)
├── /admin/health (display only — no drill-down)
├── /admin/seed
│   ├── View version history table
│   ├── Upload new JSON → modal (Pattern D)
│   └── Rollback → confirmation modal (Pattern D)
├── /admin/flags (toggle switches — all Pattern D)
├── /admin/audit-log (filter + paginate — Pattern D for detail modal)
└── /admin/security (display + incident management — Pattern D)
```

---

## 10. Unsaved Changes Modal

Standard modal used by Pattern A and within Pattern C (dirty tabs):

```
┌─────────────────────────────────────┐
│  Unsaved Changes                    │
│                                     │
│  You have unsaved changes. If you   │
│  leave, your changes will be lost.  │
│                                     │
│  [Stay and Continue Editing]        │
│  [Leave Without Saving]             │
└─────────────────────────────────────┘
```

- "Stay" → dismiss modal, `context.PreventNavigation()` already fired
- "Leave" → call `NavigationManager.NavigateTo(targetLocation)` forcefully

---

## 11. Blazor Implementation Checklist

For every new page component, verify all of the following before marking it done:

- [ ] `@page` directive with correct route
- [ ] `[Authorize(Roles = "...")]` attribute with correct roles per this spec
- [ ] `// REQ-SEC-002` or `// REQ-SEC-003` traceability comment at top of file
- [ ] Nav type correctly implemented (full-page, sticky panel, tab, modal)
- [ ] Back button: shown only if specified in route table above; navigates to correct parent
- [ ] Dirty-state guard: applied if pattern A; not applied if pattern B/C/D
- [ ] Mobile behaviour: sticky panel becomes bottom sheet; bottom nav shows correct items
- [ ] Sidebar active state: current route highlights correct sidebar item
- [ ] Breadcrumbs: shown only on `/admin/*` subpages
- [ ] Unauthorized access redirects to `/login` (not 403 page)

---

## Version History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-09 | Claude Agent (Navigation Analysis) | Initial spec — 26 routes, 4 nav patterns, back-button rules, mobile nav, breadcrumbs, subpage drill-down maps |
