// CTL-SARS-006: Tax reference number validation.
// REQ-HR-001: Validates SARS income tax reference numbers for employee records.

using ZenoHR.Domain.Errors;

namespace ZenoHR.Module.Compliance.Services;

/// <summary>
/// Validates SARS income tax reference numbers.
/// CTL-SARS-006: Valid format is 10 digits starting with 0, 1, 2, or 3.
/// </summary>
public static class TaxReferenceValidator
{
    private static readonly char[] ValidFirstDigits = ['0', '1', '2', '3'];

    /// <summary>
    /// Validates a SARS income tax reference number.
    /// CTL-SARS-006: Must be exactly 10 digits, starting with 0, 1, 2, or 3.
    /// </summary>
    /// <param name="taxReference">The tax reference number to validate.</param>
    /// <returns>Success with the validated tax reference, or a failure with the validation error.</returns>
    public static Result<string> Validate(string? taxReference)
    {
        if (string.IsNullOrWhiteSpace(taxReference))
        {
            return Result<string>.Failure(
                ZenoHrErrorCode.RequiredFieldMissing,
                "Tax reference is required.");
        }

        if (taxReference.Length != 10)
        {
            return Result<string>.Failure(
                ZenoHrErrorCode.InvalidFormat,
                "Tax reference must be exactly 10 digits.");
        }

        if (!taxReference.All(char.IsDigit))
        {
            return Result<string>.Failure(
                ZenoHrErrorCode.InvalidFormat,
                "Tax reference must be exactly 10 digits.");
        }

        if (!ValidFirstDigits.Contains(taxReference[0]))
        {
            return Result<string>.Failure(
                ZenoHrErrorCode.InvalidFormat,
                "Tax reference must start with 0, 1, 2, or 3.");
        }

        return Result<string>.Success(taxReference);
    }
}
