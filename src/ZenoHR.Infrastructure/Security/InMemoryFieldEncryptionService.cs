// CTL-POPIA-006: Field-level PII encryption — dev/test implementation.
// VUL-019: Simplified AES-CBC encryption with static key from configuration.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ZenoHR.Infrastructure.Security;

/// <summary>
/// Development and test field encryption service using AES-256-CBC with a static key
/// loaded from configuration. Not suitable for production — use <see cref="AesFieldEncryptionService"/>
/// with Azure Key Vault envelope encryption in production.
/// </summary>
public sealed class InMemoryFieldEncryptionService : IFieldEncryptionService
{
    private readonly byte[] _key;

    /// <summary>
    /// Initializes the service with a static key from configuration.
    /// The <paramref name="key"/> must be exactly <see cref="EncryptionConstants.KeySizeBytes"/> bytes.
    /// </summary>
    public InMemoryFieldEncryptionService(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key.Length != EncryptionConstants.KeySizeBytes)
        {
            throw new ArgumentException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Key must be exactly {0} bytes, but was {1} bytes.",
                    EncryptionConstants.KeySizeBytes,
                    key.Length),
                nameof(key));
        }

        _key = (byte[])key.Clone();
    }

    /// <inheritdoc />
    public string Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

        using var aes = Aes.Create();
        aes.KeySize = EncryptionConstants.KeySizeBytes * 8;
        aes.Key = _key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV(); // Random IV per encryption — never reused.

        byte[] ciphertext;
        using (var encryptor = aes.CreateEncryptor())
        {
            ciphertext = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);
        }

        // Wire format: IV ‖ Ciphertext (no HMAC — dev/test only)
        var result = new byte[EncryptionConstants.IvSizeBytes + ciphertext.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, EncryptionConstants.IvSizeBytes);
        Buffer.BlockCopy(ciphertext, 0, result, EncryptionConstants.IvSizeBytes, ciphertext.Length);

        return string.Concat(EncryptionConstants.Prefix, Convert.ToBase64String(result));
    }

    /// <inheritdoc />
    public string Decrypt(string ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);

        if (!IsEncrypted(ciphertext))
        {
            throw new ArgumentException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Value does not start with the expected '{0}' prefix.",
                    EncryptionConstants.Prefix),
                nameof(ciphertext));
        }

        var base64 = ciphertext.Substring(EncryptionConstants.Prefix.Length);
        var raw = Convert.FromBase64String(base64);

        if (raw.Length < EncryptionConstants.IvSizeBytes + 1)
        {
            throw new CryptographicException("Encrypted payload is too short to contain IV and ciphertext.");
        }

        var iv = new byte[EncryptionConstants.IvSizeBytes];
        Buffer.BlockCopy(raw, 0, iv, 0, EncryptionConstants.IvSizeBytes);

        var ciphertextLength = raw.Length - EncryptionConstants.IvSizeBytes;
        var encryptedData = new byte[ciphertextLength];
        Buffer.BlockCopy(raw, EncryptionConstants.IvSizeBytes, encryptedData, 0, ciphertextLength);

        using var aes = Aes.Create();
        aes.KeySize = EncryptionConstants.KeySizeBytes * 8;
        aes.Key = _key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        byte[] plaintextBytes;
        using (var decryptor = aes.CreateDecryptor())
        {
            plaintextBytes = decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
        }

        return Encoding.UTF8.GetString(plaintextBytes);
    }

    /// <inheritdoc />
    public bool IsEncrypted(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.StartsWith(EncryptionConstants.Prefix, StringComparison.Ordinal);
    }
}
