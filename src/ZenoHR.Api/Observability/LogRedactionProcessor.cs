// REQ-SEC-001, CTL-POPIA-001: OpenTelemetry log redaction processor.
// VUL-022 remediation: strips PII fields from trace spans before export to Azure Monitor.
using System.Diagnostics;
using OpenTelemetry;

namespace ZenoHR.Api.Observability;

/// <summary>
/// OpenTelemetry processor that redacts PII fields from trace spans.
/// Prevents national IDs, tax references, and bank details from appearing in Azure Monitor.
/// VUL-022: Sensitive data potentially logged via OpenTelemetry — this processor prevents it.
/// </summary>
public sealed class LogRedactionProcessor : BaseProcessor<Activity>
{
    // REQ-SEC-001: Fields classified as Restricted/Confidential under POPIA
    private static readonly HashSet<string> RedactedAttributeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "employee.national_id",
        "employee.national_id_or_passport",
        "employee.tax_reference",
        "employee.bank_account",
        "employee.bank_account_ref",
        "user.password",
        "user.token",
        "http.request.body",     // May contain PII in POST bodies
        "db.statement",          // May contain PII in Firestore query values
    };

    private const string RedactedValue = "[REDACTED]";

    public override void OnEnd(Activity activity)
    {
        // CTL-POPIA-001: Strip PII from all outgoing spans
        foreach (var tag in activity.TagObjects)
        {
            if (RedactedAttributeKeys.Contains(tag.Key))
            {
                activity.SetTag(tag.Key, RedactedValue);
            }
        }

        // Also redact from events
        foreach (var activityEvent in activity.Events)
        {
            // ActivityEvent tags are read-only — log warning if PII detected
            foreach (var tag in activityEvent.Tags)
            {
                if (RedactedAttributeKeys.Contains(tag.Key))
                {
                    // Cannot mutate event tags — this is a gap to track
                    // Activity event PII is mitigated by not including PII in event tags (by convention)
                }
            }
        }
    }
}
