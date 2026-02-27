// REQ-OPS-001: ICommandHandler<TCommand> / ICommandHandler<TCommand, TResult> — CQRS handler contracts.
// All command handlers in ZenoHR must implement one of these interfaces.
// This enables cross-cutting pipeline behaviours (logging, validation, tracing) to apply uniformly.

using MediatR;
using ZenoHR.Domain.Errors;

namespace ZenoHR.Domain.Contracts;

/// <summary>
/// Handler for a command that produces no return value.
/// </summary>
/// <typeparam name="TCommand">The command type (must implement <see cref="ICommand"/>).</typeparam>
public interface ICommandHandler<TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand { }

/// <summary>
/// Handler for a command that returns a typed result on success.
/// </summary>
/// <typeparam name="TCommand">The command type (must implement <see cref="ICommand{TResult}"/>).</typeparam>
/// <typeparam name="TResult">The result type returned on success.</typeparam>
public interface ICommandHandler<TCommand, TResult> : IRequestHandler<TCommand, Result<TResult>>
    where TCommand : ICommand<TResult> { }
