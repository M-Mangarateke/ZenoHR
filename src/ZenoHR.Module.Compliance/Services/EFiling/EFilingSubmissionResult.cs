// CTL-SARS-010: Result model returned after a SARS eFiling submission or status query.

namespace ZenoHR.Module.Compliance.Services.EFiling;

/// <summary>
/// Represents the outcome of a SARS eFiling submission or status inquiry.
/// </summary>
/// <param name="SubmissionId">Unique identifier for this submission (ZenoHR-generated).</param>
/// <param name="Status">Current lifecycle status of the submission.</param>
/// <param name="SubmittedAt">UTC timestamp when the submission was sent.</param>
/// <param name="SarsReferenceNumber">Reference number returned by SARS upon acceptance (null if not yet accepted).</param>
/// <param name="ErrorMessage">Error detail when status is Rejected or Error (null on success).</param>
/// <param name="RetryCount">Number of retry attempts made for this submission.</param>
public sealed record EFilingSubmissionResult(
    string SubmissionId,
    EFilingSubmissionStatus Status,
    DateTimeOffset SubmittedAt,
    string? SarsReferenceNumber,
    string? ErrorMessage,
    int RetryCount);
