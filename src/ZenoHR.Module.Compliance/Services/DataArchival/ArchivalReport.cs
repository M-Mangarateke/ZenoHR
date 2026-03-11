// CTL-POPIA-009, CTL-POPIA-015: Archival run report.
// Summarises a nightly archival run with counts and individual entries.

using System.Collections.Immutable;

namespace ZenoHR.Module.Compliance.Services.DataArchival;

/// <summary>
/// Summary report produced by the nightly data archival background service.
/// Contains aggregated counts and individual <see cref="ArchivalRecord"/> entries
/// for all employees processed during the run.
/// </summary>
/// <param name="TenantId">The tenant for which the archival run was executed.</param>
/// <param name="RunDate">The date (SAST) on which the archival run was executed.</param>
/// <param name="TotalEligible">Total number of employees whose retention period has elapsed.</param>
/// <param name="TotalArchived">Number of employees successfully archived in this run.</param>
/// <param name="TotalSkipped">Number of eligible employees skipped (e.g., legal hold, already archived).</param>
/// <param name="Entries">Individual archival records for each processed employee.</param>
public sealed record ArchivalReport(
    string TenantId,
    DateOnly RunDate,
    int TotalEligible,
    int TotalArchived,
    int TotalSkipped,
    ImmutableList<ArchivalRecord> Entries);
