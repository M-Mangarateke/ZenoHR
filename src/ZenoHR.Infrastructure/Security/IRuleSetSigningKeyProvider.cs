// CTL-SARS-001: Signing key provider abstraction for statutory rule set verification.
// VUL-015: Statutory Rule Set Signature Verification — key management interface.

namespace ZenoHR.Infrastructure.Security;

/// <summary>
/// Provides the HMAC-SHA256 signing key used to verify statutory rule set signatures.
/// Production implementations should retrieve the key from Azure Key Vault.
/// CTL-SARS-001, VUL-015
/// </summary>
public interface IRuleSetSigningKeyProvider
{
    /// <summary>
    /// Returns the signing key bytes used for HMAC-SHA256 computation.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The signing key as a byte array.</returns>
    Task<byte[]> GetSigningKeyAsync(CancellationToken ct = default);
}
