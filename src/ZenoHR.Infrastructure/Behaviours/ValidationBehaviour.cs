// REQ-OPS-001: ValidationBehaviour — runs FluentValidation before every MediatR command/query.
// REQ-OPS-005: Validation failures are logged at Warning so server logs capture client input errors.
// Validation failures return a Result<T>.Failure rather than throwing exceptions.
// Only requests with registered IValidator<TRequest> are validated.

using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using ZenoHR.Domain.Errors;

namespace ZenoHR.Infrastructure.Behaviours;

/// <summary>
/// MediatR pipeline behaviour that runs FluentValidation before the request handler.
/// If validation fails, the handler is never called — a <see cref="Result{T}"/> failure
/// is returned directly (no exception thrown).
/// <para>
/// Only requests that have a registered <see cref="IValidator{T}"/> are validated.
/// Requests without a validator pass through silently.
/// </para>
/// </summary>
internal sealed partial class ValidationBehaviour<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators,
    ILogger<ValidationBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
            return await next();

        // Log validation failure — field name and message only (never the attempted value: PII risk).
        var first = failures[0];
        LogValidationFailed(logger, typeof(TRequest).Name, failures.Count, first.PropertyName, first.ErrorMessage);

        // Build a ZenoHrError per validation failure.
        // If TResponse is Result<T>, return the first failure as a failed Result.
        // Otherwise throw ValidationException (API boundary will convert to ProblemDetails).
        var errors = failures
            .Select(f => ZenoHrError.ValidationFailed(f.PropertyName, f.ErrorMessage, f.AttemptedValue))
            .ToList();

        if (typeof(TResponse).IsGenericType &&
            typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
        {
            // Return the first validation error as a typed Result failure.
            var firstError = errors[0];
            var failureMethod = typeof(Result<>)
                .MakeGenericType(typeof(TResponse).GetGenericArguments()[0])
                .GetMethod(nameof(Result<object>.Failure), [typeof(ZenoHrError)])!;

            return (TResponse)failureMethod.Invoke(null, [firstError])!;
        }

        throw new ValidationException(failures);
    }

    [LoggerMessage(EventId = 5000, Level = LogLevel.Warning,
        Message = "Validation failed {RequestName}: {ErrorCount} error(s) — field '{Field}': {Message}")]
    private static partial void LogValidationFailed(
        ILogger logger, string requestName, int errorCount, string field, string message);
}
