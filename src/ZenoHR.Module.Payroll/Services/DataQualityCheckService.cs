// CTL-POPIA-004: Pre-payroll data quality validation service.
// Validates SA ID numbers, SARS tax references, and banking details
// before allowing payroll finalization.

using System.Globalization;
using ZenoHR.Domain.Errors;

namespace ZenoHR.Module.Payroll.Services;

/// <summary>
/// Pre-payroll data quality checks: validates SA ID (13-digit Luhn), SARS tax reference
/// (10-digit, valid prefix), and bank account reference (6–11 digits) formats.
/// All methods are static — no instance state required.
/// </summary>
public static class DataQualityCheckService
{
    /// <summary>
    /// Validate a South African ID number: 13 digits, valid date of birth (first 6 digits),
    /// and correct Luhn check digit (digit 13).
    /// </summary>
    // CTL-POPIA-004
    public static Result<bool> ValidateSaIdNumber(string idNumber)
    {
        if (string.IsNullOrWhiteSpace(idNumber))
            return Result<bool>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "SA ID number is required.");

        if (idNumber.Length != 13)
            return Result<bool>.Failure(ZenoHrErrorCode.InvalidFormat, "SA ID number must be exactly 13 digits.");

        if (!idNumber.All(char.IsDigit))
            return Result<bool>.Failure(ZenoHrErrorCode.InvalidFormat, "SA ID number must contain only digits.");

        // Validate date of birth from first 6 digits (YYMMDD)
        var yearPart = idNumber[..2];
        var monthPart = idNumber[2..4];
        var dayPart = idNumber[4..6];

        if (!int.TryParse(monthPart, NumberStyles.None, CultureInfo.InvariantCulture, out var month) ||
            month < 1 || month > 12)
        {
            return Result<bool>.Failure(ZenoHrErrorCode.InvalidFormat,
                $"SA ID number contains invalid month '{monthPart}'. Must be 01–12.");
        }

        if (!int.TryParse(dayPart, NumberStyles.None, CultureInfo.InvariantCulture, out var day) ||
            day < 1 || day > 31)
        {
            return Result<bool>.Failure(ZenoHrErrorCode.InvalidFormat,
                $"SA ID number contains invalid day '{dayPart}'. Must be 01–31.");
        }

        // Validate Luhn check digit
        if (!IsValidLuhn(idNumber))
        {
            return Result<bool>.Failure(ZenoHrErrorCode.InvalidFormat,
                "SA ID number failed Luhn check digit validation.");
        }

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Validate a SARS tax reference number: 10 digits, first digit must be 0, 1, 2, 3, or 9.
    /// </summary>
    // CTL-POPIA-004
    public static Result<bool> ValidateTaxReference(string taxRef)
    {
        if (string.IsNullOrWhiteSpace(taxRef))
            return Result<bool>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "Tax reference number is required.");

        if (taxRef.Length != 10)
            return Result<bool>.Failure(ZenoHrErrorCode.InvalidFormat, "Tax reference must be exactly 10 digits.");

        if (!taxRef.All(char.IsDigit))
            return Result<bool>.Failure(ZenoHrErrorCode.InvalidFormat, "Tax reference must contain only digits.");

        var firstDigit = taxRef[0];
        if (firstDigit != '0' && firstDigit != '1' && firstDigit != '2' && firstDigit != '3' && firstDigit != '9')
        {
            return Result<bool>.Failure(ZenoHrErrorCode.InvalidFormat,
                $"Tax reference must start with 0, 1, 2, 3, or 9. Got '{firstDigit}'.");
        }

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Validate a bank account reference: 6 to 11 digits only.
    /// </summary>
    // CTL-POPIA-004
    public static Result<bool> ValidateBankAccountRef(string accountRef)
    {
        if (string.IsNullOrWhiteSpace(accountRef))
            return Result<bool>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "Bank account reference is required.");

        if (!accountRef.All(char.IsDigit))
            return Result<bool>.Failure(ZenoHrErrorCode.InvalidFormat, "Bank account reference must contain only digits.");

        if (accountRef.Length < 6 || accountRef.Length > 11)
        {
            return Result<bool>.Failure(ZenoHrErrorCode.InvalidFormat,
                $"Bank account reference must be 6–11 digits. Got {accountRef.Length} digits.");
        }

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Combined pre-payroll data quality check. Returns success if all validations pass,
    /// or a failure containing all validation error messages.
    /// </summary>
    // CTL-POPIA-004
    public static Result<bool> ValidateEmployeeDataQuality(string saId, string taxRef, string bankAccount)
    {
        var failures = new List<string>();

        var idResult = ValidateSaIdNumber(saId);
        if (idResult.IsFailure)
            failures.Add(idResult.Error.Message);

        var taxResult = ValidateTaxReference(taxRef);
        if (taxResult.IsFailure)
            failures.Add(taxResult.Error.Message);

        var bankResult = ValidateBankAccountRef(bankAccount);
        if (bankResult.IsFailure)
            failures.Add(bankResult.Error.Message);

        if (failures.Count > 0)
        {
            var combined = string.Join(" | ", failures);
            return Result<bool>.Failure(ZenoHrErrorCode.ValidationFailed,
                $"Data quality check failed with {failures.Count} error(s): {combined}");
        }

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Standard Luhn algorithm validation for a numeric string.
    /// </summary>
    private static bool IsValidLuhn(string digits)
    {
        var sum = 0;
        var alternate = false;

        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var n = digits[i] - '0';

            if (alternate)
            {
                n *= 2;
                if (n > 9)
                    n -= 9;
            }

            sum += n;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }
}
