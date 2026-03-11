// CTL-SARS-010: Interface for SARS eFiling API client.
// Polly retry and circuit-breaker policies should wrap the real implementation via DI decoration.

using ZenoHR.Domain.Errors;

namespace ZenoHR.Module.Compliance.Services.EFiling;

/// <summary>
/// Abstraction over the SARS eFiling API.
/// <para>
/// Production implementation should use Polly for retry (exponential back-off, max 3 attempts)
/// and circuit-breaker (break after 5 consecutive failures, 30-second recovery window) policies.
/// </para>
/// <para>
/// <see cref="StubEFilingClient"/> provides a logging/mock implementation for now.
/// </para>
/// </summary>
public interface IEFilingClient
{
    /// <summary>
    /// Submit a tax return file to SARS eFiling.
    /// </summary>
    /// <param name="request">The submission payload including file content and metadata.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing the submission outcome or an error.</returns>
    Task<Result<EFilingSubmissionResult>> SubmitAsync(EFilingSubmissionRequest request, CancellationToken ct);

    /// <summary>
    /// Query the status of a previously submitted return.
    /// </summary>
    /// <param name="submissionId">The ZenoHR submission ID.</param>
    /// <param name="tenantId">Tenant scope for authorization.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing the current submission status or an error.</returns>
    Task<Result<EFilingSubmissionResult>> GetStatusAsync(string submissionId, string tenantId, CancellationToken ct);

    /// <summary>
    /// Retrieve submission history for a tenant and tax year.
    /// </summary>
    /// <param name="tenantId">Tenant scope.</param>
    /// <param name="taxYear">Tax year to query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing the list of submissions or an error.</returns>
    Task<Result<IReadOnlyList<EFilingSubmissionResult>>> GetSubmissionHistoryAsync(
        string tenantId, int taxYear, CancellationToken ct);
}
