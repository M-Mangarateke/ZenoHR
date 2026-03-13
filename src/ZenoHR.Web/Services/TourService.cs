// REQ-OPS-001: Product tour service — manages tour state, role-specific steps, and checklist items.
// Scoped service: one per Blazor Server circuit. Uses localStorage via JS interop.

using Microsoft.JSInterop;

namespace ZenoHR.Web.Services;

/// <summary>
/// Manages interactive product tour state and role-specific onboarding steps.
/// Registered as Scoped — each Blazor circuit gets its own instance.
/// </summary>
public sealed class TourService
{
    private readonly IJSRuntime _js;

    public TourService(IJSRuntime js)
    {
        ArgumentNullException.ThrowIfNull(js);
        _js = js;
    }

    /// <summary>
    /// Checks whether the user has completed or skipped the specified tour.
    /// </summary>
    public async Task<bool> HasCompletedTourAsync(string userId, string tourId)
    {
        try
        {
            return await _js.InvokeAsync<bool>("ZenoHRTour.hasTourCompleted", userId, tourId);
        }
        catch (JSDisconnectedException)
        {
            return true; // Assume completed if circuit is disconnected
        }
        catch (InvalidOperationException)
        {
            return true; // JS interop not available during prerender
        }
    }

    /// <summary>
    /// Marks the tour as completed or skipped in localStorage.
    /// </summary>
    public async Task MarkTourCompletedAsync(string userId, string tourId, string status = "completed")
    {
        try
        {
            await _js.InvokeVoidAsync("ZenoHRTour.markTourCompleted", userId, tourId, status);
        }
        catch (JSDisconnectedException)
        {
            // Circuit disconnected — safe to ignore.
        }
        catch (InvalidOperationException)
        {
            // JS interop not available during prerender.
        }
    }

    /// <summary>
    /// Determines whether the tour should be shown for this user.
    /// </summary>
    public async Task<bool> ShouldShowTourAsync(string userId, string tourId)
    {
        return !await HasCompletedTourAsync(userId, tourId);
    }

    /// <summary>
    /// Starts the tour engine with the given steps via JS interop.
    /// </summary>
    public async Task StartTourAsync(IReadOnlyList<TourStep> steps)
    {
        try
        {
            await _js.InvokeVoidAsync("ZenoHRTour.start", steps);
        }
        catch (JSDisconnectedException) { }
        catch (InvalidOperationException) { }
    }

    /// <summary>
    /// Returns role-specific tour steps for the guided onboarding walkthrough.
    /// </summary>
    public static IReadOnlyList<TourStep> GetTourSteps(string systemRole)
    {
        return systemRole switch
        {
            "Employee" => GetEmployeeTourSteps(),
            "Manager" => GetManagerTourSteps(),
            "HRManager" or "Director" => GetHrDirectorTourSteps(),
            "SaasAdmin" => GetSaasAdminTourSteps(),
            _ => GetEmployeeTourSteps()
        };
    }

    /// <summary>
    /// Returns the tour ID for a given role.
    /// </summary>
    public static string GetTourId(string systemRole)
    {
        return systemRole switch
        {
            "Employee" => "employee-onboarding",
            "Manager" => "manager-onboarding",
            "HRManager" or "Director" => "hr-director-onboarding",
            "SaasAdmin" => "saasadmin-onboarding",
            _ => "employee-onboarding"
        };
    }

    /// <summary>
    /// Returns role-specific onboarding checklist items.
    /// </summary>
    public static IReadOnlyList<ChecklistItem> GetChecklistItems(string systemRole)
    {
        return systemRole switch
        {
            "Employee" => new ChecklistItem[]
            {
                new("view-dashboard", "View your dashboard"),
                new("check-leave-balance", "Check your leave balance"),
                new("download-payslip", "Download a payslip"),
            },
            "Manager" => new ChecklistItem[]
            {
                new("view-team", "View your team"),
                new("approve-leave", "Approve pending leave"),
                new("review-timesheets", "Review timesheets"),
            },
            "HRManager" or "Director" => new ChecklistItem[]
            {
                new("review-employees", "Review employee list"),
                new("run-payroll-preview", "Run a payroll preview"),
                new("check-compliance", "Check compliance score"),
                new("view-audit-trail", "View audit trail"),
            },
            "SaasAdmin" => new ChecklistItem[]
            {
                new("check-system-health", "Check system health"),
                new("review-security-ops", "Review security ops"),
                new("view-audit-trail", "View audit trail"),
            },
            _ => Array.Empty<ChecklistItem>()
        };
    }

    // ── Checklist state management ─────────────────────────────────────────────

    /// <summary>
    /// Gets the checklist state from localStorage.
    /// Returns null if no checklist exists or it has expired.
    /// </summary>
    public async Task<ChecklistState?> GetChecklistStateAsync(string userId, string role)
    {
        try
        {
            return await _js.InvokeAsync<ChecklistState?>("ZenoHRTour.getChecklistState", userId, role);
        }
        catch (JSDisconnectedException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    /// <summary>
    /// Saves checklist state to localStorage.
    /// </summary>
    public async Task SaveChecklistStateAsync(string userId, string role, Dictionary<string, bool> items, bool dismissed = false)
    {
        try
        {
            await _js.InvokeVoidAsync("ZenoHRTour.saveChecklistState", userId, role, items, dismissed);
        }
        catch (JSDisconnectedException) { }
        catch (InvalidOperationException) { }
    }

    /// <summary>
    /// Dismisses (removes) the checklist from localStorage.
    /// </summary>
    public async Task DismissChecklistAsync(string userId, string role)
    {
        try
        {
            await _js.InvokeVoidAsync("ZenoHRTour.dismissChecklist", userId, role);
        }
        catch (JSDisconnectedException) { }
        catch (InvalidOperationException) { }
    }

    // ── Private step definitions ───────────────────────────────────────────────

    // REQ-OPS-001: Employee onboarding — 5 steps
    private static IReadOnlyList<TourStep> GetEmployeeTourSteps() => new TourStep[]
    {
        new(".zenohr-content", "Welcome to ZenoHR!", "This is your personal dashboard with key stats at a glance."),
        new("[data-nav='payroll']", "My Payslips", "View and download your payslips here. They're generated automatically each pay period."),
        new("[data-nav='leave']", "Leave Requests", "Request leave, check your balances, and track approval status."),
        new("[data-nav='my-analytics']", "My Analytics", "See your personal earnings trends, tax summary, and leave usage."),
        new(".topbar-actions", "Your Profile", "Keep your personal details up to date and manage your preferences here."),
    };

    // REQ-OPS-001: Manager onboarding — 6 steps
    private static IReadOnlyList<TourStep> GetManagerTourSteps() => new TourStep[]
    {
        new(".zenohr-content", "Team Dashboard", "Your team dashboard shows headcount, pending approvals, and key metrics."),
        new("[data-nav='employees']", "Team Employees", "View and manage your department's employee profiles."),
        new("[data-nav='leave']", "Leave Approvals", "Approve or decline leave requests from your team members."),
        new("[data-nav='timesheets']", "Timesheet Review", "Review and approve weekly timesheets for your department."),
        new("[data-nav='analytics']", "Team Analytics", "Track your team's attendance patterns and leave trends."),
        new("[data-nav='payroll']", "My Payslips", "Access your own payslips and personal analytics too."),
    };

    // REQ-OPS-001: HRManager/Director onboarding — 8 steps
    private static IReadOnlyList<TourStep> GetHrDirectorTourSteps() => new TourStep[]
    {
        new(".zenohr-content", "Command Centre", "Your command centre — company-wide KPIs, compliance status, and alerts."),
        new("[data-nav='employees']", "Employee Management", "Manage all employees — onboard, update contracts, and track records."),
        new("[data-nav='payroll']", "Payroll", "Run monthly payroll, review calculations, and generate payslips."),
        new("[data-nav='leave']", "Leave Management", "Company-wide leave calendar with approval workflows."),
        new("[data-nav='compliance']", "Compliance", "SARS filing status, BCEA checks, and POPIA control scores."),
        new("[data-nav='audit']", "Audit Trail", "Every action is recorded with tamper-proof hash chains."),
        new("[data-nav='analytics']", "Analytics", "Company analytics — headcount trends, payroll costs, salary bands."),
        new("[data-nav='settings']", "Settings", "Configure departments, manage users, set security policies."),
    };

    // REQ-OPS-001: SaasAdmin onboarding — 5 steps
    private static IReadOnlyList<TourStep> GetSaasAdminTourSteps() => new TourStep[]
    {
        new("[data-nav='admin']", "Admin Console", "Platform operations — tenant management, system health, and metrics."),
        new("[data-nav='security-ops']", "Security Operations", "OWASP coverage, vulnerability tracking, and POPIA control status."),
        new("[data-nav='audit']", "Audit Trail", "Cross-tenant audit visibility for platform-level monitoring."),
        new(".zenohr-content", "System Health", "Application health checks, performance metrics, and alerts."),
        new("[data-nav='admin']", "Tenant Management", "Manage tenant configurations and platform settings."),
    };
}

/// <summary>
/// Represents a single step in the product tour.
/// </summary>
public sealed record TourStep(string Selector, string Title, string Description);

/// <summary>
/// Represents a single onboarding checklist item.
/// </summary>
public sealed record ChecklistItem(string Id, string Label);

/// <summary>
/// Represents the persisted state of an onboarding checklist.
/// </summary>
public sealed record ChecklistState
{
    public Dictionary<string, bool>? Items { get; init; }
    public bool Dismissed { get; init; }
    public long CreatedAt { get; init; }
}
