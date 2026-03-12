// CTL-SARS-001: Verification result for statutory rule set signature checks.
// VUL-015: Statutory Rule Set Signature Verification — verification outcome.

namespace ZenoHR.Infrastructure.Security;

/// <summary>
/// Result of verifying a <see cref="SignedRuleSet"/> HMAC-SHA256 signature.
/// <see cref="IsValid"/> is true only when the computed signature matches the expected value.
/// CTL-SARS-001, VUL-015
/// </summary>
public sealed record RuleSetVerificationResult(
    bool IsValid,
    string RuleSetId,
    string? Reason);
