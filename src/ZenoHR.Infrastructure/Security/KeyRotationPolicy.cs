// REQ-SEC-008, VUL-010: Key rotation configuration and policy.
// Defines rotation intervals and warning periods for cryptographic key management.
// Keys (encryption, signing) must be rotated every 180 days per security policy.

namespace ZenoHR.Infrastructure.Security;

/// <summary>
/// Defines the key rotation schedule and provides methods to determine
/// whether rotation is due or approaching. Applied to all cryptographic
/// keys used in ZenoHR (field encryption, rule-set signing, audit hashing).
/// </summary>
public sealed class KeyRotationPolicy
{
    /// <summary>Maximum number of days a key may remain active before rotation is required.</summary>
    public const int RotationIntervalDays = 180;

    /// <summary>Number of days before rotation deadline to begin warning.</summary>
    public const int WarningBeforeDays = 30;

    /// <summary>
    /// Determines whether key rotation is due (or overdue).
    /// </summary>
    /// <param name="lastRotated">The date the key was last rotated.</param>
    /// <param name="currentDate">The current date.</param>
    /// <returns><c>true</c> if the key has exceeded its rotation interval.</returns>
    public static bool IsRotationDue(DateTimeOffset lastRotated, DateTimeOffset currentDate)
    {
        var nextRotation = GetNextRotationDate(lastRotated);
        return currentDate >= nextRotation;
    }

    /// <summary>
    /// Calculates the next required rotation date based on the last rotation.
    /// </summary>
    /// <param name="lastRotated">The date the key was last rotated.</param>
    /// <returns>The date by which the key must be rotated.</returns>
    public static DateTimeOffset GetNextRotationDate(DateTimeOffset lastRotated) =>
        lastRotated.AddDays(RotationIntervalDays);

    /// <summary>
    /// Determines whether the key is within the warning period (approaching rotation deadline).
    /// Returns <c>true</c> when the key is within <see cref="WarningBeforeDays"/> days of its
    /// rotation deadline but has not yet exceeded it.
    /// </summary>
    /// <param name="lastRotated">The date the key was last rotated.</param>
    /// <param name="currentDate">The current date.</param>
    /// <returns><c>true</c> if within the warning window; <c>false</c> otherwise.</returns>
    public static bool IsInWarningPeriod(DateTimeOffset lastRotated, DateTimeOffset currentDate)
    {
        var nextRotation = GetNextRotationDate(lastRotated);
        var warningStart = nextRotation.AddDays(-WarningBeforeDays);
        return currentDate >= warningStart && currentDate < nextRotation;
    }
}
