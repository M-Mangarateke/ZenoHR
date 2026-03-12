// CTL-POPIA-006: Field-level PII encryption constants.
// VUL-019: Envelope encryption support for national_id, tax_reference, bank_account_ref.

namespace ZenoHR.Infrastructure.Security;

/// <summary>
/// Constants used by field-level encryption services.
/// </summary>
public sealed class EncryptionConstants
{
    /// <summary>
    /// Prefix marker prepended to all encrypted values for detection.
    /// </summary>
    public const string Prefix = "ENC:";

    /// <summary>
    /// AES-256 key size in bytes.
    /// </summary>
    public const int KeySizeBytes = 32;

    /// <summary>
    /// AES IV (initialization vector) size in bytes.
    /// </summary>
    public const int IvSizeBytes = 16;

    /// <summary>
    /// HMAC-SHA256 tag size in bytes (used for authenticated encryption in CBC mode).
    /// </summary>
    public const int HmacSizeBytes = 32;
}
