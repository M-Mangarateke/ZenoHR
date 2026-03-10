// CTL-SARS-008: IRP5/IT3(a) XML serializer — SARS e@syFile format.
// Produces the XML submission file for SARS annual reconciliation (EMP501).
// Uses System.Xml.Linq (XDocument/XElement) — no XmlSerializer.
// All monetary amounts formatted as "0.00" with InvariantCulture (CA1305).
// Dates formatted as "yyyy-MM-dd".

using System.Globalization;
using System.Xml.Linq;

namespace ZenoHR.Infrastructure.Services.Filing.Irp5;

/// <summary>
/// Serializes a collection of <see cref="Irp5Certificate"/> records to the SARS
/// e@syFile IRP5/IT3(a) XML format for annual EMP501 reconciliation submission.
/// CTL-SARS-008: XML output is the official submission artifact.
/// </summary>
public sealed class Irp5XmlSerializer
{
    // CTL-SARS-008: InvariantCulture for all formatting — CA1305 compliance.
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private static readonly XNamespace SarsNs = "http://www.sars.gov.za/irp5";

    /// <summary>
    /// Serializes all <see cref="Irp5Certificate"/> records for a tenant/tax year
    /// into the SARS e@syFile IRP5/IT3(a) XML format.
    /// </summary>
    /// <param name="tenantId">Employer tenant ID (used in the Employer element).</param>
    /// <param name="taxYear">SA tax year label, e.g. "2026".</param>
    /// <param name="certificates">The certificates to serialize (may be empty — produces zero-count file).</param>
    /// <returns>UTF-8 XML string in SARS e@syFile format.</returns>
    public static string Serialize(
        string tenantId,
        string taxYear,
        IReadOnlyList<Irp5Certificate> certificates)
    {
        ArgumentNullException.ThrowIfNull(certificates);

        // CTL-SARS-008: Build XDocument using XDocument/XElement (System.Xml.Linq).
        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(SarsNs + "IRP5IT3aFile",
                new XAttribute("TaxYear", taxYear),
                new XAttribute("GeneratedAt",
                    DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", Invariant)),

                new XElement(SarsNs + "Employer",
                    new XElement(SarsNs + "TenantId", tenantId)),

                new XElement(SarsNs + "Certificates",
                    new XAttribute("Count",
                        certificates.Count.ToString(Invariant)),
                    certificates.Select(c => BuildCertificateElement(c)))));

        return doc.Declaration + Environment.NewLine + doc.ToString();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static XElement BuildCertificateElement(Irp5Certificate cert)
    {
        // CTL-SARS-008: Per-certificate XML element matching SARS e@syFile schema.
        return new XElement(SarsNs + "Certificate",
            new XAttribute("Type", cert.CertificateType),
            new XAttribute("Number", cert.CertificateNumber),

            new XElement(SarsNs + "Employee",
                new XElement(SarsNs + "Id", cert.EmployeeId),
                new XElement(SarsNs + "Name", cert.EmployeeName),
                new XElement(SarsNs + "IdNumber", cert.IdNumber),
                new XElement(SarsNs + "TaxRef", cert.TaxReferenceNumber)),

            new XElement(SarsNs + "Period",
                new XAttribute("Start",
                    cert.PeriodStart.ToString("yyyy-MM-dd", Invariant)),
                new XAttribute("End",
                    cert.PeriodEnd.ToString("yyyy-MM-dd", Invariant))),

            new XElement(SarsNs + "Income",
                new XElement(SarsNs + "Code3601", FormatAmount(cert.Code3601.Amount)),
                new XElement(SarsNs + "Code3605", FormatAmount(cert.Code3605.Amount)),
                new XElement(SarsNs + "Code3713", FormatAmount(cert.Code3713.Amount)),
                new XElement(SarsNs + "Code3697", FormatAmount(cert.Code3697.Amount)),
                new XElement(SarsNs + "Total",    FormatAmount(cert.TotalRemuneration.Amount))),

            new XElement(SarsNs + "Deductions",
                new XElement(SarsNs + "Code4001", FormatAmount(cert.Code4001.Amount)),
                new XElement(SarsNs + "Code4005", FormatAmount(cert.Code4005.Amount)),
                new XElement(SarsNs + "Code4474", FormatAmount(cert.Code4474.Amount)),
                new XElement(SarsNs + "Code4493", FormatAmount(cert.Code4493.Amount)),
                new XElement(SarsNs + "Code4497", FormatAmount(cert.Code4497.Amount)),
                new XElement(SarsNs + "Total",    FormatAmount(cert.TotalDeductions.Amount))),

            new XElement(SarsNs + "TaxableIncome", FormatAmount(cert.TaxableIncome.Amount)));
    }

    /// <summary>
    /// Formats a decimal monetary amount as "0.00" using InvariantCulture.
    /// CTL-SARS-008, CA1305: No locale-variable format calls on monetary values.
    /// </summary>
    private static string FormatAmount(decimal amount) =>
        amount.ToString("0.00", Invariant);
}
