// CTL-SARS-010: Stub eFiling client — logs all calls, returns mock success results.
// Replace with a production HTTP client when direct SARS eFiling integration is pursued.
// Production client should use Polly policies:
//   - Retry: exponential back-off, max 3 attempts, jitter
//   - Circuit-breaker: break after 5 consecutive failures, 30-second recovery window

using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;
using ZenoHR.Domain.Errors;

namespace ZenoHR.Module.Compliance.Services.EFiling;

/// <summary>
/// Stub implementation of <see cref="IEFilingClient"/> that logs all calls
/// and returns simulated success results. Replace with real HTTP client when needed.
/// </summary>
public sealed partial class StubEFilingClient : IEFilingClient
{
    private readonly ILogger<StubEFilingClient> _logger;
    private readonly ConcurrentDictionary<string, EFilingSubmissionResult> _submissions = new();
    private int _counter;

    public StubEFilingClient(ILogger<StubEFilingClient> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<EFilingSubmissionResult>> SubmitAsync(
        EFilingSubmissionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TenantId))
        {
            return Task.FromResult(Result<EFilingSubmissionResult>.Failure(
                ZenoHrErrorCode.RequiredFieldMissing,
                "TenantId is required for eFiling submission."));
        }

        if (request.SubmissionType == EFilingSubmissionType.Unknown)
        {
            return Task.FromResult(Result<EFilingSubmissionResult>.Failure(
                ZenoHrErrorCode.InvalidFormat,
                "SubmissionType must be a valid SARS return type."));
        }

        ct.ThrowIfCancellationRequested();

        var seq = Interlocked.Increment(ref _counter);
        var submissionId = string.Format(
            CultureInfo.InvariantCulture,
            "STUB-{0:yyyyMMddHHmmss}-{1:D5}",
            DateTimeOffset.UtcNow,
            seq);

        var result = new EFilingSubmissionResult(
            SubmissionId: submissionId,
            Status: EFilingSubmissionStatus.Submitted,
            SubmittedAt: DateTimeOffset.UtcNow,
            SarsReferenceNumber: null,
            ErrorMessage: null,
            RetryCount: 0);

        _submissions[submissionId] = result;

        LogSubmission(
            request.SubmissionType,
            request.TenantId,
            request.TaxYear,
            request.TaxPeriod,
            request.FileName,
            submissionId);

        return Task.FromResult(Result<EFilingSubmissionResult>.Success(result));
    }

    /// <inheritdoc />
    public Task<Result<EFilingSubmissionResult>> GetStatusAsync(
        string submissionId, string tenantId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Task.FromResult(Result<EFilingSubmissionResult>.Failure(
                ZenoHrErrorCode.RequiredFieldMissing,
                "TenantId is required for eFiling status query."));
        }

        if (string.IsNullOrWhiteSpace(submissionId))
        {
            return Task.FromResult(Result<EFilingSubmissionResult>.Failure(
                ZenoHrErrorCode.RequiredFieldMissing,
                "SubmissionId is required for eFiling status query."));
        }

        ct.ThrowIfCancellationRequested();

        LogStatusQuery(submissionId, tenantId);

        if (_submissions.TryGetValue(submissionId, out var existing))
        {
            // Simulate progression: Submitted -> Accepted on second query
            var updated = existing with { Status = EFilingSubmissionStatus.Accepted };
            _submissions[submissionId] = updated;
            return Task.FromResult(Result<EFilingSubmissionResult>.Success(updated));
        }

        return Task.FromResult(Result<EFilingSubmissionResult>.Failure(
            ZenoHrErrorCode.ComplianceSubmissionNotFound,
            string.Format(
                CultureInfo.InvariantCulture,
                "Submission '{0}' not found.",
                submissionId)));
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<EFilingSubmissionResult>>> GetSubmissionHistoryAsync(
        string tenantId, int taxYear, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Task.FromResult(Result<IReadOnlyList<EFilingSubmissionResult>>.Failure(
                ZenoHrErrorCode.RequiredFieldMissing,
                "TenantId is required for eFiling history query."));
        }

        ct.ThrowIfCancellationRequested();

        LogHistoryQuery(tenantId, taxYear);

        // Stub returns empty history — no persistence across restarts
        IReadOnlyList<EFilingSubmissionResult> empty = Array.Empty<EFilingSubmissionResult>();
        return Task.FromResult(Result<IReadOnlyList<EFilingSubmissionResult>>.Success(empty));
    }

    // ── LoggerMessage source-generated log methods ─────────────────────────

    [LoggerMessage(
        EventId = 5010,
        Level = LogLevel.Information,
        Message = "[STUB eFiling] Submitted {SubmissionType} for tenant {TenantId}, " +
                  "tax year {TaxYear} period {TaxPeriod}, file '{FileName}' -> {SubmissionId}")]
    private partial void LogSubmission(
        EFilingSubmissionType submissionType, string tenantId, int taxYear, int taxPeriod,
        string fileName, string submissionId);

    [LoggerMessage(
        EventId = 5011,
        Level = LogLevel.Information,
        Message = "[STUB eFiling] Status query for submission {SubmissionId}, tenant {TenantId}")]
    private partial void LogStatusQuery(string submissionId, string tenantId);

    [LoggerMessage(
        EventId = 5012,
        Level = LogLevel.Information,
        Message = "[STUB eFiling] History query for tenant {TenantId}, tax year {TaxYear}")]
    private partial void LogHistoryQuery(string tenantId, int taxYear);
}
