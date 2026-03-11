// REQ-COMP-005: Evidence pack PDF generator via QuestPDF.
// Bundles audit trail, compliance scores, and supporting documents for regulatory submission.

using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ZenoHR.Domain.Errors;

namespace ZenoHR.Infrastructure.Services.Pdf.EvidencePack;

/// <summary>
/// Generates a multi-section A4 evidence pack PDF containing compliance scores,
/// audit trail entries, and supporting document references.
/// </summary>
public sealed class EvidencePackGenerator
{
    private static readonly HashSet<string> ValidPackTypes = new(StringComparer.Ordinal)
    {
        "SARS_AUDIT",
        "POPIA_REVIEW",
        "INTERNAL_AUDIT",
        "BCEA_INSPECTION"
    };

    static EvidencePackGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // REQ-COMP-005: Generate evidence pack PDF from request data.
    public static Result<byte[]> Generate(EvidencePackRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationResult = Validate(request);
        if (validationResult.IsFailure)
            return Result<byte[]>.Failure(validationResult.Error);

        var reference = GenerateReference(request);

        try
        {
            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginHorizontal(40);
                    page.MarginVertical(50);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Content().Column(column =>
                    {
                        ComposeCoverPage(column, request, reference);
                        column.Item().PageBreak();

                        ComposeTableOfContents(column);
                        column.Item().PageBreak();

                        ComposeExecutiveSummary(column, request);
                        column.Item().PageBreak();

                        ComposeComplianceScores(column, request);
                        column.Item().PageBreak();

                        ComposeAuditTrail(column, request);
                        column.Item().PageBreak();

                        ComposeSupportingDocuments(column, request);
                    });

                    page.Footer().Row(row =>
                    {
                        row.RelativeItem().AlignLeft().Text(text =>
                        {
                            text.Span(string.Format(
                                CultureInfo.InvariantCulture,
                                "CONFIDENTIAL — {0} — Evidence Pack {1}",
                                request.CompanyName,
                                reference));
                            text.DefaultTextStyle(x => x.FontSize(7).FontColor(Colors.Grey.Medium));
                        });

                        row.RelativeItem().AlignRight().Text(text =>
                        {
                            text.Span("Page ");
                            text.CurrentPageNumber();
                            text.Span(" of ");
                            text.TotalPages();
                            text.DefaultTextStyle(x => x.FontSize(7).FontColor(Colors.Grey.Medium));
                        });
                    });
                });
            }).GeneratePdf();

            return Result<byte[]>.Success(pdfBytes);
        }
        catch (Exception ex)
        {
            return Result<byte[]>.Failure(
                ZenoHrErrorCode.PdfGenerationFailed,
                string.Format(CultureInfo.InvariantCulture, "Evidence pack PDF generation failed: {0}", ex.Message));
        }
    }

    private static Result Validate(EvidencePackRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId))
            return Result.Failure(ZenoHrErrorCode.RequiredFieldMissing, "TenantId is required.");

        if (string.IsNullOrWhiteSpace(request.CompanyName))
            return Result.Failure(ZenoHrErrorCode.RequiredFieldMissing, "CompanyName is required.");

        if (string.IsNullOrWhiteSpace(request.GeneratedBy))
            return Result.Failure(ZenoHrErrorCode.RequiredFieldMissing, "GeneratedBy is required.");

        if (request.PeriodEnd < request.PeriodStart)
            return Result.Failure(ZenoHrErrorCode.ValidationFailed, "PeriodEnd must be on or after PeriodStart.");

        if (!ValidPackTypes.Contains(request.PackType))
            return Result.Failure(
                ZenoHrErrorCode.ValidationFailed,
                string.Format(CultureInfo.InvariantCulture,
                    "Invalid PackType '{0}'. Must be one of: {1}.",
                    request.PackType,
                    string.Join(", ", ValidPackTypes)));

        if ((request.AuditEntries is null || request.AuditEntries.Count == 0) &&
            (request.ComplianceScores is null || request.ComplianceScores.Count == 0))
        {
            return Result.Failure(
                ZenoHrErrorCode.ValidationFailed,
                "At least one audit entry or compliance score is required.");
        }

        return Result.Success();
    }

    private static string GenerateReference(EvidencePackRequest request)
    {
        var year = request.PeriodStart.Year.ToString(CultureInfo.InvariantCulture);
        var seq = Math.Abs(
            HashCode.Combine(request.TenantId, request.PeriodStart, request.PeriodEnd, request.PackType)
        ) % 10000;
        return string.Format(CultureInfo.InvariantCulture, "EVP-{0}-{1:D4}", year, seq);
    }

    private static void ComposeCoverPage(ColumnDescriptor column, EvidencePackRequest request, string reference)
    {
        column.Item().PaddingTop(120).AlignCenter().Column(inner =>
        {
            inner.Item().AlignCenter().Text(request.CompanyName)
                .FontSize(24).Bold().FontColor(Colors.Blue.Darken3);

            inner.Item().PaddingTop(30).AlignCenter().Text("Evidence Pack")
                .FontSize(20).SemiBold();

            inner.Item().PaddingTop(10).AlignCenter().Text(FormatPackType(request.PackType))
                .FontSize(14).FontColor(Colors.Grey.Darken1);

            inner.Item().PaddingTop(30).AlignCenter().Text(string.Format(
                CultureInfo.InvariantCulture,
                "Period: {0} to {1}",
                request.PeriodStart.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture),
                request.PeriodEnd.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture)))
                .FontSize(11);

            inner.Item().PaddingTop(10).AlignCenter().Text(string.Format(
                CultureInfo.InvariantCulture,
                "Generated by: {0}",
                request.GeneratedBy))
                .FontSize(10).FontColor(Colors.Grey.Darken1);

            inner.Item().PaddingTop(5).AlignCenter().Text(string.Format(
                CultureInfo.InvariantCulture,
                "Date: {0}",
                DateTimeOffset.UtcNow.ToString("dd MMMM yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture)))
                .FontSize(10).FontColor(Colors.Grey.Darken1);

            inner.Item().PaddingTop(10).AlignCenter().Text(string.Format(
                CultureInfo.InvariantCulture,
                "Reference: {0}",
                reference))
                .FontSize(10).Bold();
        });
    }

    private static void ComposeTableOfContents(ColumnDescriptor column)
    {
        column.Item().Text("Table of Contents").FontSize(16).Bold();
        column.Item().PaddingTop(10).Column(inner =>
        {
            inner.Item().Text("1. Executive Summary").FontSize(11);
            inner.Item().PaddingTop(4).Text("2. Compliance Scores").FontSize(11);
            inner.Item().PaddingTop(4).Text("3. Audit Trail").FontSize(11);
            inner.Item().PaddingTop(4).Text("4. Supporting Documents Index").FontSize(11);
        });
    }

    private static void ComposeExecutiveSummary(ColumnDescriptor column, EvidencePackRequest request)
    {
        column.Item().Text("1. Executive Summary").FontSize(16).Bold();
        column.Item().PaddingTop(10).Column(inner =>
        {
            inner.Item().Text(string.Format(
                CultureInfo.InvariantCulture,
                "This evidence pack was generated for {0} covering the period {1} to {2}.",
                request.CompanyName,
                request.PeriodStart.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture),
                request.PeriodEnd.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture)))
                .FontSize(10);

            inner.Item().PaddingTop(8).Text(string.Format(
                CultureInfo.InvariantCulture,
                "Pack Type: {0}",
                FormatPackType(request.PackType)))
                .FontSize(10);

            inner.Item().PaddingTop(4).Text(string.Format(
                CultureInfo.InvariantCulture,
                "Total Audit Entries: {0}",
                request.AuditEntries.Count))
                .FontSize(10);

            inner.Item().PaddingTop(4).Text(string.Format(
                CultureInfo.InvariantCulture,
                "Total Compliance Domains Assessed: {0}",
                request.ComplianceScores.Count))
                .FontSize(10);

            inner.Item().PaddingTop(4).Text(string.Format(
                CultureInfo.InvariantCulture,
                "Supporting Documents: {0}",
                request.SupportingDocuments.Count))
                .FontSize(10);

            if (request.ComplianceScores.Count > 0)
            {
                var avgScore = request.ComplianceScores.Average(s => s.ScorePercentage);
                inner.Item().PaddingTop(4).Text(string.Format(
                    CultureInfo.InvariantCulture,
                    "Average Compliance Score: {0:F1}%",
                    avgScore))
                    .FontSize(10).Bold();
            }
        });
    }

    private static void ComposeComplianceScores(ColumnDescriptor column, EvidencePackRequest request)
    {
        column.Item().Text("2. Compliance Scores").FontSize(16).Bold();

        if (request.ComplianceScores.Count == 0)
        {
            column.Item().PaddingTop(10).Text("No compliance scores included in this evidence pack.")
                .FontSize(10).Italic();
            return;
        }

        column.Item().PaddingTop(10).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2);   // Domain
                columns.RelativeColumn(1);   // Score
                columns.RelativeColumn(1);   // Status
                columns.RelativeColumn(3);   // Findings
            });

            table.Header(header =>
            {
                header.Cell().Background(Colors.Blue.Darken3).Padding(5)
                    .Text("Domain").FontSize(9).Bold().FontColor(Colors.White);
                header.Cell().Background(Colors.Blue.Darken3).Padding(5)
                    .Text("Score").FontSize(9).Bold().FontColor(Colors.White);
                header.Cell().Background(Colors.Blue.Darken3).Padding(5)
                    .Text("Status").FontSize(9).Bold().FontColor(Colors.White);
                header.Cell().Background(Colors.Blue.Darken3).Padding(5)
                    .Text("Findings").FontSize(9).Bold().FontColor(Colors.White);
            });

            foreach (var score in request.ComplianceScores)
            {
                var bgColor = Colors.White;
                table.Cell().Background(bgColor).Padding(4)
                    .Text(score.Domain).FontSize(9);
                table.Cell().Background(bgColor).Padding(4)
                    .Text(score.ScorePercentage.ToString("F1", CultureInfo.InvariantCulture) + "%").FontSize(9);
                table.Cell().Background(bgColor).Padding(4)
                    .Text(score.Status).FontSize(9);
                table.Cell().Background(bgColor).Padding(4)
                    .Text(score.Findings.Length > 0
                        ? string.Join("; ", score.Findings)
                        : "None")
                    .FontSize(9);
            }
        });
    }

    private static void ComposeAuditTrail(ColumnDescriptor column, EvidencePackRequest request)
    {
        column.Item().Text("3. Audit Trail").FontSize(16).Bold();

        if (request.AuditEntries.Count == 0)
        {
            column.Item().PaddingTop(10).Text("No audit entries included in this evidence pack.")
                .FontSize(10).Italic();
            return;
        }

        column.Item().PaddingTop(10).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(25);  // #
                columns.RelativeColumn(2);   // Timestamp
                columns.RelativeColumn(1.5f); // Action
                columns.RelativeColumn(1.5f); // PerformedBy
                columns.RelativeColumn(3);   // Description
                columns.RelativeColumn(1);   // Hash
            });

            table.Header(header =>
            {
                header.Cell().Background(Colors.Blue.Darken3).Padding(4)
                    .Text("#").FontSize(8).Bold().FontColor(Colors.White);
                header.Cell().Background(Colors.Blue.Darken3).Padding(4)
                    .Text("Timestamp").FontSize(8).Bold().FontColor(Colors.White);
                header.Cell().Background(Colors.Blue.Darken3).Padding(4)
                    .Text("Action").FontSize(8).Bold().FontColor(Colors.White);
                header.Cell().Background(Colors.Blue.Darken3).Padding(4)
                    .Text("Performed By").FontSize(8).Bold().FontColor(Colors.White);
                header.Cell().Background(Colors.Blue.Darken3).Padding(4)
                    .Text("Description").FontSize(8).Bold().FontColor(Colors.White);
                header.Cell().Background(Colors.Blue.Darken3).Padding(4)
                    .Text("Hash").FontSize(8).Bold().FontColor(Colors.White);
            });

            var index = 1;
            foreach (var entry in request.AuditEntries)
            {
                var bgColor = index % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;

                table.Cell().Background(bgColor).Padding(3)
                    .Text(index.ToString(CultureInfo.InvariantCulture)).FontSize(8);
                table.Cell().Background(bgColor).Padding(3)
                    .Text(entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).FontSize(8);
                table.Cell().Background(bgColor).Padding(3)
                    .Text(entry.Action).FontSize(8);
                table.Cell().Background(bgColor).Padding(3)
                    .Text(entry.PerformedBy).FontSize(8);
                table.Cell().Background(bgColor).Padding(3)
                    .Text(entry.Description).FontSize(8);
                table.Cell().Background(bgColor).Padding(3)
                    .Text(entry.EventHash.Length >= 8
                        ? entry.EventHash[..8]
                        : entry.EventHash)
                    .FontSize(8).FontFamily("Courier New");

                index++;
            }
        });
    }

    private static void ComposeSupportingDocuments(ColumnDescriptor column, EvidencePackRequest request)
    {
        column.Item().Text("4. Supporting Documents Index").FontSize(16).Bold();

        if (request.SupportingDocuments.Count == 0)
        {
            column.Item().PaddingTop(10).Text("No supporting documents included in this evidence pack.")
                .FontSize(10).Italic();
            return;
        }

        column.Item().PaddingTop(10).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(25);  // #
                columns.RelativeColumn(3);   // Title
                columns.RelativeColumn(1.5f); // Type
                columns.RelativeColumn(1.5f); // Date
                columns.RelativeColumn(2);   // Reference
            });

            table.Header(header =>
            {
                header.Cell().Background(Colors.Blue.Darken3).Padding(5)
                    .Text("#").FontSize(9).Bold().FontColor(Colors.White);
                header.Cell().Background(Colors.Blue.Darken3).Padding(5)
                    .Text("Title").FontSize(9).Bold().FontColor(Colors.White);
                header.Cell().Background(Colors.Blue.Darken3).Padding(5)
                    .Text("Type").FontSize(9).Bold().FontColor(Colors.White);
                header.Cell().Background(Colors.Blue.Darken3).Padding(5)
                    .Text("Date").FontSize(9).Bold().FontColor(Colors.White);
                header.Cell().Background(Colors.Blue.Darken3).Padding(5)
                    .Text("Reference").FontSize(9).Bold().FontColor(Colors.White);
            });

            var index = 1;
            foreach (var doc in request.SupportingDocuments)
            {
                var bgColor = index % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;

                table.Cell().Background(bgColor).Padding(4)
                    .Text(index.ToString(CultureInfo.InvariantCulture)).FontSize(9);
                table.Cell().Background(bgColor).Padding(4)
                    .Text(doc.DocumentTitle).FontSize(9);
                table.Cell().Background(bgColor).Padding(4)
                    .Text(doc.DocumentType).FontSize(9);
                table.Cell().Background(bgColor).Padding(4)
                    .Text(doc.DocumentDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)).FontSize(9);
                table.Cell().Background(bgColor).Padding(4)
                    .Text(doc.Reference).FontSize(9);

                index++;
            }
        });
    }

    private static string FormatPackType(string packType)
    {
        return packType switch
        {
            "SARS_AUDIT" => "SARS Audit",
            "POPIA_REVIEW" => "POPIA Review",
            "INTERNAL_AUDIT" => "Internal Audit",
            "BCEA_INSPECTION" => "BCEA Inspection",
            _ => packType
        };
    }
}
