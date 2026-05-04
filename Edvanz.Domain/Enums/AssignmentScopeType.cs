using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// The kind of target scope an assignment applies to.
/// REQ-EXH-002: An assignment may target a specific individual student, a specific session
/// (all students in that session), or a specific session group (all students across the group's sessions).
/// REQ-EXH-003: A single assignment may combine multiple scopes; deduplication is enforced
/// at write time by the service layer and at the database level by a unique composite index
/// on (OccurrenceId, TeacherStudentId).
///
/// On the AssignmentScopes row, this value identifies which of the three nullable foreign keys
/// (TeacherStudentId, SessionId, SessionGroupId) is populated. A CHECK constraint enforces that
/// exactly one is non-null and consistent with this value.
///
/// Stored as tinyint in the database.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AssignmentScopeType : byte
{
    /// <summary>
    /// The scope row targets a single named student.
    /// On this row, TeacherStudentId is non-null; SessionId and SessionGroupId are null.
    /// </summary>
    IndividualStudent = 0,

    /// <summary>
    /// The scope row targets all students assigned to a specific session.
    /// On this row, SessionId is non-null; TeacherStudentId and SessionGroupId are null.
    /// </summary>
    Session = 1,

    /// <summary>
    /// The scope row targets all students assigned to any session within a specific session group.
    /// On this row, SessionGroupId is non-null; TeacherStudentId and SessionId are null.
    /// </summary>
    SessionGroup = 2
}
