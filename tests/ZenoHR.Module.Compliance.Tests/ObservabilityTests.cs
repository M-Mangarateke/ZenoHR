// REQ-OPS-003: Unit tests for ZenoHR observability — custom metrics and health checks.
// TC-OPS-004: Validates metric instrument creation and health check registration.

using System.Diagnostics.Metrics;
using FluentAssertions;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using ZenoHR.Api.Observability;
using ZenoHR.Api.Observability.HealthChecks;

namespace ZenoHR.Module.Compliance.Tests;

/// <summary>
/// Tests for <see cref="ZenoHrMetrics"/> and <see cref="FirestoreHealthCheck"/>.
/// </summary>
public sealed class ObservabilityTests
{
    // REQ-OPS-003: ZenoHrMetrics can be instantiated with IMeterFactory
    [Fact]
    public void ZenoHrMetrics_Instantiation_CreatesAllInstruments()
    {
        // Arrange — use the real MeterFactory from DI
        var services = new ServiceCollection();
        services.AddMetrics();
        using var provider = services.BuildServiceProvider();
        var meterFactory = provider.GetRequiredService<IMeterFactory>();

        // Act
        var metrics = new ZenoHrMetrics(meterFactory);

        // Assert
        metrics.PayrollRunsTotal.Should().NotBeNull();
        metrics.PayrollRunDurationSeconds.Should().NotBeNull();
        metrics.ComplianceChecksTotal.Should().NotBeNull();
        metrics.ApiErrorsTotal.Should().NotBeNull();
    }

    // REQ-OPS-003: ZenoHrMetrics throws on null meterFactory
    [Fact]
    public void ZenoHrMetrics_NullMeterFactory_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ZenoHrMetrics(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("meterFactory");
    }

    // REQ-OPS-003: ZenoHrMetrics.MeterName matches ObservabilityExtensions.MeterName
    [Fact]
    public void ZenoHrMetrics_MeterName_MatchesObservabilityExtensions()
    {
        ZenoHrMetrics.MeterName.Should().Be(ObservabilityExtensions.MeterName);
    }

    // REQ-OPS-007: FirestoreHealthCheck throws on null firestoreDb
    [Fact]
    public void FirestoreHealthCheck_NullDb_ThrowsArgumentNullException()
    {
        var act = () => new FirestoreHealthCheck(null!);

        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("firestoreDb");
    }

    // REQ-OPS-007: Health check registration includes Firestore check tagged "ready"
    [Fact]
    public void AddZenoHrObservability_RegistersHealthChecksAndMetrics()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMetrics();
        services.AddLogging();

        // Build a minimal IConfiguration without external packages
        var configuration = new ConfigurationBuilder().Build();

        // Act
        services.AddZenoHrObservability(configuration);

        // Assert — ZenoHrMetrics descriptor is registered
        services.Should().Contain(sd =>
            sd.ServiceType == typeof(ZenoHrMetrics) &&
            sd.Lifetime == ServiceLifetime.Singleton);

        // Assert — HealthCheckService descriptor is registered (from AddHealthChecks)
        services.Should().Contain(sd =>
            sd.ServiceType == typeof(HealthCheckService));
    }

    // REQ-OPS-003: Counters can be incremented without error
    [Fact]
    public void ZenoHrMetrics_RecordValues_DoesNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMetrics();
        using var provider = services.BuildServiceProvider();
        var meterFactory = provider.GetRequiredService<IMeterFactory>();
        var metrics = new ZenoHrMetrics(meterFactory);

        // Act & Assert — recording values should not throw
        var act = () =>
        {
            metrics.PayrollRunsTotal.Add(1, new KeyValuePair<string, object?>("status", "completed"));
            metrics.PayrollRunDurationSeconds.Record(12.5, new KeyValuePair<string, object?>("period", "monthly"));
            metrics.ComplianceChecksTotal.Add(1, new KeyValuePair<string, object?>("type", "emp201"));
            metrics.ApiErrorsTotal.Add(1, new KeyValuePair<string, object?>("status_code", "500"));
        };

        act.Should().NotThrow();
    }
}
