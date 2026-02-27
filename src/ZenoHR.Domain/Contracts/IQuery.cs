// REQ-OPS-001: IQuery<TResult> — CQRS query contract.
// Queries NEVER mutate state. They always return Result<TResult>.
// Handlers must use read-only Firestore operations (no writes inside a query handler).

using MediatR;
using ZenoHR.Domain.Errors;

namespace ZenoHR.Domain.Contracts;

/// <summary>
/// CQRS query that reads data and returns a typed result.
/// <para>
/// Queries must NEVER have side effects. Handlers must use read-only operations only.
/// </para>
/// </summary>
/// <typeparam name="TResult">The type of data returned on success.</typeparam>
public interface IQuery<TResult> : IRequest<Result<TResult>> { }
