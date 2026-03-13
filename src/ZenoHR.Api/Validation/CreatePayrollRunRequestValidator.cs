// REQ-HR-003, CTL-SARS-001: Input validation for payroll run creation.
// VUL-027: Closes missing input validation on API endpoints (MEDIUM severity).

using FluentValidation;
using ZenoHR.Api.Endpoints;

namespace ZenoHR.Api.Validation;

/// <summary>
/// Validates <see cref="CreatePayrollRunRequest"/> before processing.
/// REQ-HR-003: Period, run type, employee IDs, and rule set version must be valid.
/// </summary>
public sealed class CreatePayrollRunRequestValidator : AbstractValidator<CreatePayrollRunRequest>
{
    private static readonly string[] ValidRunTypes = ["Monthly", "Weekly"];

    public CreatePayrollRunRequestValidator()
    {
        RuleFor(x => x.Period)
            .NotEmpty().WithMessage("Period is required.")
            .MaximumLength(20).WithMessage("Period must not exceed 20 characters.")
            .Matches(@"^\d{4}-\d{2}$|^\d{4}-W\d{2}$")
            .WithMessage("Period must be in yyyy-MM (monthly) or yyyy-Wnn (weekly) format.");

        RuleFor(x => x.RunType)
            .NotEmpty().WithMessage("RunType is required.")
            .Must(x => ValidRunTypes.Contains(x))
            .WithMessage($"RunType must be one of: {string.Join(", ", ValidRunTypes)}.");

        RuleFor(x => x.EmployeeIds)
            .NotNull().WithMessage("EmployeeIds is required.")
            .Must(x => x.Count > 0).WithMessage("EmployeeIds must not be empty.")
            .Must(x => x.Count <= 500).WithMessage("EmployeeIds must not exceed 500 entries per run.");

        RuleFor(x => x.RuleSetVersion)
            .NotEmpty().WithMessage("RuleSetVersion is required.")
            .MaximumLength(20).WithMessage("RuleSetVersion must not exceed 20 characters.");

        RuleFor(x => x.IdempotencyKey)
            .MaximumLength(100).WithMessage("IdempotencyKey must not exceed 100 characters.")
            .When(x => x.IdempotencyKey is not null);
    }
}
