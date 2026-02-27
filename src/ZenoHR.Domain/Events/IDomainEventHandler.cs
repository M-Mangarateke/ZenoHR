// REQ-OPS-002: IDomainEventHandler<T> — typed handler contract for domain events.
// All cross-module reactions to state changes are implemented as IDomainEventHandlers.

using MediatR;

namespace ZenoHR.Domain.Events;

/// <summary>
/// Marker interface for domain event handlers.
/// Wraps <see cref="INotificationHandler{TNotification}"/> to enforce the constraint
/// that <typeparamref name="TEvent"/> must be a <see cref="DomainEvent"/>.
/// <para>
/// Register all handlers via MediatR's assembly scanning in Program.cs/DI extensions.
/// </para>
/// </summary>
/// <typeparam name="TEvent">The specific <see cref="DomainEvent"/> this handler reacts to.</typeparam>
public interface IDomainEventHandler<TEvent> : INotificationHandler<TEvent>
    where TEvent : DomainEvent
{
    // Inherits: Task Handle(TEvent notification, CancellationToken cancellationToken)
}
