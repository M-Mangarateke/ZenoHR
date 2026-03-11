// CTL-SARS-010: EMP201 eFiling submission service — connects EMP201 generator output to IEFilingClient.
// TASK-131: Submission workflow + status query for EMP201 monthly declarations.

using System.Globalization;
using Microsoft.Extensions.Logging;
using ZenoHR.Domain.Errors;

namespace ZenoHR.Module.Compliance.Services.EFiling;

/// <summary>
/// Orchestrates EMP201 eFiling submissions by validating inputs, constructing
/// <see cref="EFilingSubmissionRequest"/>, and delegating to <see cref="IEFilingClient"/>.
/// </summary>
public sealed partial class Emp201SubmissionService
{
    private readonly IEFilingClient _eFilingClient;
    private readonly ILogger<Emp201SubmissionService> _logger;

    public Emp201SubmissionService(IEFilingClient eFilingClient, ILogger<Emp201SubmissionService> logger)
    {
        ArgumentNullException.ThrowIfNull(eFilingClient);
        ArgumentNullException.ThrowIfNull(logger);
        _eFilingClient = eFilingClient;
        _logger = logger;
    }

    /// <summary>
    /// Submit an EMP201 CSV file to SARS eFiling.
    /// CTL-SARS-010: Validates inputs, constructs request, delegates to <see cref="IEFilingClient.SubmitAsync"/>.
    /// </summary>
    /// <param name="tenantId">Tenant scope — required for tenant isolation.</param>
    /// <param name="taxYear">SA tax year (e.g. 2026). Must be between 2020 and 2099.</param>
    /// <param name="taxPeriod">Monthly period (1–12).</param>
    /// <param name="emp201Content">Generated EMP201 CSV content as byte array.</param>
    /// <param name="submittedBy">User ID of the person initiating the submission.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the submission outcome or a typed error.</returns>
    public async Task<Result<EFilingSubmissionResult>> SubmitEmp201Async(
        string tenantId,
        int taxYear,
        int taxPeriod,
        byte[] emp201Content,
        string submittedBy,
        CancellationToken ct)
    {
        // ── Input validation ────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result<EFilingSubmissionResult>.Failure(
                ZenoHrErrorCode.RequiredFieldMissing,
                "TenantId is required for EMP201 submission.");
        }

        if (emp201Content is null || emp201Content.Length == 0)
        {
            return Result<EFilingSubmissionResult>.Failure(
                ZenoHrErrorCode.RequiredFieldMissing,
                "EMP201 file content must not be empty.");
        }

        if (taxYear < 2020 || taxYear > 2099)
        {
            return Result<EFilingSubmissionResult>.Failure(
                ZenoHrErrorCode.ValueOutOfRange,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Tax year {0} is outside the valid range (2020–2099).",
                    taxYear));
        }

        if (taxPeriod < 1 || taxPeriod > 12)
        {
            return Result<EFilingSubmissionResult>.Failure(
                ZenoHrErrorCode.ValueOutOfRange,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Tax period {0} is outside the valid range (1–12).",
                    taxPeriod));
        }

        if (string.IsNullOrWhiteSpace(submittedBy))
        {
            return Result<EFilingSubmissionResult>.Failure(
                ZenoHrErrorCode.RequiredFieldMissing,
                "SubmittedBy is required for EMP201 submission.");
        }

        // ── Build request ───────────────────────────────────────────────────
        var fileName = string.Format(
            CultureInfo.InvariantCulture,
            "EMP201_{0}_{1:D2}_{2}.csv",
            taxYear,
            taxPeriod,
            tenantId);

        var request = new EFilingSubmissionRequest(
            TenantId: tenantId,
            SubmissionType: EFilingSubmissionType.EMP201,
            TaxYear: taxYear,
            TaxPeriod: taxPeriod,
            FileContent: emp201Content,
            FileName: fileName,
            SubmittedBy: submittedBy);

        LogSubmissionAttempt(tenantId, taxYear, taxPeriod, fileName);

        // ── Delegate to eFiling client ──────────────────────────────────────
        var result = await _eFilingClient.SubmitAsync(request, ct);

        if (result.IsSuccess)
        {
            LogSubmissionSuccess(result.Value.SubmissionId, tenantId, taxYear, taxPeriod);
        }
        else
        {
            LogSubmissionFailure(tenantId, taxYear, taxPeriod, result.Error.Message);
        }

        return result;
    }

    /// <summary>
    /// Query the status of a previously submitted EMP201.
    /// CTL-SARS-010: Delegates to <see cref="IEFilingClient.GetStatusAsync"/>.
    /// </summary>
    /// <param name="submissionId">The ZenoHR submission ID returned from <see cref="SubmitEmp201Async"/>.</param>
    /// <param name="tenantId">Tenant scope for authorization.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the current submission status or a typed error.</returns>
    public async Task<Result<EFilingSubmissionResult>> GetSubmissionStatusAsync(
        string submissionId, string tenantId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(submissionId))
        {
            return Result<EFilingSubmissionResult>.Failure(
                ZenoHrErrorCode.RequiredFieldMissing,
                "SubmissionId is required for status query.");
        }

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result<EFilingSubmissionResult>.Failure(
                ZenoHrErrorCode.RequiredFieldMissing,
                "TenantId is required for status query.");
        }

        LogStatusQuery(submissionId, tenantId);

        return await _eFilingClient.GetStatusAsync(submissionId, tenantId, ct);
    }

    // ── LoggerMessage source-generated log methods ──────────────────────────

    [LoggerMessage(
        EventId = 5020,
        Level = LogLevel.Information,
        Message = "EMP201 submission attempt for tenant {TenantId}, tax year {TaxYear} period {TaxPeriod}, file '{FileName}'")]
    private partial void LogSubmissionAttempt(string tenantId, int taxYear, int taxPeriod, string fileName);

    [LoggerMessage(
        EventId = 5021,
        Level = LogLevel.Information,
        Message = "EMP201 submission succeeded: {SubmissionId} for tenant {TenantId}, tax year {TaxYear} period {TaxPeriod}")]
    private partial void LogSubmissionSuccess(string submissionId, string tenantId, int taxYear, int taxPeriod);

    [LoggerMessage(
        EventId = 5022,
        Level = LogLevel.Warning,
        Message = "EMP201 submission failed for tenant {TenantId}, tax year {TaxYear} period {TaxPeriod}: {ErrorMessage}")]
    private partial void LogSubmissionFailure(string tenantId, int taxYear, int taxPeriod, string errorMessage);

    [LoggerMessage(
        EventId = 5023,
        Level = LogLevel.Information,
        Message = "EMP201 status query for submission {SubmissionId}, tenant {TenantId}")]
    private partial void LogStatusQuery(string submissionId, string tenantId);
}
