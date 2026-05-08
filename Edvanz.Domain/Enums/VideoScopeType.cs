namespace Edvanz.Domain.Enums;

/// <summary>
/// Identifies which of the three nullable target FKs is populated on a
/// <c>VideoScope</c> row. Mirrors <see cref="AssignmentScopeType"/> from Module 6
/// for consistency across the codebase.
///
/// REQ-VCM-FR-02: A video can be targeted to (a) a single student, (b) all students
/// currently in a session, or (c) all students across the sessions of a session group.
///
/// CHECK constraints in <c>EdvanzDbContext.OnModelCreating</c> enforce that
/// <c>ScopeType</c> matches exactly one populated FK on the row — so this enum is
/// the discriminator that aligns with database-level integrity.
///
/// Numeric values are part of the persisted contract — never reorder or repurpose.
/// </summary>
public enum VideoScopeType : byte
{
    /// <summary>Targets exactly one <c>TeacherStudent</c> row.</summary>
    IndividualStudent = 0,

    /// <summary>Targets every student currently assigned to the referenced session.</summary>
    Session = 1,

    /// <summary>Targets every student across the sessions of the referenced group.</summary>
    SessionGroup = 2
}
