// REQ-SEC-005, VUL-027: FluentValidation registration and filter for minimal API endpoints.
// Registers all validators from this assembly and provides a validation filter for endpoint groups.

using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;

namespace ZenoHR.Api.Validation;

/// <summary>
/// Extension methods for registering FluentValidation validators in the DI container
/// and applying validation as an endpoint filter on minimal API routes.
/// VUL-027: Ensures all POST/PUT request bodies are validated before reaching handlers.
/// </summary>
public static class ValidationExtensions
{
    /// <summary>
    /// Registers all <see cref="IValidator{T}"/> implementations from the ZenoHR.Api assembly.
    /// </summary>
    // VUL-027: Called from Program.cs to register all validators.
    public static IServiceCollection AddZenoHrValidation(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<CreateEmployeeRequestValidator>(
            ServiceLifetime.Singleton);
        return services;
    }

    /// <summary>
    /// Adds a validation endpoint filter that automatically validates request bodies
    /// using registered FluentValidation validators. Returns 400 ProblemDetails on failure.
    /// </summary>
    // VUL-027: Applied to endpoint groups that accept POST/PUT request bodies.
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder) where T : class
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
            if (validator is null)
                return await next(context);

            // Find the argument of type T in the endpoint parameters
            var argument = context.Arguments.OfType<T>().FirstOrDefault();
            if (argument is null)
                return await next(context);

            var validationResult = await validator.ValidateAsync(argument);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray());

                return Results.ValidationProblem(errors,
                    title: "One or more validation errors occurred.",
                    statusCode: 400);
            }

            return await next(context);
        });
    }
}
