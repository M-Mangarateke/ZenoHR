// REQ-HR-003, CTL-SARS-001: Firestore repository for payroll_runs/{id}/payroll_results subcollection.
// TASK-083: Per-employee payroll result persistence.
// Write-once after parent PayrollRun is Finalized (enforced by CreateDocumentAsync).
// Document ID = employee_id (one result per employee per run).

using System.Globalization;
using Google.Cloud.Firestore;
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Payroll.Entities;

namespace ZenoHR.Infrastructure.Firestore;

/// <summary>
/// Firestore repository for the <c>payroll_runs/{runId}/payroll_results</c> subcollection.
/// REQ-HR-003: Persists per-employee payroll calculation results.
/// CTL-SARS-001: Results are write-once — use <see cref="CreateAsync"/> only;
///               no update operations are exposed.
/// Document ID = employee_id — enforces one result per employee per run.
/// </summary>
public sealed class PayrollResultRepository
{
    private readonly FirestoreDb _db;

    public PayrollResultRepository(FirestoreDb db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    // ── Collection reference helper ───────────────────────────────────────────

    private CollectionReference ResultsCollection(string payrollRunId) =>
        _db.Collection("payroll_runs").Document(payrollRunId).Collection("payroll_results");

    // ── Reads ─────────────────────────────────────────────────────────────────

    /// <summary>Gets a single result by employee ID within a run.</summary>
    public async Task<Result<PayrollResult>> GetByEmployeeIdAsync(
        string payrollRunId, string employeeId, CancellationToken ct = default)
    {
        var snapshot = await ResultsCollection(payrollRunId).Document(employeeId).GetSnapshotAsync(ct);

        if (!snapshot.Exists)
            return Result<PayrollResult>.Failure(
                ZenoHrError.NotFound(ZenoHrErrorCode.PayrollResultNotFound,
                    $"payroll_results/{payrollRunId}", employeeId));

        return Result<PayrollResult>.Success(FromSnapshot(snapshot));
    }

    /// <summary>Lists all results for a payroll run.</summary>
    public async Task<IReadOnlyList<PayrollResult>> ListByRunAsync(
        string payrollRunId, CancellationToken ct = default)
    {
        var snapshot = await ResultsCollection(payrollRunId).GetSnapshotAsync(ct);
        return snapshot.Documents.Select(FromSnapshot).ToList();
    }

    // ── Write (create-only — write-once) ─────────────────────────────────────

    /// <summary>
    /// Creates a new payroll result document. Fails if the document already exists.
    /// CTL-SARS-001: PayrollResult is write-once — call this exactly once per employee per run.
    /// </summary>
    public async Task<Result> CreateAsync(PayrollResult result, CancellationToken ct = default)
    {
        var docRef = ResultsCollection(result.PayrollRunId).Document(result.EmployeeId);
        try
        {
            await docRef.CreateAsync(ToDocument(result), ct);
            return Result.Success();
        }
        catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.AlreadyExists)
        {
            return Result.Failure(
                ZenoHrErrorCode.FirestoreWriteConflict,
                $"PayrollResult for employee {result.EmployeeId} in run {result.PayrollRunId} already exists.");
        }
    }

    /// <summary>
    /// Writes all results for a run in a single batch.
    /// Fails atomically if any document already exists.
    /// </summary>
    public async Task<Result> CreateBatchAsync(
        IReadOnlyList<PayrollResult> results, CancellationToken ct = default)
    {
        if (results.Count == 0) return Result.Success();

        var batch = _db.StartBatch();
        foreach (var result in results)
        {
            var docRef = ResultsCollection(result.PayrollRunId).Document(result.EmployeeId);
            batch.Create(docRef, ToDocument(result));
        }

        try
        {
            await batch.CommitAsync(ct);
            return Result.Success();
        }
        catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.AlreadyExists)
        {
            return Result.Failure(
                ZenoHrErrorCode.FirestoreWriteConflict,
                "One or more PayrollResult documents already exist — batch creation failed.");
        }
    }

    // ── Hydration ─────────────────────────────────────────────────────────────

    private static PayrollResult FromSnapshot(DocumentSnapshot snapshot)
    {
        static IReadOnlyList<OtherLineItem> ReadLineItems(DocumentSnapshot s, string field)
        {
            if (!s.TryGetValue<List<object>>(field, out var raw) || raw is null) return [];
            var items = new List<OtherLineItem>(raw.Count);
            foreach (var obj in raw)
            {
                if (obj is not IDictionary<string, object> map) continue;
                var code = map.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
                var desc = map.TryGetValue("description", out var d) ? d?.ToString() ?? "" : "";
                var amt = map.TryGetValue("amount_zar", out var a) && a is string ars
                    ? MoneyZAR.FromFirestoreString(ars).Amount
                    : 0m;
                items.Add(new OtherLineItem(code, desc, amt));
            }
            return items;
        }

        static IReadOnlyList<string> ReadStringArray(DocumentSnapshot s, string field)
        {
            if (!s.TryGetValue<List<object>>(field, out var raw) || raw is null) return [];
            return raw.Select(o => o?.ToString() ?? "").ToList();
        }

        return PayrollResult.Reconstitute(
            employeeId: snapshot.Id,
            payrollRunId: snapshot.Reference.Parent.Parent!.Id,
            tenantId: snapshot.GetValue<string>("tenant_id"),
            basicSalary: ReadMoney(snapshot, "basic_salary_zar"),
            overtimePay: ReadMoney(snapshot, "overtime_pay_zar"),
            allowances: ReadMoney(snapshot, "allowances_zar"),
            grossPay: ReadMoney(snapshot, "gross_pay_zar"),
            paye: ReadMoney(snapshot, "paye_zar"),
            uifEmployee: ReadMoney(snapshot, "uif_employee_zar"),
            uifEmployer: ReadMoney(snapshot, "uif_employer_zar"),
            sdl: ReadMoney(snapshot, "sdl_zar"),
            pensionEmployee: ReadMoney(snapshot, "pension_employee_zar"),
            pensionEmployer: ReadMoney(snapshot, "pension_employer_zar"),
            medicalEmployee: ReadMoney(snapshot, "medical_employee_zar"),
            medicalEmployer: ReadMoney(snapshot, "medical_employer_zar"),
            etiAmount: ReadMoney(snapshot, "eti_amount_zar"),
            etiEligible: snapshot.TryGetValue<bool>("eti_eligible", out var eti) && eti,
            otherDeductions: ReadLineItems(snapshot, "other_deductions"),
            otherAdditions: ReadLineItems(snapshot, "other_additions"),
            deductionTotal: ReadMoney(snapshot, "deduction_total_zar"),
            additionTotal: ReadMoney(snapshot, "addition_total_zar"),
            netPay: ReadMoney(snapshot, "net_pay_zar"),
            hoursOrdinary: ReadDecimal(snapshot, "hours_ordinary"),
            hoursOvertime: ReadDecimal(snapshot, "hours_overtime"),
            taxTableVersion: snapshot.GetValue<string>("tax_table_version"),
            complianceFlags: ReadStringArray(snapshot, "compliance_flags"),
            calculationTimestamp: snapshot.GetValue<Timestamp>("calculation_timestamp").ToDateTimeOffset());
    }

    // ── Serialisation ─────────────────────────────────────────────────────────

    private static Dictionary<string, object?> ToDocument(PayrollResult r)
    {
        static List<Dictionary<string, object?>> SerialiseLineItems(IReadOnlyList<OtherLineItem> items) =>
            items.Select(li => new Dictionary<string, object?>
            {
                ["code"] = li.Code,
                ["description"] = li.Description,
                ["amount_zar"] = new MoneyZAR(li.AmountZar).ToFirestoreString(),
            }).ToList();

        return new()
        {
            ["employee_id"] = r.EmployeeId,
            ["payroll_run_id"] = r.PayrollRunId,
            ["tenant_id"] = r.TenantId,
            ["basic_salary_zar"] = r.BasicSalary.ToFirestoreString(),
            ["overtime_pay_zar"] = r.OvertimePay.ToFirestoreString(),
            ["allowances_zar"] = r.Allowances.ToFirestoreString(),
            ["gross_pay_zar"] = r.GrossPay.ToFirestoreString(),
            ["paye_zar"] = r.Paye.ToFirestoreString(),
            ["uif_employee_zar"] = r.UifEmployee.ToFirestoreString(),
            ["uif_employer_zar"] = r.UifEmployer.ToFirestoreString(),
            ["sdl_zar"] = r.Sdl.ToFirestoreString(),
            ["pension_employee_zar"] = r.PensionEmployee.ToFirestoreString(),
            ["pension_employer_zar"] = r.PensionEmployer.ToFirestoreString(),
            ["medical_employee_zar"] = r.MedicalEmployee.ToFirestoreString(),
            ["medical_employer_zar"] = r.MedicalEmployer.ToFirestoreString(),
            ["eti_amount_zar"] = r.EtiAmount.ToFirestoreString(),
            ["eti_eligible"] = r.EtiEligible,
            ["other_deductions"] = SerialiseLineItems(r.OtherDeductions),
            ["other_additions"] = SerialiseLineItems(r.OtherAdditions),
            ["deduction_total_zar"] = r.DeductionTotal.ToFirestoreString(),
            ["addition_total_zar"] = r.AdditionTotal.ToFirestoreString(),
            ["net_pay_zar"] = r.NetPay.ToFirestoreString(),
            ["hours_ordinary"] = r.HoursOrdinary.ToString(CultureInfo.InvariantCulture),
            ["hours_overtime"] = r.HoursOvertime.ToString(CultureInfo.InvariantCulture),
            ["tax_table_version"] = r.TaxTableVersion,
            ["compliance_flags"] = r.ComplianceFlags.ToList(),
            ["calculation_timestamp"] = Timestamp.FromDateTimeOffset(r.CalculationTimestamp),
            ["schema_version"] = r.SchemaVersion,
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static MoneyZAR ReadMoney(DocumentSnapshot snapshot, string field)
    {
        if (snapshot.TryGetValue<string>(field, out var str) && !string.IsNullOrWhiteSpace(str))
            return MoneyZAR.FromFirestoreString(str);
        return MoneyZAR.Zero;
    }

    private static decimal ReadDecimal(DocumentSnapshot snapshot, string field)
    {
        // Prefer string (precision-safe); fall back to double/long for legacy data
        if (snapshot.TryGetValue<string>(field, out var s) && decimal.TryParse(s, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        if (snapshot.TryGetValue<double>(field, out var d)) return (decimal)d;
        if (snapshot.TryGetValue<long>(field, out var l)) return l;
        return 0m;
    }
}
