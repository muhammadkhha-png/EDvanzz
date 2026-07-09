using Edvanz.Domain.Enums;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Edvanz.Application.Dtos.TeacherStudent;

// ══════════════════════════════════════════════
// INPUT DTOs
// ══════════════════════════════════════════════

/// <summary>
/// Input DTO for creating a single student record under a teacher.
/// REQ-STU-012: Manual student entry form.
/// REQ-STU-005: Only StudentName is mandatory.
/// </summary>
public class CreateTeacherStudentDto
{
  

    /// <summary>
    /// Student's full name. Mandatory.
    /// REQ-STU-014: Cannot be empty or whitespace.
    /// REQ-STU-NFR-002: Supports Arabic and English.
    /// </summary>
    [Required]
    public string StudentName { get; set; } = null!;

    /// <summary>
    /// Optional student phone number.
    /// </summary>
    public string? StudentPhoneNumber { get; set; }

    /// <summary>
    /// Optional parent phone number.
    /// </summary>
    public string? ParentPhoneNumber { get; set; }

    /// <summary>
    /// Optional manual student code. Only used when GenerationMode == Manual.
    /// REQ-STU-011: Teacher enters a custom code when auto-generation is disabled.
    /// REQ-STU-CODE-001: 1-10 chars, alphanumeric only (A-Z, a-z, 0-9).
    /// REQ-STU-CODE-003: Normalized to uppercase on save.
    /// </summary>
    public string? StudentCode { get; set; }

    /// <summary>
    /// Optional session Id to assign the student to.
    /// BR-SES-002: A student may only be assigned to one session at a time.
    /// </summary>
    public long? SessionId { get; set; }

}

/// <summary>
/// Input DTO for updating an existing student record.
/// REQ-STU-006: All student fields remain fully editable after creation.
/// </summary>
public class UpdateTeacherStudentDto
{
    /// <summary>
    /// Updated student name. Mandatory — cannot be blank.
    /// </summary>
    [Required]
    public string StudentName { get; set; } = null!;

    /// <summary>
    /// Updated student phone number. Null to clear.
    /// </summary>
    public string? StudentPhoneNumber { get; set; }

    /// <summary>
    /// Updated parent phone number. Null to clear.
    /// </summary>
    public string? ParentPhoneNumber { get; set; }

    /// <summary>
    /// Updated student code. Only allowed when GenerationMode == Manual.
    /// REQ-STU-CODE-001: Validated for format. Normalized to uppercase.
    /// </summary>
    public string? StudentCode { get; set; }

    /// <summary>
    /// Updated session assignment. Null to unassign.
    /// </summary>
    public long? SessionId { get; set; }
}

/// <summary>
/// Input DTO for a single row in a bulk import operation.
/// REQ-STU-015 through REQ-STU-020: Bulk student import from Google Sheets.
/// </summary>
public class BulkImportStudentRowDto
{
    /// <summary>
    /// Student name from the import sheet. Required — rows with empty names are skipped.
    /// REQ-STU-018: Skip rows where StudentName is empty.
    /// </summary>
    public string? StudentName { get; set; }

    /// <summary>
    /// Student phone number from the import sheet. Optional.
    /// </summary>
    public string? StudentPhoneNumber { get; set; }

    /// <summary>
    /// Parent phone number from the import sheet. Optional.
    /// </summary>
    public string? ParentPhoneNumber { get; set; }

    /// <summary>
    /// Student code from the import sheet. Optional — auto-generated if blank.
    /// REQ-STU-018: Auto-generate code when blank.
    /// REQ-STU-CODE-006: Format validation applied to manually provided codes.
    /// </summary>
    public string? StudentCode { get; set; }
}

/// <summary>
/// Input DTO for the bulk import request envelope.
/// </summary>
public class BulkImportTeacherStudentsDto
{
   

    /// <summary>
    /// The list of student rows to import.
    /// </summary>
    [Required]
    public List<BulkImportStudentRowDto> Students { get; set; } = new();
}

/// <summary>
/// Input DTO for bulk delete or bulk restore operations.
/// REQ-STU-022: Bulk delete by selecting multiple student Ids.
/// REQ-STU-031.1: Bulk restore from recycle bin.
/// </summary>
public class BulkStudentIdsDto
{
   
    /// <summary>
    /// List of student record Ids to operate on.
    /// </summary>
    [Required]
    public List<long> StudentIds { get; set; } = new();
}

/// <summary>
/// Paginated request specific to the student list.
/// Extends the shared pagination pattern with student-specific filters.
/// REQ-STU-036 through REQ-STU-039: Filter panel options.
/// REQ-STU-SRT-001 through REQ-STU-SRT-007: Sort options.
/// </summary>
public class StudentListRequest
{
    private int _page = 1;
    private int _pageSize = 20;

    /// <summary>
    /// Page number (1-based). Defaults to 1.
    /// </summary>
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    /// <summary>
    /// Records per page. Defaults to 20. Max 100.
    /// REQ-STU-042: Recommended 10-30 records per batch.
    /// </summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 20 : value > 100 ? 100 : value;
    }

    /// <summary>
    /// Search term for name, code, or session name.
    /// REQ-STU-032: Partial match, case-insensitive, supports Arabic and English.
    /// </summary>
    [Description("Search by: student name, student code (partial match)")]
    public string? Search { get; set; }

    /// <summary>
    /// Filter by assigned session Id.
    /// REQ-STU-036: "By Assigned Session" filter.
    /// </summary>
    public long? SessionId { get; set; }

    /// <summary>
    /// If true, show only students with no phone number.
    /// REQ-STU-036: "Missing Student Phone Number" filter.
    /// </summary>
    public bool MissingStudentPhone { get; set; } = false;

    /// <summary>
    /// If true, show only students with no parent phone number.
    /// REQ-STU-036: "Missing Parent Phone Number" filter.
    /// </summary>
    public bool MissingParentPhone { get; set; } = false;

    /// <summary>
    /// If true, show only students with no assigned session.
    /// REQ-STU-036: "Missing Assigned Session" filter.
    /// </summary>
    public bool MissingSession { get; set; } = false;

    /// <summary>
    /// Column to sort by. Defaults to DateAdded (newest first).
    /// REQ-STU-SRT-002: Default sort is DateAdded descending.
    /// </summary>
    public StudentSortBy SortBy { get; set; } = StudentSortBy.DateAdded;

    /// <summary>
    /// Sort direction. Defaults to Desc (newest first).
    /// </summary>
    public SortDirection SortDirection { get; set; } = SortDirection.Desc;
}

// ══════════════════════════════════════════════
// OUTPUT DTOs
// ══════════════════════════════════════════════

/// <summary>
/// Output DTO for a single student record. Used in list, profile, and creation responses.
/// </summary>
public class TeacherStudentDto
{
    public long Id { get; set; }
    public long TeacherId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public string? StudentPhoneNumber { get; set; }
    public string? ParentPhoneNumber { get; set; }
    public string? Barcode { get; set; }
    public long? SessionId { get; set; }

    /// <summary>
    /// HashedToken is included so the teacher can share it with the student
    /// for the linking flow (AAM-FR-05.5). The teacher needs to give the student:
    /// TeacherCode + StudentCode + HashedToken.
    /// </summary>
    public string HashedToken { get; set; } = null!;

    /// <summary>
    /// Indicates record completeness for the visual indicator.
    /// REQ-STU-UX-007: Colored dot showing complete vs incomplete.
    /// True = all optional fields filled (phone, parent phone, session).
    /// </summary>
    public bool IsComplete { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Output DTO for a student record in the recycle bin.
/// Extends the base DTO with deletion metadata.
/// REQ-STU-UX-010: Days remaining before permanent deletion.
/// </summary>
public class RecycleBinStudentDto
{
    public long Id { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public string? StudentPhoneNumber { get; set; }
    public string? ParentPhoneNumber { get; set; }
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Number of days remaining before permanent purge.
    /// REQ-STU-UX-010: Color progression based on days remaining.
    /// </summary>
    public int DaysRemaining { get; set; }
}

/// <summary>
/// Output DTO for the student list header counters.
/// REQ-STU-UX-001: Total student count.
/// REQ-STU-UX-002: Filtered count when filters are active.
/// REQ-STU-UX-009: Recycle bin count for badge.
/// </summary>
public class StudentCountsDto
{
    /// <summary>
    /// Total active (non-deleted) students in the teacher's account.
    /// </summary>
    public int TotalActiveStudents { get; set; }

    /// <summary>
    /// Number of students matching current filters (equals TotalActiveStudents if no filters).
    /// </summary>
    public int FilteredCount { get; set; }

    /// <summary>
    /// Number of students in the recycle bin.
    /// </summary>
    public int RecycleBinCount { get; set; }
}

/// <summary>
/// Output DTO for the bulk import summary report.
/// REQ-STU-019: Import summary with totals and failure details.
/// </summary>
public class BulkImportResultDto
{
    /// <summary>
    /// Total rows processed from the import sheet.
    /// </summary>
    public int TotalProcessed { get; set; }

    /// <summary>
    /// Number of students successfully imported.
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of rows that failed validation.
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// Details of each failed row including the reason.
    /// REQ-STU-UX-014: Color-coded summary with failure reasons.
    /// </summary>
    public List<BulkImportFailureDto> Failures { get; set; } = new();
}

/// <summary>
/// Detail of a single failed row during bulk import.
/// </summary>
public class BulkImportFailureDto
{
    /// <summary>
    /// 1-based row number from the import sheet.
    /// </summary>
    public int RowNumber { get; set; }

    /// <summary>
    /// The student name from the failed row (if any).
    /// </summary>
    public string? StudentName { get; set; }

    /// <summary>
    /// The student code from the failed row (if any).
    /// </summary>
    public string? StudentCode { get; set; }

    /// <summary>
    /// Human-readable reason for the failure (localized).
    /// </summary>
    public string Reason { get; set; } = null!;
}