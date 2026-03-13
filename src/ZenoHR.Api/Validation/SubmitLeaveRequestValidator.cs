// REQ-HR-002, CTL-BCEA-003: Input validation for leave request submission.
// VUL-027: Closes missing input validation on API endpoints (MEDIUM severity).

using FluentValidation;
using ZenoHR.Api.Endpoints;

namespace ZenoHR.Api.Validation;

/// <summary>
/// Validates <see cref="SubmitLeaveRequestDto"/> before processing.
/// REQ-HR-002: Leave type, dates, and hours must be valid.
/// CTL-BCEA-003: Total hours must be positive (balance check is in domain).
/// </summary>
public sealed class SubmitLeaveRequestValidator : AbstractValidator<SubmitLeaveRequestDto>
{
    private static readonly string[] ValidLeaveTypes =
        ["Annual", "Sick", "FamilyResponsibility", "Maternity", "StudyLeave"];

    public SubmitLeaveRequestValidator()
    {
        RuleFor(x => x.LeaveType)
            .NotEmpty().WithMessage("LeaveType is required.")
            .Must(x => ValidLeaveTypes.Contains(x))
            .WithMessage($"LeaveType must be one of: {string.Join(", ", ValidLeaveTypes)}.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("StartDate is required.")
            .Must(BeAValidDate).WithMessage("StartDate must be a valid date in yyyy-MM-dd format.");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("EndDate is required.")
            .Must(BeAValidDate).WithMessage("EndDate must be a valid date in yyyy-MM-dd format.");

        RuleFor(x => x)
            .Must(x => BeEndDateAfterStartDate(x.StartDate, x.EndDate))
            .WithMessage("EndDate must be on or after StartDate.")
            .When(x => BeAValidDate(x.StartDate) && BeAValidDate(x.EndDate));

        RuleFor(x => x.TotalHours)
            .GreaterThan(0m).WithMessage("TotalHours must be greater than zero.")
            .LessThanOrEqualTo(480m).WithMessage("TotalHours must not exceed 480 (60 working days).");

        RuleFor(x => x.ReasonCode)
            .NotEmpty().WithMessage("ReasonCode is required.")
            .MaximumLength(50).WithMessage("ReasonCode must not exceed 50 characters.");
    }

    private static bool BeAValidDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return DateOnly.TryParseExact(value, "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out _);
    }

    private static bool BeEndDateAfterStartDate(string startDate, string endDate)
    {
        var start = DateOnly.ParseExact(startDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var end = DateOnly.ParseExact(endDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        return end >= start;
    }
}
