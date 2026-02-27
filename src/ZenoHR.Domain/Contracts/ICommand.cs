// REQ-OPS-001: ICommand / ICommand<TResult> — CQRS command contract.
// Commands express intent to change state and always return Result or Result<T>.
// Commands NEVER return domain entities — they return IDs or summary records only.

using MediatR;
using ZenoHR.Domain.Errors;

namespace ZenoHR.Domain.Contracts;

/// <summary>
/// CQRS command that returns no value — only success or failure.
/// <para>
/// Implement this for write operations where the caller needs no return value
/// (e.g., archive, delete, status transitions, finalise).
/// </para>
/// </summary>
public interface ICommand : IRequest<Result> { }

/// <summary>
/// CQRS command that returns a typed result on success.
/// <para>
/// Implement this for write operations that create resources and need to return
/// the new resource ID or a summary (e.g., create employee → employee ID).
/// </para>
/// </summary>
/// <typeparam name="TResult">The type of data returned on success.</typeparam>
public interface ICommand<TResult> : IRequest<Result<TResult>> { }
