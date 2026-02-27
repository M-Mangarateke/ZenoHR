// REQ-OPS-001: IQueryHandler<TQuery, TResult> — CQRS query handler contract.
// All query handlers in ZenoHR must implement this interface.

using MediatR;
using ZenoHR.Domain.Errors;

namespace ZenoHR.Domain.Contracts;

/// <summary>
/// Handler for a query that returns a typed result.
/// </summary>
/// <typeparam name="TQuery">The query type (must implement <see cref="IQuery{TResult}"/>).</typeparam>
/// <typeparam name="TResult">The result type returned on success.</typeparam>
public interface IQueryHandler<TQuery, TResult> : IRequestHandler<TQuery, Result<TResult>>
    where TQuery : IQuery<TResult> { }
