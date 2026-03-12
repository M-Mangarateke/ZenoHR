// CTL-SARS-001: Signed statutory rule set — ensures PAYE/UIF/SDL/ETI tables are tamper-evident.
// VUL-015: Statutory Rule Set Signature Verification — signed artifact model.

namespace ZenoHR.Infrastructure.Security;

/// <summary>
/// Represents a statutory rule set with its HMAC-SHA256 signature.
/// The <see cref="Content"/> field holds the canonical JSON of the rule set,
/// and <see cref="Signature"/> holds the hex-encoded HMAC computed over that content.
/// CTL-SARS-001, VUL-015
/// </summary>
public sealed record SignedRuleSet(
    string RuleSetId,
    int TaxYear,
    string Content,
    string Signature,
    DateTimeOffset SignedAt,
    string SignedBy);
