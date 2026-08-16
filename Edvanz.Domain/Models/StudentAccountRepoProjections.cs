namespace Edvanz.Domain.Interfaces;

// ════════════════════════════════════════════════════════════════════════════
// STUDENT ACCOUNT (SUPER-ADMIN LIST) — REPOSITORY PROJECTION TYPES
// ════════════════════════════════════════════════════════════════════════════
//
// Query projections returned by the IUserRepo student-account list methods for
// the SuperAdmin "Student Accounts" screen. Same convention as
// StudentLinkRepoProjections: these are NOT DTOs — the repo returns these, the
// service maps them to client-facing DTOs (Catalog: repo projections vs.
// Application DTOs stay separate so a repo shape change never leaks to the API).
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// One page row on the SuperAdmin student-accounts list: the account identity
/// only (StudentUser + User). Linked-teacher rows are loaded separately in one
/// batch query (<see cref="IUserRepo.GetActiveLinkedTeachersForStudentUsersAsync"/>)
/// keyed by <see cref="StudentAccountId"/> to avoid an N+1 per row.
/// </summary>
public sealed class StudentAccountRow
{
    /// <summary>StudentUser.Id — the value returned to the client as studentAccountId.</summary>
    public long StudentAccountId { get; set; }
    public string StudentAccountCode { get; set; }
    public string FullName { get; set; } = null!;
    public string UserName { get; set; } = null!;
    public string? PhoneNumber { get; set; }

    /// <summary>User.LastLoginAt — most recent successful login, or null if never.</summary>
    public DateTime? LastLoginAt { get; set; }
}

/// <summary>
/// One ACTIVE teacher link for a student account, batch-loaded for a page of
/// <see cref="StudentAccountRow"/> results. <see cref="StudentCode"/> is the
/// per-teacher code from the bound TeacherStudent roster record — null when
/// the Active link is not (or no longer) bound to one (see
/// GetActiveLinkedStudentsForTeacherPagedAsync for the same degraded-link case).
/// </summary>
public sealed class StudentAccountLinkedTeacherRow
{
    /// <summary>FK back to the owning <see cref="StudentAccountRow.StudentAccountId"/> for grouping.</summary>
    public long StudentUserId { get; set; }

    public long TeacherId { get; set; }
    public string TeacherCode { get; set; } = null!;
    public string TeacherName { get; set; } = null!;
    public string? StudentCode { get; set; }
}
