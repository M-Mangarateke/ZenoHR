// REQ-HR-001, REQ-SEC-005: Input validation for employee creation.
// VUL-027: Closes missing input validation on API endpoints (MEDIUM severity).
// tenant_id is NEVER accepted from user input — it comes from auth claims (REQ-SEC-005).

using FluentValidation;
using ZenoHR.Api.Endpoints;

namespace ZenoHR.Api.Validation;

/// <summary>
/// Validates <see cref="CreateEmployeeRequest"/> before processing.
/// REQ-HR-001: All required fields must be present and well-formed.
/// REQ-SEC-005: No tenant_id in request body — enforced by DTO shape (no TenantId property).
/// </summary>
public sealed class CreateEmployeeRequestValidator : AbstractValidator<CreateEmployeeRequest>
{
    // REQ-HR-001: Valid South African ID number is 13 digits.
    private static readonly string[] ValidSystemRoles = ["Director", "HRManager", "Manager", "Employee"];
    private static readonly string[] ValidEmployeeTypes = ["Permanent", "FixedTerm", "Temporary"];

    public CreateEmployeeRequestValidator()
    {
        RuleFor(x => x.FirebaseUid)
            .NotEmpty().WithMessage("FirebaseUid is required.")
            .MaximumLength(128).WithMessage("FirebaseUid must not exceed 128 characters.");

        RuleFor(x => x.LegalName)
            .NotEmpty().WithMessage("LegalName is required.")
            .MinimumLength(2).WithMessage("LegalName must be at least 2 characters.")
            .MaximumLength(200).WithMessage("LegalName must not exceed 200 characters.");

        RuleFor(x => x.NationalIdOrPassport)
            .NotEmpty().WithMessage("NationalIdOrPassport is required.")
            .MaximumLength(20).WithMessage("NationalIdOrPassport must not exceed 20 characters.");

        RuleFor(x => x.TaxReference)
            .MaximumLength(20).WithMessage("TaxReference must not exceed 20 characters.")
            .When(x => x.TaxReference is not null);

        RuleFor(x => x.DateOfBirth)
            .NotEmpty().WithMessage("DateOfBirth is required.")
            .Must(BeAValidDate).WithMessage("DateOfBirth must be a valid date in yyyy-MM-dd format.");

        RuleFor(x => x.PersonalPhoneNumber)
            .NotEmpty().WithMessage("PersonalPhoneNumber is required.")
            .MaximumLength(20).WithMessage("PersonalPhoneNumber must not exceed 20 characters.");

        RuleFor(x => x.PersonalEmail)
            .NotEmpty().WithMessage("PersonalEmail is required.")
            .EmailAddress().WithMessage("PersonalEmail must be a valid email address.")
            .MaximumLength(254).WithMessage("PersonalEmail must not exceed 254 characters.");

        RuleFor(x => x.WorkEmail)
            .EmailAddress().WithMessage("WorkEmail must be a valid email address.")
            .MaximumLength(254).WithMessage("WorkEmail must not exceed 254 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.WorkEmail));

        RuleFor(x => x.Nationality)
            .NotEmpty().WithMessage("Nationality is required.")
            .MaximumLength(50).WithMessage("Nationality must not exceed 50 characters.");

        RuleFor(x => x.Gender)
            .NotEmpty().WithMessage("Gender is required.")
            .MaximumLength(20).WithMessage("Gender must not exceed 20 characters.");

        RuleFor(x => x.Race)
            .NotEmpty().WithMessage("Race is required.")
            .MaximumLength(30).WithMessage("Race must not exceed 30 characters.");

        RuleFor(x => x.HireDate)
            .NotEmpty().WithMessage("HireDate is required.")
            .Must(BeAValidDate).WithMessage("HireDate must be a valid date in yyyy-MM-dd format.");

        RuleFor(x => x.EmployeeType)
            .NotEmpty().WithMessage("EmployeeType is required.")
            .Must(x => ValidEmployeeTypes.Contains(x))
            .WithMessage($"EmployeeType must be one of: {string.Join(", ", ValidEmployeeTypes)}.");

        RuleFor(x => x.DepartmentId)
            .NotEmpty().WithMessage("DepartmentId is required.")
            .MaximumLength(100).WithMessage("DepartmentId must not exceed 100 characters.");

        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("RoleId is required.")
            .MaximumLength(100).WithMessage("RoleId must not exceed 100 characters.");

        RuleFor(x => x.SystemRole)
            .NotEmpty().WithMessage("SystemRole is required.")
            .Must(x => ValidSystemRoles.Contains(x))
            .WithMessage($"SystemRole must be one of: {string.Join(", ", ValidSystemRoles)}.");

        RuleFor(x => x.ReportsToEmployeeId)
            .MaximumLength(100).WithMessage("ReportsToEmployeeId must not exceed 100 characters.")
            .When(x => x.ReportsToEmployeeId is not null);
    }

    private static bool BeAValidDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return DateOnly.TryParseExact(value, "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out _);
    }
}
