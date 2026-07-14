namespace Edvanz.Domain.Enums;

/// <summary>
/// Identifies which of the two nullable target FKs is populated on an
/// <see cref="Entities.OnlineExamScope"/> row. Mirrors <see cref="VideoScopeType"/>
/// minus the individual-student branch (Q1 decision: no per-student exam scope).
///
/// CHECK constraints in <c>EdvanzDbContext.OnModelCreating</c> enforce that
/// <c>ScopeType</c> matches exactly one populated FK — see
/// <c>CK_OnlineExamScopes_ScopeTypeMatchesFK</c>.
///
/// Numeric values are part of the persisted contract — never reorder.
/// </summary>
public enum OnlineExamScopeType : byte
{
    /// <summary>Targets every student currently assigned to the referenced session.</summary>
    Session = 1,

    /// <summary>Targets every student across the sessions of the referenced group.</summary>
    SessionGroup = 2
}