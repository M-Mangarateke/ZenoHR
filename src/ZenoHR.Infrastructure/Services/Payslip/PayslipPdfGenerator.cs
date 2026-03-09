// REQ-HR-004, CTL-SARS-005: QuestPDF payslip generator — BCEA Section 33 compliant.
// CTL-BCEA-006: Payslip must be issued within 3 days of payment.
// All 9 sections match docs/design/mockups/16-payslip-template.html (pixel-by-pixel source of truth).
// QuestPDF community license (open source projects).

using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ZenoHR.Infrastructure.Services.Payslip;

/// <summary>
/// Generates a BCEA §33-compliant A4 PDF payslip using QuestPDF.
/// Implements all 9 sections from the canonical 16-payslip-template.html mockup.
/// </summary>
public sealed class PayslipPdfGenerator : IPayslipPdfGenerator
{
    // ── ZenoHR brand colors (from docs/design/design-tokens.md) ──────────────
    private static readonly Color BrandPrimary = Color.FromHex("#1d4777");
    private static readonly Color BrandAccent = Color.FromHex("#d4890e");
    private static readonly Color TextPrimary = Color.FromHex("#1a202c");
    private static readonly Color TextSecondary = Color.FromHex("#64748b");
    private static readonly Color TextMuted = Color.FromHex("#94a3b8");
    private static readonly Color BorderDefault = Color.FromHex("#e2e8f0");
    private static readonly Color BorderStrong = Color.FromHex("#cbd5e1");
    private static readonly Color BgSurface = Color.FromHex("#f8fafc");
    private static readonly Color BgRowAlt = Color.FromHex("#f1f5f9");

    static PayslipPdfGenerator()
    {
        // QuestPDF community license — valid for open source projects
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // ── REQ-HR-004, CTL-SARS-005: Generate ───────────────────────────────────

    /// <inheritdoc/>
    public byte[] Generate(PayslipData data)
    {
        // Payslip invariant: net_pay == gross_pay - total_deductions
        // (BCEA §33 — verified to the cent before PDF generation)
        var expectedNet = data.GrossSalary - data.TotalDeductions;
        if (Math.Abs(expectedNet - data.NetPay) > 0.01m)
            throw new InvalidOperationException(
                $"Payslip invariant violated: net_pay ({data.NetPay.ToString("F2", CultureInfo.InvariantCulture)}) " +
                $"!= gross ({data.GrossSalary.ToString("F2", CultureInfo.InvariantCulture)}) - deductions ({data.TotalDeductions.ToString("F2", CultureInfo.InvariantCulture)}). " +
                $"Difference: {Math.Abs(expectedNet - data.NetPay).ToString("F2", CultureInfo.InvariantCulture)}");

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginTop(0);
                page.MarginBottom(0);
                page.MarginLeft(0);
                page.MarginRight(0);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(9).FontColor(Colors.Black));

                page.Content().Column(col =>
                {
                    // 1. Header bar
                    col.Item().Element(c => ComposeHeader(c, data));

                    // Accent stripe
                    col.Item().Height(4).Background(BrandAccent);

                    // 2. Company + Employee info (two-column grid)
                    col.Item().Element(c => ComposeInfoSection(c, data));

                    // 3. Earnings table
                    col.Item().PaddingHorizontal(28).PaddingTop(16).Element(c => ComposeEarningsTable(c, data));

                    // 4. Deductions table
                    col.Item().PaddingHorizontal(28).PaddingTop(12).Element(c => ComposeDeductionsTable(c, data));

                    // 5. Net pay box
                    col.Item().Padding(12).PaddingHorizontal(28).Element(c => ComposeNetPayBox(c, data));

                    // 6. Employer contributions
                    col.Item().PaddingHorizontal(28).PaddingBottom(12).Element(c => ComposeEmployerContributions(c, data));

                    // 7. Leave balances
                    col.Item().PaddingHorizontal(28).PaddingBottom(12).Element(c => ComposeLeaveBalances(c, data));

                    // 8. Tax year-to-date
                    col.Item().PaddingHorizontal(28).PaddingBottom(12).Element(c => ComposeYearToDate(c, data));

                    // 9. Footer
                    col.Item().Element(c => ComposeFooter(c, data));
                });
            });
        });

        return document.GeneratePdf();
    }

    // ── Section 1: Header bar ─────────────────────────────────────────────────

    private static void ComposeHeader(IContainer container, PayslipData data)
    {
        // REQ-HR-004, CTL-SARS-005: BCEA §33 employer_name, work_address, pay_date
        container
            .Background(BrandPrimary)
            .Padding(16)
            .PaddingHorizontal(28)
            .Row(row =>
            {
                // Left: Company logo mark + name + address
                row.RelativeItem().Row(inner =>
                {
                    // Logo mark — "Z" in white box (no CornerRadius — not supported in this API version)
                    inner.AutoItem()
                        .Width(36).Height(36)
                        .Background(Color.FromHex("#FFFFFF20"))
                        .AlignMiddle()
                        .AlignCenter()
                        .Text("Z")
                        .FontSize(14).Bold().FontColor(Colors.White);

                    inner.ConstantItem(12); // gap

                    inner.RelativeItem().Column(nameCol =>
                    {
                        // BCEA §33: employer_name
                        nameCol.Item()
                            .Text(data.EmployerName)
                            .FontSize(13).Bold().FontColor(Colors.White);
                        // BCEA §33: work_address
                        nameCol.Item()
                            .Text(data.EmployerAddress)
                            .FontSize(9).FontColor(Color.FromHex("#FFFFFFB8"));
                    });
                });

                // Right: PAYSLIP title + period/date
                row.AutoItem().AlignRight().Column(right =>
                {
                    right.Item()
                        .Text("PAY ADVICE")
                        .FontSize(16).Bold().FontColor(Colors.White);
                    // BCEA §33: pay_date
                    right.Item()
                        .Text($"Pay Period: {data.PayPeriodLabel}  ·  Pay Date: {data.PaymentDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)}")
                        .FontSize(9).FontColor(Color.FromHex("#FFFFFFCC"));
                });
            });
    }

    // ── Section 2: Company + Employee info ────────────────────────────────────

    private static void ComposeInfoSection(IContainer container, PayslipData data)
    {
        // BCEA §33: employer details + employee_name, occupation, employment_date
        container
            .BorderBottom(1).BorderColor(BorderDefault)
            .Row(row =>
            {
                // Left column — Company details
                row.RelativeItem()
                    .BorderRight(1).BorderColor(BorderDefault)
                    .Padding(14).PaddingHorizontal(28)
                    .Column(col =>
                    {
                        col.Item().Text("Company Details")
                            .FontSize(8).Bold().FontColor(TextSecondary)
                            .LetterSpacing(0.07f);
                        col.Item().Height(8);
                        InfoRow(col, "Registration No.", data.EmployerRegistrationNumber, mono: true);
                        InfoRow(col, "Tax Reference No.", data.EmployerTaxReferenceNumber, mono: true);
                        InfoRow(col, "PAYE Reference", data.EmployerPayeReference, mono: true);
                        InfoRow(col, "UIF Reference", data.EmployerUifReferenceNumber, mono: true);
                        InfoRow(col, "Pay Frequency", data.PayFrequency);
                    });

                // Right column — Employee details
                row.RelativeItem()
                    .Padding(14).PaddingHorizontal(28)
                    .Column(col =>
                    {
                        col.Item().Text("Employee Details")
                            .FontSize(8).Bold().FontColor(TextSecondary)
                            .LetterSpacing(0.07f);
                        col.Item().Height(8);
                        // BCEA §33: employee_name
                        InfoRow(col, "Name", data.EmployeeFullName);
                        InfoRow(col, "Employee No.", data.EmployeeId, mono: true);
                        InfoRow(col, "ID Number", data.IdOrPassportMasked, mono: true);
                        InfoRow(col, "Department", data.Department);
                        // BCEA §33: occupation
                        InfoRow(col, "Job Title", data.JobTitle);
                        // BCEA §33: employment_date
                        InfoRow(col, "Hire Date", data.HireDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), mono: true);
                        InfoRow(col, "Tax Ref", data.TaxReferenceNumber, mono: true);
                    });
            });
    }

    private static void InfoRow(ColumnDescriptor col, string label, string value, bool mono = false)
    {
        col.Item().PaddingVertical(2).Row(row =>
        {
            row.RelativeItem().Text(label).FontSize(9).FontColor(TextSecondary);
            var txt = row.AutoItem().Text(value).FontSize(10).Bold().FontColor(TextPrimary);
            if (mono) txt.FontFamily("Courier New");
        });
    }

    // ── Section 3: Earnings table ─────────────────────────────────────────────

    private static void ComposeEarningsTable(IContainer container, PayslipData data)
    {
        // BCEA §33: ordinary_hours, ordinary_pay, overtime_hours, overtime_pay
        container.Column(col =>
        {
            // Table header
            col.Item()
                .BorderBottom(1.5f).BorderColor(BrandPrimary)
                .PaddingBottom(4)
                .Row(row =>
                {
                    row.RelativeItem().Text("Earnings").FontSize(8).Bold().FontColor(BrandPrimary).LetterSpacing(0.07f);
                    row.ConstantItem(80).AlignRight().Text("Hours").FontSize(8).Bold().FontColor(BrandPrimary).LetterSpacing(0.07f);
                    row.ConstantItem(80).AlignRight().Text("Rate").FontSize(8).Bold().FontColor(BrandPrimary).LetterSpacing(0.07f);
                    row.ConstantItem(90).AlignRight().Text("Amount").FontSize(8).Bold().FontColor(BrandPrimary).LetterSpacing(0.07f);
                });

            // Basic salary row — BCEA §33: ordinary_pay
            var basicRate = data.HoursOrdinary > 0 ? data.BasicSalary / data.HoursOrdinary : 0m;
            EarningsRow(col, "Basic Salary",
                data.HoursOrdinary > 0 ? FormatHours(data.HoursOrdinary) : "—",
                data.HoursOrdinary > 0 ? FormatMoney(basicRate) : "—",
                FormatMoney(data.BasicSalary));

            // Overtime — BCEA §33: overtime_hours, overtime_pay
            if (data.Overtime > 0 || data.HoursOvertime > 0)
            {
                var overtimeRate = data.HoursOvertime > 0 ? data.Overtime / data.HoursOvertime : 0m;
                EarningsRow(col, "Overtime (1.5x)",
                    data.HoursOvertime > 0 ? FormatHours(data.HoursOvertime) : "—",
                    data.HoursOvertime > 0 ? FormatMoney(overtimeRate) : "—",
                    FormatMoney(data.Overtime));
            }

            if (data.TravelAllowance > 0)
                EarningsRow(col, "Travel Allowance", "—", "—", FormatMoney(data.TravelAllowance));

            if (data.Bonus > 0)
                EarningsRow(col, "Bonus", "—", "—", FormatMoney(data.Bonus));

            if (data.OtherEarnings > 0)
                EarningsRow(col, data.OtherEarningsLabel ?? "Other Allowances", "—", "—", FormatMoney(data.OtherEarnings));

            // Gross subtotal row — BCEA §33: ordinary_pay + overtime_pay
            col.Item()
                .Background(BgSurface)
                .BorderTop(1).BorderColor(BorderStrong)
                .BorderBottom(1.5f).BorderColor(BorderStrong)
                .PaddingVertical(5)
                .Row(row =>
                {
                    row.RelativeItem().Text("Gross Earnings").FontSize(10).Bold().FontColor(TextPrimary);
                    var totalHours = data.HoursOrdinary + data.HoursOvertime;
                    row.ConstantItem(80).AlignRight().Text(totalHours > 0 ? FormatHours(totalHours) : "—")
                        .FontSize(10).Bold().FontFamily("Courier New");
                    row.ConstantItem(80).AlignRight().Text("").FontSize(10).Bold();
                    row.ConstantItem(90).AlignRight().Text(FormatMoney(data.GrossSalary))
                        .FontSize(10).Bold().FontFamily("Courier New").FontColor(TextPrimary);
                });

            // BCEA §33: actual_hours_worked_period note
            col.Item().PaddingTop(3)
                .Text($"Ordinary hours worked this period: {FormatHours(data.HoursOrdinary)}  ·  Overtime hours: {FormatHours(data.HoursOvertime)}")
                .FontSize(8).FontColor(TextMuted);
        });
    }

    private static void EarningsRow(ColumnDescriptor col, string description, string hours, string rate, string amount)
    {
        col.Item()
            .BorderBottom(1).BorderColor(BgRowAlt)
            .PaddingVertical(5)
            .Row(row =>
            {
                row.RelativeItem().Text(description).FontSize(9.5f).FontColor(Color.FromHex("#334155"));
                row.ConstantItem(80).AlignRight().Text(hours).FontSize(9).FontFamily("Courier New");
                row.ConstantItem(80).AlignRight().Text(rate).FontSize(9).FontFamily("Courier New");
                row.ConstantItem(90).AlignRight().Text(amount).FontSize(9).FontFamily("Courier New");
            });
    }

    // ── Section 4: Deductions table ───────────────────────────────────────────

    private static void ComposeDeductionsTable(IContainer container, PayslipData data)
    {
        // BCEA §33: deductions_itemised (each deduction must be itemised with reason)
        container.Column(col =>
        {
            // Table header
            col.Item()
                .BorderBottom(1.5f).BorderColor(BrandPrimary)
                .PaddingBottom(4)
                .Row(row =>
                {
                    row.RelativeItem().Text("Deductions").FontSize(8).Bold().FontColor(BrandPrimary).LetterSpacing(0.07f);
                    row.ConstantItem(130).AlignRight().Text("Rate / Reference").FontSize(8).Bold().FontColor(BrandPrimary).LetterSpacing(0.07f);
                    row.ConstantItem(90).AlignRight().Text("Amount").FontSize(8).Bold().FontColor(BrandPrimary).LetterSpacing(0.07f);
                });

            // PAYE (Income Tax)
            DeductionRow(col, "PAYE (Income Tax)", "Annual equiv.", FormatMoney(data.PayeAmount));

            // UIF employee — BCEA §33: deductions_itemised
            DeductionRow(col, "UIF (Employee)", "1% · cap R177.12", FormatMoney(data.UifEmployee));

            // Pension — BCEA §33: scheme_name
            if (data.PensionEmployee > 0)
                DeductionRow(col, "Pension Fund (Employee)", "7.5%", FormatMoney(data.PensionEmployee));

            // Medical aid employee
            if (data.MedicalAidEmployee > 0)
                DeductionRow(col, "Medical Aid (Employee)", "7.5%", FormatMoney(data.MedicalAidEmployee));

            // Other deductions
            if (data.OtherDeductions > 0)
                DeductionRow(col, data.OtherDeductionsLabel ?? "Other Deductions", "—", FormatMoney(data.OtherDeductions));

            // Total deductions subtotal
            col.Item()
                .Background(BgSurface)
                .BorderTop(1).BorderColor(BorderStrong)
                .BorderBottom(1.5f).BorderColor(BorderStrong)
                .PaddingVertical(5)
                .Row(row =>
                {
                    row.RelativeItem().Text("Total Deductions").FontSize(10).Bold().FontColor(TextPrimary);
                    row.ConstantItem(130).AlignRight().Text("").FontSize(10).Bold();
                    row.ConstantItem(90).AlignRight().Text(FormatMoney(data.TotalDeductions))
                        .FontSize(10).Bold().FontFamily("Courier New").FontColor(TextPrimary);
                });
        });
    }

    private static void DeductionRow(ColumnDescriptor col, string description, string reference, string amount)
    {
        col.Item()
            .BorderBottom(1).BorderColor(BgRowAlt)
            .PaddingVertical(5)
            .Row(row =>
            {
                row.RelativeItem().Text(description).FontSize(9.5f).FontColor(Color.FromHex("#334155"));
                row.ConstantItem(130).AlignRight().Text(reference).FontSize(9).FontFamily("Courier New");
                row.ConstantItem(90).AlignRight().Text(amount).FontSize(9).FontFamily("Courier New");
            });
    }

    // ── Section 5: Net pay box ────────────────────────────────────────────────

    private static void ComposeNetPayBox(IContainer container, PayslipData data)
    {
        // BCEA §33: net_pay — the actual amount paid to the employee
        container
            .Background(BrandPrimary)
            .Padding(16)
            .PaddingHorizontal(24)
            .Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("NET PAY").FontSize(11).Bold().FontColor(Colors.White);
                    col.Item().Text($"Credited via {data.PaymentMethod} on {data.PaymentDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)}")
                        .FontSize(8).FontColor(Color.FromHex("#FFFFFFA6"));
                });
                row.AutoItem().AlignMiddle()
                    .Text(FormatMoney(data.NetPay))
                    .FontSize(24).Bold().FontFamily("Courier New").FontColor(Colors.White);
            });
    }

    // ── Section 6: Employer contributions ────────────────────────────────────

    private static void ComposeEmployerContributions(IContainer container, PayslipData data)
    {
        container
            .Background(BgSurface)
            .Border(1).BorderColor(BorderDefault)
            .Padding(12).PaddingHorizontal(16)
            .Column(col =>
            {
                col.Item().Text("Employer Contributions — Not deducted from your salary")
                    .FontSize(8).Bold().FontColor(TextSecondary).LetterSpacing(0.07f);
                col.Item().Height(8);
                col.Item().Row(row =>
                {
                    // Left column of employer contributions
                    row.RelativeItem().Column(left =>
                    {
                        EmployerRow(left, "UIF (Employer) · 1%", FormatMoney(data.UifEmployer));
                        EmployerRow(left, "SDL · 1%", FormatMoney(data.Sdl));
                        if (data.EtiAmount > 0)
                            EmployerRow(left, "ETI Reduction (employer benefit)", FormatMoney(data.EtiAmount));
                    });
                    row.ConstantItem(24);
                    // Right column
                    row.RelativeItem().Column(right =>
                    {
                        EmployerRow(right, "Pension Fund (Employer) · 7.5%", FormatMoney(data.PensionEmployerContribution));
                        EmployerRow(right, "Medical Aid (Employer) · 7.5%", FormatMoney(data.MedicalAidEmployerContribution));
                    });
                });
                col.Item().Height(8);
                col.Item()
                    .BorderTop(1).BorderColor(BorderDefault)
                    .PaddingTop(6)
                    .Text("These costs are borne by your employer and do not affect your net pay.")
                    .FontSize(8).FontColor(TextMuted);
            });
    }

    private static void EmployerRow(ColumnDescriptor col, string label, string amount)
    {
        col.Item().PaddingVertical(2).Row(row =>
        {
            row.RelativeItem().Text(label).FontSize(9).FontColor(TextPrimary);
            row.AutoItem().Text(amount).FontSize(9).FontFamily("Courier New").FontColor(Color.FromHex("#334155"));
        });
    }

    // ── Section 7: Leave balances ─────────────────────────────────────────────

    private static void ComposeLeaveBalances(IContainer container, PayslipData data)
    {
        // BCEA §33(1)(f): leave balance disclosure
        container
            .Border(1).BorderColor(BorderDefault)
            .Padding(10).PaddingHorizontal(16)
            .Row(row =>
            {
                row.AutoItem().AlignMiddle().Column(label =>
                {
                    label.Item().Text("Leave").FontSize(8).Bold().FontColor(TextSecondary).LetterSpacing(0.07f);
                    label.Item().Text("Balances").FontSize(8).Bold().FontColor(TextSecondary).LetterSpacing(0.07f);
                });
                row.ConstantItem(24);
                row.AutoItem().PaddingRight(24).Column(c => LeaveItem(c, "Annual Leave",
                    $"{data.AnnualLeaveBalance.ToString("F0", CultureInfo.InvariantCulture)} / {data.AnnualLeaveEntitlement.ToString("F0", CultureInfo.InvariantCulture)} days"));
                row.AutoItem().PaddingRight(24).Column(c => LeaveItem(c, "Sick Leave (3-yr cycle)",
                    $"{data.SickLeaveBalance.ToString("F0", CultureInfo.InvariantCulture)} / {data.SickLeaveEntitlement.ToString("F0", CultureInfo.InvariantCulture)} days"));
                row.AutoItem().Column(c => LeaveItem(c, "Family Responsibility",
                    $"{data.FamilyResponsibilityBalance.ToString("F0", CultureInfo.InvariantCulture)} / {data.FamilyResponsibilityEntitlement.ToString("F0", CultureInfo.InvariantCulture)} days"));
            });
    }

    private static void LeaveItem(ColumnDescriptor col, string type, string value)
    {
        col.Item().Text(type).FontSize(8).FontColor(TextSecondary);
        col.Item().Text(value).FontSize(10).Bold().FontFamily("Courier New").FontColor(BrandPrimary);
    }

    // ── Section 8: Tax year-to-date ───────────────────────────────────────────

    private static void ComposeYearToDate(IContainer container, PayslipData data)
    {
        container.Column(col =>
        {
            // Table header
            col.Item()
                .BorderBottom(1.5f).BorderColor(BrandPrimary)
                .PaddingBottom(4)
                .Row(row =>
                {
                    row.RelativeItem().Text($"Tax Year to Date ({data.TaxYear})")
                        .FontSize(8).Bold().FontColor(BrandPrimary).LetterSpacing(0.07f);
                    row.ConstantItem(90).AlignRight().Text("This Period").FontSize(8).Bold().FontColor(BrandPrimary).LetterSpacing(0.07f);
                    row.ConstantItem(90).AlignRight().Text("Year to Date").FontSize(8).Bold().FontColor(BrandPrimary).LetterSpacing(0.07f);
                    row.ConstantItem(60).AlignRight().Text("IRP5 Code").FontSize(7).FontColor(TextMuted).LetterSpacing(0.07f);
                });

            YtdRow(col, "Gross Earnings", FormatMoney(data.GrossSalary), FormatMoney(data.YtdGross), "3601");
            YtdRow(col, "PAYE Withheld", FormatMoney(data.PayeAmount), FormatMoney(data.YtdPaye), "4102");
            YtdRow(col, "UIF (Employee)", FormatMoney(data.UifEmployee), FormatMoney(data.YtdUifEmployee), "4141");
            YtdRow(col, "Net Pay", FormatMoney(data.NetPay), FormatMoney(data.YtdGross - data.YtdPaye - data.YtdUifEmployee), "—");
        });
    }

    private static void YtdRow(ColumnDescriptor col, string label, string thisPeriod, string ytd, string irp5Code)
    {
        col.Item()
            .BorderBottom(1).BorderColor(BgRowAlt)
            .PaddingVertical(5)
            .Row(row =>
            {
                row.RelativeItem().Text(label).FontSize(9.5f).FontColor(Color.FromHex("#334155"));
                row.ConstantItem(90).AlignRight().Text(thisPeriod).FontSize(9).FontFamily("Courier New");
                row.ConstantItem(90).AlignRight().Text(ytd).FontSize(9).FontFamily("Courier New");
                row.ConstantItem(60).AlignRight().Text(irp5Code).FontSize(8).FontColor(TextMuted);
            });
    }

    // ── Section 9: Footer ─────────────────────────────────────────────────────

    private static void ComposeFooter(IContainer container, PayslipData data)
    {
        container
            .BorderTop(1).BorderColor(BorderDefault)
            .Background(BgSurface)
            .Padding(10).PaddingHorizontal(28)
            .Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("This payslip is computer generated and valid without signature.")
                        .FontSize(8).FontColor(TextSecondary);
                    col.Item().Text(
                        $"Issued in compliance with the Basic Conditions of Employment Act 75 of 1997, Section 33  ·  " +
                        $"Tax Year: {data.TaxYear}  ·  All values in South African Rand (ZAR)")
                        .FontSize(8).FontColor(TextSecondary);
                    col.Item().Text($"Generated: {data.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} UTC  ·  Run: {data.PayrollRunReference}  ·  Result ID: {data.PayrollResultId}")
                        .FontSize(7).FontColor(TextMuted);
                });
                row.AutoItem().AlignRight().Column(col =>
                {
                    col.Item().AlignRight().Text($"ZenoHR v1.0  ·  Payslip ID: PSL-{data.PayrollRunId}-{data.EmployeeId}")
                        .FontSize(8).FontColor(TextMuted);
                    col.Item().AlignRight().Text("Page 1 of 1")
                        .FontSize(8).FontColor(TextMuted);
                });
            });
    }

    // ── Formatting helpers ─────────────────────────────────────────────────────

    /// <summary>Format a ZAR monetary amount as "R #,##0.00" using invariant culture.</summary>
    private static string FormatMoney(decimal amount) =>
        $"R {amount.ToString("N2", CultureInfo.InvariantCulture)}";

    /// <summary>Format hours to 2 decimal places using invariant culture.</summary>
    private static string FormatHours(decimal hours) =>
        hours.ToString("N2", CultureInfo.InvariantCulture);
}
