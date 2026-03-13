// CTL-POPIA-006: Field-level PII encryption — dev/test implementation.
// VUL-019: AES-256-CBC + HMAC-SHA256 encryption with static key from configuration.
// Audit fix: Added HMAC authentication to prevent padding oracle attacks (AES-CBC without HMAC).

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ZenoHR.Infrastructure.Security;

/// <summary>
/// Development and test field encryption service using AES-256-CBC with HMAC-SHA256 authentication
/// and a static key loaded from configuration. Not suitable for production — use
/// <see cref="AesFieldEncryptionService"/> with Azure Key Vault envelope encryption in production.
/// <para>
/// Wire format: <c>ENC:Base64(IV ‖ Ciphertext ‖ HMAC)</c>
/// </para>
/// </summary>
public sealed class InMemoryFieldEncryptionService : IFieldEncryptionService
{
    private readonly byte[] _key;
    private readonly byte[] _hmacKey;

    /// <summary>
    /// Initializes the service with a static key from configuration.
    /// The <paramref name="key"/> must be exactly <see cref="EncryptionConstants.KeySizeBytes"/> bytes.
    /// A separate HMAC key is derived from the key using HKDF to avoid key reuse.
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

        // Derive a separate HMAC key from the encryption key using HKDF to avoid key reuse.
        _hmacKey = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            _key,
            outputLength: EncryptionConstants.KeySizeBytes,
            info: "ZenoHR-InMemory-HMAC-Key"u8.ToArray());
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

        // Compute HMAC over IV ‖ Ciphertext for authenticated encryption (encrypt-then-MAC).
        byte[] hmac;
        using (var hmacAlg = new HMACSHA256(_hmacKey))
        {
            hmacAlg.TransformBlock(aes.IV, 0, aes.IV.Length, null, 0);
            hmacAlg.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
            hmac = hmacAlg.Hash!;
        }

        // Wire format: IV ‖ Ciphertext ‖ HMAC
        var result = new byte[EncryptionConstants.IvSizeBytes + ciphertext.Length + EncryptionConstants.HmacSizeBytes];
        Buffer.BlockCopy(aes.IV, 0, result, 0, EncryptionConstants.IvSizeBytes);
        Buffer.BlockCopy(ciphertext, 0, result, EncryptionConstants.IvSizeBytes, ciphertext.Length);
        Buffer.BlockCopy(hmac, 0, result, EncryptionConstants.IvSizeBytes + ciphertext.Length, EncryptionConstants.HmacSizeBytes);

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

        var minimumLength = EncryptionConstants.IvSizeBytes + 1 + EncryptionConstants.HmacSizeBytes;
        if (raw.Length < minimumLength)
        {
            throw new CryptographicException("Encrypted payload is too short to contain IV, ciphertext, and HMAC.");
        }

        // Extract IV, ciphertext, HMAC.
        var iv = new byte[EncryptionConstants.IvSizeBytes];
        Buffer.BlockCopy(raw, 0, iv, 0, EncryptionConstants.IvSizeBytes);

        var ciphertextLength = raw.Length - EncryptionConstants.IvSizeBytes - EncryptionConstants.HmacSizeBytes;
        var encryptedData = new byte[ciphertextLength];
        Buffer.BlockCopy(raw, EncryptionConstants.IvSizeBytes, encryptedData, 0, ciphertextLength);

        var storedHmac = new byte[EncryptionConstants.HmacSizeBytes];
        Buffer.BlockCopy(raw, EncryptionConstants.IvSizeBytes + ciphertextLength, storedHmac, 0, EncryptionConstants.HmacSizeBytes);

        // Verify HMAC before decryption (encrypt-then-MAC) to prevent padding oracle attacks.
        byte[] computedHmac;
        using (var hmacAlg = new HMACSHA256(_hmacKey))
        {
            hmacAlg.TransformBlock(iv, 0, iv.Length, null, 0);
            hmacAlg.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
            computedHmac = hmacAlg.Hash!;
        }

        if (!CryptographicOperations.FixedTimeEquals(storedHmac, computedHmac))
        {
            throw new CryptographicException("HMAC verification failed — ciphertext may have been tampered with.");
        }

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
