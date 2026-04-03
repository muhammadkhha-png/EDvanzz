using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Defines the target scope for a payment event.
/// REQ-EVT-002: Target Scope is mandatory during event creation.
/// REQ-EVT-003/004/005/006/007: Scope options from individual to all students.
/// BR-EVT-001: Only students in scope at creation time receive the obligation.
/// Stored as tinyint in the database.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EventTargetScopeType : byte
{
    /// <summary>
    /// Specific individual students selected by the tutor.
    /// REQ-EVT-003: Individual student selection.
    /// </summary>
    IndividualStudents = 1,

    /// <summary>
    /// All students assigned to a specific session.
    /// REQ-EVT-004: Session-scoped event.
    /// </summary>
    Session = 2,

    /// <summary>
    /// All students across all sessions in a session group.
    /// REQ-EVT-005: Session group-scoped event.
    /// </summary>
    SessionGroup = 3,

    /// <summary>
    /// All students under the tutor's account.
    /// REQ-EVT-006: All students scope.
    /// </summary>
    AllStudents = 4
}