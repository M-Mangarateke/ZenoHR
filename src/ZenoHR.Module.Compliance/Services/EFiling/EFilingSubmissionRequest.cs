// CTL-SARS-010: Request model for submitting a return to SARS eFiling.

namespace ZenoHR.Module.Compliance.Services.EFiling;

/// <summary>
/// Encapsulates all data required to submit a tax return via SARS eFiling.
/// </summary>
/// <param name="TenantId">Tenant scope — all submissions are tenant-isolated.</param>
/// <param name="SubmissionType">The SARS return type being submitted.</param>
/// <param name="TaxYear">Tax year the submission relates to (e.g., 2026).</param>
/// <param name="TaxPeriod">Period within the tax year (1-12 for monthly, 1-2 for bi-annual).</param>
/// <param name="FileContent">The generated file content (CSV/XML) to submit.</param>
/// <param name="FileName">Original file name for the submission.</param>
/// <param name="SubmittedBy">User ID of the person initiating the submission.</param>
public sealed record EFilingSubmissionRequest(
    string TenantId,
    EFilingSubmissionType SubmissionType,
    int TaxYear,
    int TaxPeriod,
    byte[] FileContent,
    string FileName,
    string SubmittedBy);
