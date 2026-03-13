// REQ-HR-001, REQ-SEC-002, REQ-SEC-005: Employee API endpoints.
// TASK-067: GET list, GET by ID, POST (create), PUT (update profile).
// VUL-009: GET /{id} returns role-filtered DTO via EmployeeDtoMapper.
// All endpoints require authentication. Role access enforced per RBAC spec (PRD-15).

using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZenoHR.Api.Auth;
using ZenoHR.Api.DTOs;
using ZenoHR.Domain.Common;
using ZenoHR.Infrastructure.Firestore;
using ZenoHR.Module.Employee.Aggregates;

namespace ZenoHR.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for the Employee module.
/// REQ-HR-001: CRUD operations on employee records.
/// REQ-SEC-005: Every endpoint resolves tenant_id from JWT claim — never from request body.
/// </summary>
public static class EmployeeEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/employees")
            .RequireAuthorization()
            .RequireRateLimiting("general-api")   // REQ-SEC-003: closes VUL-007
            .WithTags("Employees");

        // GET /api/employees — list all (Director/HRManager) or own dept (Manager)
        group.MapGet("/", ListEmployeesAsync)
            .WithName("ListEmployees")
            .Produces<IReadOnlyList<EmployeeSummaryDto>>(200);

        // GET /api/employees/{id} — get by ID (own record always allowed; others need role)
        group.MapGet("/{id}", GetEmployeeByIdAsync)
            .WithName("GetEmployeeById")
            .Produces<EmployeeDetailDto>(200)
            .Produces(404);

        // POST /api/employees — create new employee (Director/HRManager only)
        group.MapPost("/", CreateEmployeeAsync)
            .WithName("CreateEmployee")
            .RequireAuthorization(policy => policy.RequireRole("Director", "HRManager"))
            .Produces<EmployeeDetailDto>(201)
            .Produces<ProblemDetails>(400);

        // PUT /api/employees/{id}/profile — update mutable profile fields
        group.MapPut("/{id}/profile", UpdateProfileAsync)
            .WithName("UpdateEmployeeProfile")
            .Produces<EmployeeDetailDto>(200)
            .Produces<ProblemDetails>(400)
            .Produces(404);

        // PUT /api/employees/{id}/terminate — terminate employment (Director/HRManager only)
        // REQ-SEC-004: MFA required — termination is a privileged, irreversible operation (closes VUL-003).
        group.MapPut("/{id}/terminate", TerminateEmployeeAsync)
            .WithName("TerminateEmployee")
            .RequireAuthorization(ZenoHrPolicies.RequiresMfa)
            .Produces(200)
            .Produces<ProblemDetails>(400)
            .Produces(404);

        return app;
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private static async Task<IResult> ListEmployeesAsync(
        ClaimsPrincipal user,
        EmployeeRepository repo,
        CancellationToken ct)
    {
        // REQ-SEC-005: tenant always from JWT — never from request
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var systemRole = user.FindFirstValue(ZenoHrClaimNames.SystemRoleJwt) ?? "";

        IReadOnlyList<Employee> employees;

        if (systemRole is "Director" or "HRManager")
        {
            // Full tenant access — all employees
            employees = await repo.ListByTenantAsync(tenantId, ct);
        }
        else if (systemRole == "Manager")
        {
            // Manager sees their primary department only
            var deptId = user.FindFirstValue(ZenoHrClaimNames.DeptId) ?? "";
            employees = string.IsNullOrWhiteSpace(deptId)
                ? []
                : await repo.ListByDepartmentAsync(tenantId, deptId, ct);
        }
        else
        {
            // Employee: only own record
            var empId = user.FindFirstValue(ZenoHrClaimNames.EmployeeId) ?? "";
            var own = await repo.GetByEmployeeIdAsync(tenantId, empId, ct);
            employees = own.IsSuccess ? [own.Value!] : [];
        }

        return Results.Ok(employees.Select(ToSummaryDto));
    }

    // VUL-009: Role-filtered employee response — Director/HRManager get EmployeeFullDto,
    // Manager gets EmployeeProfileDto (no salary/tax/banking), Employee gets EmployeeSelfDto (own only).
    private static async Task<IResult> GetEmployeeByIdAsync(
        string id,
        ClaimsPrincipal user,
        EmployeeRepository repo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var result = await repo.GetByEmployeeIdAsync(tenantId, id, ct);

        if (result.IsFailure) return Results.NotFound(result.Error!.Message);

        // Employee self-access: always allowed. Others: role check.
        var ownEmpId = user.FindFirstValue(ZenoHrClaimNames.EmployeeId);
        var systemRole = user.FindFirstValue(ZenoHrClaimNames.SystemRoleJwt) ?? "";

        // VUL-009: Employee can only access their own record — 403 for any other employee.
        if (id != ownEmpId && systemRole is not ("Director" or "HRManager" or "Manager"))
            return Results.Forbid();

        // VUL-009: Return role-appropriate DTO shape via EmployeeDtoMapper.
        // Director/HRManager → EmployeeFullDto (masked PII, all fields).
        // Manager → EmployeeProfileDto (no salary, tax, banking, national ID).
        // Employee → EmployeeSelfDto (minimal own-record view).
        var roleDto = EmployeeDtoMapper.ToRoleDto(result.Value!, systemRole);
        return Results.Ok(roleDto);
    }

    private static async Task<IResult> CreateEmployeeAsync(
        [FromBody] CreateEmployeeRequest req,
        ClaimsPrincipal user,
        EmployeeRepository repo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var actorId = user.FindFirstValue(ZenoHrClaimNames.EmployeeId) ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var now = DateTimeOffset.UtcNow;

        if (!Enum.TryParse<EmployeeType>(req.EmployeeType, out var empType))
            return Results.BadRequest($"Invalid employee type: {req.EmployeeType}");

        var result = Employee.Create(
            employeeId: $"emp_{Guid.CreateVersion7()}",
            tenantId: tenantId,
            firebaseUid: req.FirebaseUid,
            legalName: req.LegalName,
            nationalIdOrPassport: req.NationalIdOrPassport,
            taxReference: req.TaxReference,
            dateOfBirth: DateOnly.Parse(req.DateOfBirth, System.Globalization.CultureInfo.InvariantCulture),
            personalPhoneNumber: req.PersonalPhoneNumber,
            personalEmail: req.PersonalEmail,
            workEmail: req.WorkEmail,
            nationality: req.Nationality,
            gender: req.Gender,
            race: req.Race,
            disabilityStatus: req.DisabilityStatus,
            disabilityDescription: req.DisabilityDescription,
            hireDate: DateOnly.Parse(req.HireDate, System.Globalization.CultureInfo.InvariantCulture),
            employeeType: empType,
            departmentId: req.DepartmentId,
            roleId: req.RoleId,
            systemRole: req.SystemRole,
            reportsToEmployeeId: req.ReportsToEmployeeId,
            actorId: actorId,
            now: now);

        if (result.IsFailure)
            return Results.BadRequest(result.Error!.Message);

        var saveResult = await repo.SaveAsync(result.Value!, ct);
        if (saveResult.IsFailure)
            return Results.Problem(saveResult.Error!.Message);

        // VUL-009: Return role-filtered DTO — only Director/HRManager can call this endpoint,
        // but use the mapper consistently to avoid leaking unmasked PII in responses.
        var systemRole = user.FindFirstValue(ZenoHrClaimNames.SystemRoleJwt) ?? "";
        var roleDto = EmployeeDtoMapper.ToRoleDto(result.Value!, systemRole);
        return Results.Created($"/api/employees/{result.Value!.EmployeeId}", roleDto);
    }

    private static async Task<IResult> UpdateProfileAsync(
        string id,
        [FromBody] UpdateProfileRequest req,
        ClaimsPrincipal user,
        EmployeeRepository repo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var actorId = user.FindFirstValue(ZenoHrClaimNames.EmployeeId) ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var getResult = await repo.GetByEmployeeIdAsync(tenantId, id, ct);
        if (getResult.IsFailure) return Results.NotFound(getResult.Error!.Message);

        var emp = getResult.Value!;

        // Employees may only update their own profile
        var ownEmpId = user.FindFirstValue(ZenoHrClaimNames.EmployeeId);
        var systemRole = user.FindFirstValue(ZenoHrClaimNames.SystemRoleJwt) ?? "";
        if (id != ownEmpId && systemRole is not ("Director" or "HRManager"))
            return Results.Forbid();

        var updateResult = emp.UpdateProfile(
            legalName: req.LegalName,
            personalPhoneNumber: req.PersonalPhoneNumber,
            personalEmail: req.PersonalEmail,
            workEmail: req.WorkEmail,
            maritalStatus: req.MaritalStatus,
            taxReference: req.TaxReference,
            bankAccountRef: req.BankAccountRef,
            actorId: actorId,
            now: DateTimeOffset.UtcNow);

        if (updateResult.IsFailure) return Results.BadRequest(updateResult.Error!.Message);

        var saveResult = await repo.SaveAsync(emp, ct);
        if (saveResult.IsFailure) return Results.Problem(saveResult.Error!.Message);

        // VUL-009: Return role-filtered DTO — prevents leaking salary/tax/banking
        // fields to Employee or Manager roles when they update their own profile.
        var roleDto = EmployeeDtoMapper.ToRoleDto(emp, systemRole);
        return Results.Ok(roleDto);
    }

    private static async Task<IResult> TerminateEmployeeAsync(
        string id,
        [FromBody] TerminateEmployeeRequest req,
        ClaimsPrincipal user,
        EmployeeRepository repo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var actorId = user.FindFirstValue(ZenoHrClaimNames.EmployeeId) ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var getResult = await repo.GetByEmployeeIdAsync(tenantId, id, ct);
        if (getResult.IsFailure) return Results.NotFound(getResult.Error!.Message);

        var terminateResult = getResult.Value!.Terminate(
            terminationReasonCode: req.TerminationReasonCode,
            effectiveDate: DateOnly.Parse(req.EffectiveDate, System.Globalization.CultureInfo.InvariantCulture),
            actorId: actorId,
            now: DateTimeOffset.UtcNow);

        if (terminateResult.IsFailure) return Results.BadRequest(terminateResult.Error!.Message);

        var saveResult = await repo.SaveAsync(getResult.Value!, ct);
        if (saveResult.IsFailure) return Results.Problem(saveResult.Error!.Message);

        return Results.Ok(new { message = "Employee terminated successfully." });
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    private static EmployeeSummaryDto ToSummaryDto(Employee e) => new(
        e.EmployeeId, e.LegalName, e.PersonalEmail, e.DepartmentId,
        e.SystemRole, e.EmploymentStatus.ToString(),
        e.HireDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));

    private static EmployeeDetailDto ToDetailDto(Employee e) => new(
        e.EmployeeId, e.TenantId, e.FirebaseUid, e.LegalName,
        e.PersonalEmail, e.WorkEmail, e.PersonalPhoneNumber,
        e.DateOfBirth.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        e.Nationality, e.Gender, e.Race,
        e.DisabilityStatus,
        e.HireDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        e.EmploymentStatus.ToString(), e.EmployeeType.ToString(),
        e.DepartmentId, e.SystemRole, e.DataStatus,
        e.CreatedAt, e.UpdatedAt);
}

// ── Request / Response DTOs ───────────────────────────────────────────────────

public sealed record EmployeeSummaryDto(
    string EmployeeId, string LegalName, string PersonalEmail,
    string DepartmentId, string SystemRole, string EmploymentStatus, string HireDate);

public sealed record EmployeeDetailDto(
    string EmployeeId, string TenantId, string FirebaseUid, string LegalName,
    string PersonalEmail, string? WorkEmail, string PersonalPhoneNumber,
    string DateOfBirth, string Nationality, string Gender, string Race,
    bool DisabilityStatus, string HireDate,
    string EmploymentStatus, string EmployeeType,
    string DepartmentId, string SystemRole, string DataStatus,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record CreateEmployeeRequest(
    string FirebaseUid, string LegalName, string NationalIdOrPassport,
    string? TaxReference, string DateOfBirth,
    string PersonalPhoneNumber, string PersonalEmail, string? WorkEmail,
    string Nationality, string Gender, string Race,
    bool DisabilityStatus, string? DisabilityDescription,
    string HireDate, string EmployeeType,
    string DepartmentId, string RoleId, string SystemRole,
    string? ReportsToEmployeeId);

public sealed record UpdateProfileRequest(
    string? LegalName, string? PersonalPhoneNumber, string? PersonalEmail,
    string? WorkEmail, string? MaritalStatus, string? TaxReference, string? BankAccountRef);

public sealed record TerminateEmployeeRequest(
    string TerminationReasonCode, string EffectiveDate);
