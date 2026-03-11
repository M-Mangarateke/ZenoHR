// REQ-OPS-003: Registration of all scheduled background services.

namespace ZenoHR.Api.BackgroundServices;

/// <summary>
/// Registers ZenoHR background services for nightly analytics, EMP201 reminders,
/// and ETI expiry alerts with the dependency injection container.
/// </summary>
public static class BackgroundServiceRegistration
{
    /// <summary>
    /// Adds all ZenoHR scheduled background services to the service collection.
    /// </summary>
    public static IServiceCollection AddZenoHrBackgroundServices(this IServiceCollection services)
    {
        services.AddHostedService<NightlyAnalyticsService>();
        services.AddHostedService<Emp201ReminderService>();
        services.AddHostedService<EtiExpiryAlertService>();
        return services;
    }
}
