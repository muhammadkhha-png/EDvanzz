namespace Edvanz.Domain.Events;

/// <summary>
/// Marker interface for domain events (Critique I-2 / D-11).
/// A domain event is an immutable record of something that happened in the domain
/// which other parts of the system may react to via handlers.
///
/// Dispatched AFTER the originating database transaction commits (never inside it)
/// so that event-handler side effects do not interfere with transactional integrity.
///
/// Handlers implement IDomainEventHandler&lt;TEvent&gt; and are resolved from DI.
/// New side effects for an existing event = new handler class — the event-emitting
/// service is NOT modified (OCP compliance).
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// UTC timestamp when the event occurred. Populated at emission.
    /// </summary>
    DateTime OccurredAtUtc { get; }
}
