// REQ-OPS-001: ZenoHrError — typed, immutable error value carried by Result<T>.

namespace ZenoHR.Domain.Errors;

/// <summary>
/// Immutable error value carried by a failed <see cref="Result{T}"/>.
/// Always includes a machine-readable <see cref="Code"/> and a human-readable <see cref="Message"/>.
/// </summary>
public sealed record ZenoHrError
{
    public ZenoHrErrorCode Code { get; }
    public string Message { get; }

    /// <summary>Optional: which field or property triggered this error (for validation errors).</summary>
    public string? PropertyName { get; }

    /// <summary>Optional: the invalid value that caused the error (useful for logging).</summary>
    public object? AttemptedValue { get; }

    public ZenoHrError(ZenoHrErrorCode code, string message, string? propertyName = null, object? attemptedValue = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code;
        Message = message;
        PropertyName = propertyName;
        AttemptedValue = attemptedValue;
    }

    // ── Factory helpers ──────────────────────────────────────────────────────

    public static ZenoHrError NotFound(ZenoHrErrorCode code, string entity, string id) =>
        new(code, $"{entity} with ID '{id}' was not found.");

    public static ZenoHrError ValidationFailed(string propertyName, string message, object? attemptedValue = null) =>
        new(ZenoHrErrorCode.ValidationFailed, message, propertyName, attemptedValue);

    public static ZenoHrError Forbidden(string? reason = null) =>
        new(ZenoHrErrorCode.Forbidden, reason ?? "You do not have permission to perform this action.");

    public static ZenoHrError Unauthorized() =>
        new(ZenoHrErrorCode.Unauthorized, "Authentication is required.");

    public static ZenoHrError HashChainBroken(string detail) =>
        new(ZenoHrErrorCode.HashChainBroken, $"Audit chain integrity violation: {detail}");

    public static ZenoHrError PayslipInvariantViolation(string detail) =>
        new(ZenoHrErrorCode.PayslipInvariantViolation, $"Payslip invariant violated: {detail}");

    public override string ToString() =>
        PropertyName is null
            ? $"[{Code}] {Message}"
            : $"[{Code}] {PropertyName}: {Message}";
}
