// REQ-OPS-002: MediatR DI registration — wires MediatR, pipeline behaviours, and all module handlers.
// All module assemblies must be listed here so MediatR can discover handlers.

using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ZenoHR.Infrastructure.Behaviours;

namespace ZenoHR.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering MediatR and its pipeline behaviours.
/// Called from Program.cs / the application host startup.
/// </summary>
public static class MediatRExtensions
{
    /// <summary>
    /// Registers MediatR with all module handler assemblies and the standard pipeline behaviours:
    /// <list type="number">
    ///   <item>Logging — every request is timed and logged.</item>
    ///   <item>Validation — FluentValidation runs before the handler (only for requests with a validator).</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddZenoHrMediatR(this IServiceCollection services)
    {
        // Discover handlers from all module assemblies + infrastructure
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(
                typeof(ZenoHR.Module.Employee.AssemblyMarker).Assembly,
                typeof(ZenoHR.Module.TimeAttendance.AssemblyMarker).Assembly,
                typeof(ZenoHR.Module.Leave.AssemblyMarker).Assembly,
                typeof(ZenoHR.Module.Payroll.AssemblyMarker).Assembly,
                typeof(ZenoHR.Module.Compliance.AssemblyMarker).Assembly,
                typeof(ZenoHR.Module.Audit.AssemblyMarker).Assembly,
                typeof(ZenoHR.Module.Risk.AssemblyMarker).Assembly,
                typeof(MediatRExtensions).Assembly   // Infrastructure handlers (if any)
            );

            // Order matters: Logging wraps Validation wraps Handler
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
        });

        // Register FluentValidation validators from all module assemblies
        services.AddValidatorsFromAssemblies(
        [
            typeof(ZenoHR.Module.Employee.AssemblyMarker).Assembly,
            typeof(ZenoHR.Module.TimeAttendance.AssemblyMarker).Assembly,
            typeof(ZenoHR.Module.Leave.AssemblyMarker).Assembly,
            typeof(ZenoHR.Module.Payroll.AssemblyMarker).Assembly,
            typeof(ZenoHR.Module.Compliance.AssemblyMarker).Assembly,
            typeof(ZenoHR.Module.Audit.AssemblyMarker).Assembly,
            typeof(ZenoHR.Module.Risk.AssemblyMarker).Assembly,
        ], lifetime: ServiceLifetime.Scoped);

        return services;
    }
}
