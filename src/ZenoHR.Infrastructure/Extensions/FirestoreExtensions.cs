// REQ-OPS-001: Firestore DI registration — registers FirestoreDb as a singleton.
// FIRESTORE_EMULATOR_HOST env var is read automatically by the Firestore SDK for local dev.

using Google.Cloud.Firestore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZenoHR.Infrastructure.Audit;
using ZenoHR.Infrastructure.Auth;
using ZenoHR.Infrastructure.Firestore;
using ZenoHR.Infrastructure.Seeding;
using ZenoHR.Infrastructure.Services.Payslip;

namespace ZenoHR.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering the Firestore connection and related infrastructure.
/// </summary>
public static class FirestoreExtensions
{
    /// <summary>
    /// Registers <see cref="FirestoreDb"/> as a singleton, configured from
    /// the "Firebase:ProjectId" key in appsettings.json (or User Secrets / Key Vault overlay).
    /// <para>
    /// For local development with the Firestore emulator, set:
    /// <c>FIRESTORE_EMULATOR_HOST=localhost:8080</c>
    /// The Firestore SDK reads this environment variable automatically.
    /// </para>
    /// </summary>
    public static IServiceCollection AddZenoHrFirestore(
        this IServiceCollection services, IConfiguration configuration)
    {
        var projectId = configuration["Firebase:ProjectId"];

        services.AddSingleton<FirestoreDb>(_ =>
        {
            if (string.IsNullOrWhiteSpace(projectId))
                throw new InvalidOperationException(
                    "Firebase:ProjectId must be set in configuration. " +
                    "Use .NET User Secrets for local development (see TASK-036).");

            return FirestoreDb.Create(projectId);
        });

        // Register the statutory rule set loader and repository
        services.AddSingleton<StatutoryRuleSetLoader>();
        services.AddSingleton<StatutoryRuleSetRepository>();

        // REQ-COMP-005, CTL-POPIA-012: Audit event writer + repository
        services.AddSingleton<AuditEventRepository>();
        services.AddSingleton<AuditEventWriter>();

        // REQ-HR-001: Employee + contract + subcollection repositories (TASK-064, TASK-065, TASK-066)
        services.AddSingleton<EmployeeRepository>();
        services.AddSingleton<EmploymentContractRepository>();
        services.AddSingleton<BankAccountRepository>();
        services.AddSingleton<NextOfKinRepository>();
        services.AddSingleton<EmployeeBenefitRepository>();

        // REQ-HR-002: Leave repositories (TASK-068, TASK-069)
        services.AddSingleton<LeaveBalanceRepository>();
        services.AddSingleton<LeaveRequestRepository>();

        // REQ-OPS-003: Time & attendance repositories (TASK-071, TASK-072)
        services.AddSingleton<ClockEntryRepository>();
        services.AddSingleton<TimesheetFlagRepository>();

        // REQ-HR-003: Payroll repositories (TASK-082, TASK-083, TASK-084)
        services.AddSingleton<PayrollRunRepository>();
        services.AddSingleton<PayrollResultRepository>();
        services.AddSingleton<PayrollAdjustmentRepository>();

        // REQ-HR-003: Payroll orchestration service (TASK-085)
        services.AddSingleton<ZenoHR.Infrastructure.Services.PayrollOrchestrationService>();

        // REQ-HR-004, CTL-SARS-005: QuestPDF payslip PDF generator (TASK-087)
        services.AddSingleton<IPayslipPdfGenerator, PayslipPdfGenerator>();

        // REQ-SEC-002, REQ-SEC-003: RBAC — user role assignments repository
        // Note: ZenoHrClaimsTransformation (IClaimsTransformation) is registered in
        // ZenoHR.Api via AddZenoHrFirebaseAuth() since it depends on ASP.NET Core types.
        // UserRoleAssignmentRepository is also registered there (to avoid double-registration).

        return services;
    }
}
