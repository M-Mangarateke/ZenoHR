// REQ-OPS-003: Firestore connectivity health check for readiness probes.
// REQ-OPS-007: Azure Container Apps readiness probe verifies Firestore is reachable.
// TC-OPS-004: Health check registration verified in integration tests.

using Google.Cloud.Firestore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ZenoHR.Api.Observability.HealthChecks;

/// <summary>
/// Health check that verifies Firestore connectivity by listing root collections.
/// Used by the <c>/health/ready</c> readiness probe to ensure the API can serve
/// requests that depend on Firestore.
/// </summary>
public sealed class FirestoreHealthCheck : IHealthCheck
{
    private readonly FirestoreDb _firestoreDb;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    // REQ-OPS-003: Injected via DI — FirestoreDb is registered as singleton in
    // ZenoHR.Infrastructure.Extensions.FirestoreExtensions.
    public FirestoreHealthCheck(FirestoreDb firestoreDb)
    {
        ArgumentNullException.ThrowIfNull(firestoreDb);
        _firestoreDb = firestoreDb;
    }

    /// <summary>
    /// Verifies Firestore connectivity by issuing a lightweight ListRootCollectionsAsync call.
    /// Returns <see cref="HealthCheckResult.Healthy"/> if the call succeeds within 5 seconds,
    /// <see cref="HealthCheckResult.Unhealthy"/> otherwise.
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Timeout);

            // Lightweight operation — lists root collection names without reading documents.
            var collections = _firestoreDb.ListRootCollectionsAsync();
            await foreach (var _ in collections.WithCancellation(cts.Token).ConfigureAwait(false))
            {
                // We only need to confirm connectivity — break after the first result.
                break;
            }

            return HealthCheckResult.Healthy("Firestore connectivity verified.");
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy(
                $"Firestore health check timed out after {Timeout.TotalSeconds}s.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Firestore health check failed.",
                exception: ex);
        }
    }
}
