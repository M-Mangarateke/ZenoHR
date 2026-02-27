// REQ-SEC-001: Firebase project configuration — bound from appsettings.json "Firebase" section.
// Do not store secrets here; use Azure Key Vault or .NET User Secrets (see TASK-036).

namespace ZenoHR.Infrastructure.Firestore;

/// <summary>
/// Configuration options for Firebase/Firestore.
/// Bound from the "Firebase" section in appsettings.json.
/// ProjectId is required — startup throws if absent.
/// </summary>
public sealed record FirebaseOptions
{
    public const string SectionName = "Firebase";

    /// <summary>
    /// Firebase project ID (e.g., "zenohr-prod").
    /// For local development, set via .NET User Secrets: Firebase:ProjectId.
    /// For Firestore emulator, also set FIRESTORE_EMULATOR_HOST=localhost:8080.
    /// </summary>
    public string ProjectId { get; init; } = string.Empty;
}
