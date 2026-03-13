// TC-SEC-040: Field-level PII encryption — VUL-019 remediation tests.
// CTL-POPIA-006: Validates encrypt/decrypt round-trip, prefix detection, IV randomness, and Unicode support.

using System.Security.Cryptography;
using FluentAssertions;
using ZenoHR.Infrastructure.Security;

namespace ZenoHR.Domain.Tests.Security;

/// <summary>
/// Unit tests for <see cref="AesFieldEncryptionService"/> and <see cref="InMemoryFieldEncryptionService"/>.
/// Verifies field-level PII encryption behaviour for national_id, tax_reference, bank_account_ref.
/// VUL-019 remediation — TC-SEC-040
/// </summary>
public sealed class FieldEncryptionServiceTests
{
    private static byte[] GenerateTestKey()
    {
        var key = new byte[EncryptionConstants.KeySizeBytes];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    private static AesFieldEncryptionService CreateProductionService(byte[]? key = null)
        => new(key ?? GenerateTestKey());

    private static InMemoryFieldEncryptionService CreateInMemoryService(byte[]? key = null)
        => new(key ?? GenerateTestKey());

    // --- AesFieldEncryptionService tests ---

    // TC-SEC-040-001: Encrypt then decrypt returns original plaintext
    [Theory]
    [InlineData("9001015009087")]           // SA national ID
    [InlineData("0123456789")]              // Tax reference
    [InlineData("1234567890/00")]           // Bank account ref
    [InlineData("simple text")]
    public void Encrypt_ThenDecrypt_ReturnsOriginalValue(string plaintext)
    {
        // Arrange
        var sut = CreateProductionService();

        // Act
        var encrypted = sut.Encrypt(plaintext);
        var decrypted = sut.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(plaintext,
            because: "decrypt must recover the exact original plaintext");
    }

    // TC-SEC-040-002: Encrypted value starts with ENC: prefix
    [Fact]
    public void Encrypt_Result_StartsWithEncPrefix()
    {
        // Arrange
        var sut = CreateProductionService();

        // Act
        var encrypted = sut.Encrypt("9001015009087");

        // Assert
        encrypted.Should().StartWith(EncryptionConstants.Prefix,
            because: "all encrypted values must be prefixed with 'ENC:' for detection");
    }

    // TC-SEC-040-003: IsEncrypted returns true for encrypted value
    [Fact]
    public void IsEncrypted_EncryptedValue_ReturnsTrue()
    {
        // Arrange
        var sut = CreateProductionService();
        var encrypted = sut.Encrypt("tax-ref-12345");

        // Act
        var result = sut.IsEncrypted(encrypted);

        // Assert
        result.Should().BeTrue(
            because: "a value produced by Encrypt must be detected as encrypted");
    }

    // TC-SEC-040-004: IsEncrypted returns false for plain text value
    [Theory]
    [InlineData("9001015009087")]
    [InlineData("plain text")]
    [InlineData("")]
    [InlineData("some-base64-but-no-prefix")]
    public void IsEncrypted_PlainValue_ReturnsFalse(string plainValue)
    {
        // Arrange
        var sut = CreateProductionService();

        // Act
        var result = sut.IsEncrypted(plainValue);

        // Assert
        result.Should().BeFalse(
            because: "values without the 'ENC:' prefix are not encrypted");
    }

    // TC-SEC-040-005: Different encryptions of same value produce different ciphertext (random IV)
    [Fact]
    public void Encrypt_SameValueTwice_ProducesDifferentCiphertext()
    {
        // Arrange
        var sut = CreateProductionService();
        const string plaintext = "9001015009087";

        // Act
        var encrypted1 = sut.Encrypt(plaintext);
        var encrypted2 = sut.Encrypt(plaintext);

        // Assert
        encrypted1.Should().NotBe(encrypted2,
            because: "each encryption must use a random IV, producing unique ciphertext");

        // Both must still decrypt to the same value.
        sut.Decrypt(encrypted1).Should().Be(plaintext);
        sut.Decrypt(encrypted2).Should().Be(plaintext);
    }

    // TC-SEC-040-006: Empty string encryption and decryption works
    [Fact]
    public void Encrypt_EmptyString_RoundTripsCorrectly()
    {
        // Arrange
        var sut = CreateProductionService();

        // Act
        var encrypted = sut.Encrypt(string.Empty);
        var decrypted = sut.Decrypt(encrypted);

        // Assert
        decrypted.Should().BeEmpty(
            because: "empty string must survive encrypt/decrypt round-trip");
        encrypted.Should().StartWith(EncryptionConstants.Prefix);
    }

    // TC-SEC-040-007: Unicode text encryption works (SA names with diacritics)
    [Theory]
    [InlineData("Thulani Ndlovu")]
    [InlineData("Nomalanga Dlamini-Mkhize")]
    [InlineData("Fran\u00e7ois du Plessis")]        // French diacritic
    [InlineData("S\u00f8ren M\u00f8ller")]           // Nordic characters
    [InlineData("\u4e2d\u6587\u6d4b\u8bd5")]         // Chinese characters
    [InlineData("\ud83d\ude00 emoji test")]           // Emoji
    public void Encrypt_UnicodeText_RoundTripsCorrectly(string unicodeText)
    {
        // Arrange
        var sut = CreateProductionService();

        // Act
        var encrypted = sut.Encrypt(unicodeText);
        var decrypted = sut.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(unicodeText,
            because: "Unicode text including diacritics must survive encrypt/decrypt round-trip");
    }

    // TC-SEC-040-008: Decrypt with wrong key fails (HMAC verification)
    [Fact]
    public void Decrypt_WithWrongKey_ThrowsCryptographicException()
    {
        // Arrange
        var key1 = GenerateTestKey();
        var key2 = GenerateTestKey();
        var encryptor = CreateProductionService(key1);
        var wrongDecryptor = CreateProductionService(key2);

        var encrypted = encryptor.Encrypt("sensitive-data");

        // Act
        var act = () => wrongDecryptor.Decrypt(encrypted);

        // Assert
        act.Should().Throw<CryptographicException>(
            because: "decryption with a wrong key must fail HMAC verification");
    }

    // TC-SEC-040-009: Decrypt without ENC: prefix throws ArgumentException
    [Fact]
    public void Decrypt_WithoutPrefix_ThrowsArgumentException()
    {
        // Arrange
        var sut = CreateProductionService();

        // Act
        var act = () => sut.Decrypt("not-encrypted-data");

        // Assert
        act.Should().Throw<ArgumentException>(
            because: "decrypting a value without the 'ENC:' prefix is invalid");
    }

    // TC-SEC-040-010: Constructor rejects key with wrong size
    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(64)]
    public void Constructor_WrongKeySize_ThrowsArgumentException(int keySize)
    {
        // Arrange
        var wrongKey = new byte[keySize];

        // Act
        var act = () => new AesFieldEncryptionService(wrongKey);

        // Assert
        act.Should().Throw<ArgumentException>(
            because: "only 32-byte (AES-256) keys are accepted");
    }

    // TC-SEC-040-011: Tampered ciphertext fails HMAC verification
    [Fact]
    public void Decrypt_TamperedCiphertext_ThrowsCryptographicException()
    {
        // Arrange
        var sut = CreateProductionService();
        var encrypted = sut.Encrypt("sensitive-national-id");

        // Tamper with the Base64 payload (flip a character in the ciphertext portion).
        var base64Part = encrypted.Substring(EncryptionConstants.Prefix.Length);
        var rawBytes = Convert.FromBase64String(base64Part);
        rawBytes[EncryptionConstants.IvSizeBytes + 1] ^= 0xFF; // Flip a byte in ciphertext.
        var tampered = EncryptionConstants.Prefix + Convert.ToBase64String(rawBytes);

        // Act
        var act = () => sut.Decrypt(tampered);

        // Assert
        act.Should().Throw<CryptographicException>(
            because: "tampered ciphertext must fail HMAC verification");
    }

    // --- InMemoryFieldEncryptionService tests ---

    // TC-SEC-040-012: InMemory service encrypt/decrypt round-trip
    [Fact]
    public void InMemory_EncryptDecrypt_RoundTrips()
    {
        // Arrange
        var sut = CreateInMemoryService();
        const string plaintext = "9001015009087";

        // Act
        var encrypted = sut.Encrypt(plaintext);
        var decrypted = sut.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(plaintext,
            because: "InMemory service must also correctly round-trip encrypt/decrypt");
        encrypted.Should().StartWith(EncryptionConstants.Prefix);
    }

    // TC-SEC-040-013: InMemory service uses random IV (different ciphertext each time)
    [Fact]
    public void InMemory_SameValueTwice_ProducesDifferentCiphertext()
    {
        // Arrange
        var sut = CreateInMemoryService();
        const string plaintext = "bank-account-ref";

        // Act
        var encrypted1 = sut.Encrypt(plaintext);
        var encrypted2 = sut.Encrypt(plaintext);

        // Assert
        encrypted1.Should().NotBe(encrypted2,
            because: "even the dev/test service must use random IVs");
    }

    // TC-SEC-040-014: InMemory service handles Unicode
    [Fact]
    public void InMemory_UnicodeText_RoundTrips()
    {
        // Arrange
        var sut = CreateInMemoryService();
        const string text = "Fran\u00e7ois du Plessis";

        // Act
        var encrypted = sut.Encrypt(text);
        var decrypted = sut.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(text);
    }

    // TC-SEC-040-015: InMemory decrypt with wrong key fails (HMAC verification)
    [Fact]
    public void InMemory_DecryptWithWrongKey_Throws()
    {
        // Arrange
        var encryptor = CreateInMemoryService(GenerateTestKey());
        var wrongDecryptor = CreateInMemoryService(GenerateTestKey());
        var encrypted = encryptor.Encrypt("secret");

        // Act
        var act = () => wrongDecryptor.Decrypt(encrypted);

        // Assert
        act.Should().Throw<CryptographicException>(
            because: "decryption with wrong key must fail HMAC verification in dev/test service");
    }

    // TC-SEC-040-017: InMemory service detects tampered ciphertext via HMAC
    [Fact]
    public void InMemory_TamperedCiphertext_ThrowsCryptographicException()
    {
        // Arrange
        var sut = CreateInMemoryService();
        var encrypted = sut.Encrypt("sensitive-national-id");

        // Tamper with the Base64 payload (flip a byte in the ciphertext portion).
        var base64Part = encrypted.Substring(EncryptionConstants.Prefix.Length);
        var rawBytes = Convert.FromBase64String(base64Part);
        rawBytes[EncryptionConstants.IvSizeBytes + 1] ^= 0xFF; // Flip a byte in ciphertext.
        var tampered = EncryptionConstants.Prefix + Convert.ToBase64String(rawBytes);

        // Act
        var act = () => sut.Decrypt(tampered);

        // Assert
        act.Should().Throw<CryptographicException>(
            because: "tampered ciphertext must fail HMAC verification in dev/test service");
    }

    // TC-SEC-040-016: Constants have expected values
    [Fact]
    public void EncryptionConstants_HaveExpectedValues()
    {
        EncryptionConstants.Prefix.Should().Be("ENC:");
        EncryptionConstants.KeySizeBytes.Should().Be(32);
        EncryptionConstants.IvSizeBytes.Should().Be(16);
        EncryptionConstants.HmacSizeBytes.Should().Be(32);
    }
}
