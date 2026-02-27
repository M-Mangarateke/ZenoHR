// CTL-POPIA-001: DataClassification — field-level sensitivity classification per POPIA.
// Applied as an attribute on entity properties that hold personal/confidential information.
// The infrastructure layer uses this to enforce field-level masking and encryption.

namespace ZenoHR.Domain.Common;

/// <summary>
/// POPIA-aligned sensitivity classification for data fields.
/// Applied as <see cref="DataClassificationAttribute"/> on entity properties.
/// The infrastructure layer enforces masking, encryption, and access control per level.
/// </summary>
public enum DataClassification
{
    /// <summary>Classification not set — defaults to Internal.</summary>
    Unknown = 0,

    /// <summary>Non-sensitive data. Company name, job title, department name.</summary>
    Public = 1,

    /// <summary>Internal business data. Employee number, hire date, payslip amounts.</summary>
    Internal = 2,

    /// <summary>
    /// Personal Information per POPIA Section 1.
    /// ID/Passport number, date of birth, bank account, medical aid details,
    /// phone, personal email, marital status, gender, race (EE Act).
    /// Encrypted at rest in Firestore. Masked in API responses for non-privileged callers.
    /// </summary>
    Confidential = 3,

    /// <summary>
    /// Special Personal Information per POPIA Section 26.
    /// Disability details, health information, criminal record references.
    /// Strictest access control. Director and HRManager only. Audit-logged on every read.
    /// </summary>
    Restricted = 4,
}

/// <summary>
/// Marks an entity property with its POPIA data classification level.
/// Used by the infrastructure layer for field-level masking and encryption decisions.
/// </summary>
/// <example>
/// <code>
/// [DataClassification(DataClassification.Confidential)]
/// public string NationalIdNumber { get; init; } = string.Empty;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class DataClassificationAttribute(DataClassification level) : Attribute
{
    public DataClassification Level { get; } = level;
}
