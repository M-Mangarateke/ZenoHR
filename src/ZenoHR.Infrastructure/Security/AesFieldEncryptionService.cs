// CTL-POPIA-006: Field-level PII encryption — production implementation.
// VUL-019: AES-256-CBC + HMAC-SHA256 envelope encryption with Azure Key Vault DEK.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ZenoHR.Infrastructure.Security;

/// <summary>
/// Production field encryption service using AES-256-CBC with HMAC-SHA256 authentication.
/// Implements envelope encryption: the Data Encryption Key (DEK) is itself encrypted by a
/// Key Encryption Key (KEK) managed in Azure Key Vault.
/// <para>
/// Wire format: <c>ENC:Base64(IV ‖ Ciphertext ‖ HMAC)</c>
/// </para>
/// </summary>
public sealed class AesFieldEncryptionService : IFieldEncryptionService
{
    private readonly byte[] _encryptionKey;
    private readonly byte[] _hmacKey;

    /// <summary>
    /// Initializes the service with an unwrapped DEK.
    /// The <paramref name="dataEncryptionKey"/> must be exactly <see cref="EncryptionConstants.KeySizeBytes"/> bytes.
    /// A separate HMAC key is derived from the DEK using HKDF.
    /// </summary>
    public AesFieldEncryptionService(byte[] dataEncryptionKey)
    {
        ArgumentNullException.ThrowIfNull(dataEncryptionKey);

        if (dataEncryptionKey.Length != EncryptionConstants.KeySizeBytes)
        {
            throw new ArgumentException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Data encryption key must be exactly {0} bytes, but was {1} bytes.",
                    EncryptionConstants.KeySizeBytes,
                    dataEncryptionKey.Length),
                nameof(dataEncryptionKey));
        }

        _encryptionKey = (byte[])dataEncryptionKey.Clone();

        // Derive a separate HMAC key from the DEK using HKDF to avoid key reuse.
        _hmacKey = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            _encryptionKey,
            outputLength: EncryptionConstants.KeySizeBytes,
            info: "ZenoHR-HMAC-Key"u8.ToArray());
    }

    /// <inheritdoc />
    public string Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

        using var aes = Aes.Create();
        aes.KeySize = EncryptionConstants.KeySizeBytes * 8;
        aes.Key = _encryptionKey;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV(); // Random IV per encryption — never reused.

        byte[] ciphertext;
        using (var encryptor = aes.CreateEncryptor())
        {
            ciphertext = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);
        }

        // Compute HMAC over IV ‖ Ciphertext for authenticated encryption.
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

        // Verify HMAC before decryption (encrypt-then-MAC).
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
        aes.Key = _encryptionKey;
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
