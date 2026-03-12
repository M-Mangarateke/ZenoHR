// CTL-SARS-001: Static signing key provider for dev/test environments.
// VUL-015: Statutory Rule Set Signature Verification — non-production key source.

using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace ZenoHR.Infrastructure.Security;

/// <summary>
/// Reads the rule set signing key from <c>IConfiguration["RuleSetSigning:Key"]</c>.
/// Intended for development and test environments only. Production should use Azure Key Vault.
/// CTL-SARS-001, VUL-015
/// </summary>
public sealed class StaticSigningKeyProvider : IRuleSetSigningKeyProvider
{
    private readonly byte[] _key;

    public StaticSigningKeyProvider(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var keyHex = configuration["RuleSetSigning:Key"]
            ?? throw new InvalidOperationException(
                "RuleSetSigning:Key is not configured. " +
                "Set it in appsettings.Development.json or user secrets.");

        _key = ConvertHexToBytes(keyHex);

        if (_key.Length < 32)
            throw new InvalidOperationException(
                "RuleSetSigning:Key must be at least 256 bits (64 hex characters).");
    }

    /// <inheritdoc />
    public Task<byte[]> GetSigningKeyAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_key);
    }

    private static byte[] ConvertHexToBytes(string hex)
    {
        if (hex.Length % 2 != 0)
            throw new ArgumentException("Hex string must have an even number of characters.", nameof(hex));

        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = byte.Parse(
                hex.AsSpan(i * 2, 2),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture);
        }

        return bytes;
    }
}
