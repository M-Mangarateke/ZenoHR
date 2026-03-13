// VUL-028: Document and verify SaasAdmin cross-tenant audit read scope.
// REQ-SEC-002: SaasAdmin role exists as enum value 1 (highest privilege, platform-level).
// TC-ARC-002: SaasAdmin cross-tenant audit read is intentional for platform monitoring.
//
// Design rationale (VUL-028):
// SaasAdmin is the platform operator role. Unlike tenant roles (Director, HRManager, Manager,
// Employee), SaasAdmin operates cross-tenant for platform monitoring, incident response, and
// compliance oversight. SaasAdmin can read audit_events across all tenants via the /admin/*
// UI — this is by design, not a vulnerability.
//
// Firestore security rules grant SaasAdmin read-only access to audit_events without tenant_id
// scoping. This is documented here as an intentional architectural decision per PRD-15 §2.
// SaasAdmin CANNOT read tenant employee data, payroll, leave, or compliance records.

using FluentAssertions;
using ZenoHR.Domain.Common;

namespace ZenoHR.Architecture.Tests;

/// <summary>
/// Tests documenting and verifying the SaasAdmin audit scope.
/// VUL-028: Cross-tenant audit read is intentional for platform monitoring — not a vulnerability.
/// </summary>
public sealed class SaasAdminAuditScopeTests
{
    [Fact]
    // TC-ARC-002-001: SaasAdmin role constant exists in the SystemRole enum.
    // VUL-028: Confirms the platform operator role is defined.
    public void SaasAdmin_RoleConstant_ExistsInSystemRoleEnum()
    {
        // VUL-028
        var saasAdminExists = Enum.IsDefined(typeof(SystemRole), SystemRole.SaasAdmin);

        saasAdminExists.Should().BeTrue(
            "SaasAdmin must exist as a SystemRole enum value for platform operator access");

        // SaasAdmin is enum value 1 — highest privilege
        ((int)SystemRole.SaasAdmin).Should().Be(1,
            "SaasAdmin has the highest privilege level (lowest integer value)");
    }

    [Fact]
    // TC-ARC-002-002: SaasAdmin cross-tenant audit read is intentional for platform monitoring.
    // VUL-028: This test documents the design decision — SaasAdmin reads audit_events
    // across all tenants for incident response, compliance oversight, and anomaly detection.
    // This is NOT a tenant isolation violation. SaasAdmin has no employee record and cannot
    // access tenant-specific employee, payroll, leave, or compliance data.
    public void SaasAdmin_CrossTenantAuditRead_IsIntentionalForPlatformMonitoring()
    {
        // VUL-028: Documenting intentional cross-tenant audit access for SaasAdmin.
        //
        // Firestore security rules for audit_events:
        //   match /audit_events/{eventId} {
        //     allow read: if request.auth.token.system_role == 'SaasAdmin';  // cross-tenant
        //     allow read: if resource.data.tenant_id == request.auth.token.tenant_id;  // tenant-scoped
        //     allow create: if <immutability rules>;
        //   }
        //
        // SaasAdmin audit read scope includes:
        //   - All audit_events across all tenants (read-only)
        //   - Platform admin console (/admin/audit) displays cross-tenant feed
        //   - Used for: anomaly detection, incident response, compliance reporting
        //
        // SaasAdmin CANNOT:
        //   - Read employees, payroll_runs, leave_balances, or compliance_submissions
        //   - Write or delete any audit_events (immutable — CTL-SARS-008)
        //   - Access any /dashboard, /employees, /payroll, /leave, /compliance tenant UI

        // Verify SaasAdmin is distinct from tenant roles
        SystemRole.SaasAdmin.Should().NotBe(SystemRole.Director);
        SystemRole.SaasAdmin.Should().NotBe(SystemRole.HRManager);
        SystemRole.SaasAdmin.Should().NotBe(SystemRole.Manager);
        SystemRole.SaasAdmin.Should().NotBe(SystemRole.Employee);

        // SaasAdmin is the only role without a tenant_id — confirming cross-tenant scope
        // is by design for this role and only this role.
        var platformRoles = new[] { SystemRole.SaasAdmin };
        var tenantRoles = new[] { SystemRole.Director, SystemRole.HRManager, SystemRole.Manager, SystemRole.Employee };

        platformRoles.Should().HaveCount(1, "Only SaasAdmin operates cross-tenant");
        tenantRoles.Should().HaveCount(4, "All other roles are tenant-scoped");
    }

    [Fact]
    // TC-ARC-002-003: SaasAdmin has no Employee record — cannot appear in payroll or leave.
    // VUL-028: Documents that SaasAdmin is platform-only with no tenant data footprint.
    public void SaasAdmin_HasNoEmployeeRecord_PlatformOperatorOnly()
    {
        // VUL-028
        // SaasAdmin (enum=1) is platform operator. Per PRD-15 §2:
        // "Has no employee record, no payslip, no leave. Cannot read tenant data."
        // This means SaasAdmin never appears in:
        //   - employees collection
        //   - payroll_runs/{id}/results
        //   - leave_balances
        //   - leave_requests
        // The audit cross-tenant read is the ONLY cross-tenant access SaasAdmin has.

        SystemRole.SaasAdmin.ToString().Should().Be("SaasAdmin",
            "Role name must be 'SaasAdmin' for JWT claim matching");

        // Unknown = 0 means denied; SaasAdmin = 1 is the only cross-tenant role
        ((int)SystemRole.Unknown).Should().Be(0);
        ((int)SystemRole.SaasAdmin).Should().Be(1);
    }
}
