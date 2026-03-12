// CTL-POPIA-006: Field-level PII encryption interface.
// VUL-019: Envelope encryption for national_id, tax_reference, bank_account_ref.

namespace ZenoHR.Infrastructure.Security;

/// <summary>
/// Encrypts and decrypts individual PII field values using envelope encryption.
/// Encrypted values are prefixed with <see cref="EncryptionConstants.Prefix"/> for detection.
/// </summary>
public interface IFieldEncryptionService
{
    /// <summary>
    /// Encrypts the given plaintext value.
    /// Returns a Base64-encoded ciphertext prefixed with <see cref="EncryptionConstants.Prefix"/>.
    /// Each call produces a different ciphertext due to random IV generation.
    /// </summary>
    string Encrypt(string plaintext);

    /// <summary>
    /// Decrypts a previously encrypted value (must include the <see cref="EncryptionConstants.Prefix"/> prefix).
    /// Returns the original plaintext.
    /// </summary>
    string Decrypt(string ciphertext);

    /// <summary>
    /// Returns <c>true</c> if the value appears to be an encrypted field
    /// (starts with <see cref="EncryptionConstants.Prefix"/>).
    /// </summary>
    bool IsEncrypted(string value);
}
