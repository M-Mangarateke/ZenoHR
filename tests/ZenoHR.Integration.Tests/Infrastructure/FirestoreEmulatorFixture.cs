// REQ-OPS-003: Firestore emulator test fixture for integration tests
// All integration tests run against the local Firestore emulator — never production.
// Uses FirestoreDbBuilder with EmulatorDetection.EmulatorOrProduction so the SDK
// connects to the emulator without needing ADC credentials.

using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace ZenoHR.Integration.Tests.Infrastructure;

/// <summary>
/// xUnit collection fixture for Firestore emulator integration tests.
/// Sets the required environment variables, creates a shared FirestoreDb instance,
/// and provides a data-clearing method to isolate tests.
/// REQ-OPS-003: All integration tests use emulator, not production.
/// </summary>
public sealed class FirestoreEmulatorFixture : IAsyncLifetime
{
    // REQ-OPS-003: Configurable emulator settings via environment variables to avoid port conflicts.
    // Defaults match firebase.json but can be overridden in CI or local dev environments.
    private const string DefaultFirestorePort = "8080";
    private const string DefaultAuthPort = "9099";
    private const string DefaultProjectId = "zenohr-a7ccf";

    /// <summary>Firestore emulator project ID — configurable via FIREBASE_PROJECT_ID env var.</summary>
    public static string EmulatorProjectId { get; } =
        Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID") ?? DefaultProjectId;

    /// <summary>Firestore emulator host:port — configurable via FIRESTORE_EMULATOR_HOST env var.</summary>
    public static string EmulatorHost { get; } =
        Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST") ?? $"localhost:{DefaultFirestorePort}";

    /// <summary>Firebase Auth emulator host:port — configurable via FIREBASE_AUTH_EMULATOR_HOST env var.</summary>
    public static string AuthEmulatorHost { get; } =
        Environment.GetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST") ?? $"localhost:{DefaultAuthPort}";

    private static string EmulatorClearUrl =>
        $"http://{EmulatorHost}/emulator/v1/projects/{EmulatorProjectId}/databases/(default)/documents";

    private static readonly HttpClient _httpClient = new();

    public FirestoreDb Db { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Point the Google Cloud SDK to the local emulators
        Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", EmulatorHost);
        Environment.SetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST", AuthEmulatorHost);

        // Use FirestoreDbBuilder with EmulatorDetection so the SDK connects to the
        // emulator without requiring Application Default Credentials (ADC).
        // EmulatorOrProduction: reads FIRESTORE_EMULATOR_HOST env var → emulator path.
        Db = await new FirestoreDbBuilder
        {
            ProjectId = EmulatorProjectId,
            EmulatorDetection = EmulatorDetection.EmulatorOrProduction,
        }.BuildAsync();

        // Ensure emulator is reachable and clear any stale data from prior runs
        await ClearEmulatorDataAsync();
    }

    public Task DisposeAsync()
    {
        // Clear emulator data after the test collection finishes
        return ClearEmulatorDataAsync();
    }

    /// <summary>
    /// Deletes all documents from the emulator via the REST clear-data endpoint.
    /// Call this from test class constructors or between test groups to isolate state.
    /// </summary>
    public async Task ClearEmulatorDataAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, EmulatorClearUrl);
            var response = await _httpClient.SendAsync(request);
            // 200 OK = cleared, 404 = already empty — both are acceptable
            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException(
                    $"Failed to clear Firestore emulator data. Status: {response.StatusCode}. " +
                    "Ensure the Firebase emulator is running: firebase emulators:start --only firestore");
            }
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Cannot reach Firestore emulator at {EmulatorHost}. " +
                "Start it with: firebase emulators:start --only firestore,auth\n" +
                $"Original error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates a new FirestoreDb bound to the emulator for tests that need isolated instances.
    /// </summary>
    public static FirestoreDb CreateEmulatorDb()
    {
        Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", EmulatorHost);
        return new FirestoreDbBuilder
        {
            ProjectId = EmulatorProjectId,
            EmulatorDetection = EmulatorDetection.EmulatorOrProduction,
        }.Build();
    }
}
