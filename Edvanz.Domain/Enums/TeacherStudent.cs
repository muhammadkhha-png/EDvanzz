using Edvanz.Domain.Entities.ShareProp;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Represents a student record managed by a Teacher (Module 1: Students Module).
/// This is NOT a user account — it is a data record created and owned by a Teacher.
/// REQ-STU-004: Defines the student data fields per teacher account.
/// REQ-STU-NFR-003: Student data is scoped exclusively to the owning teacher.
/// 
/// This entity is the target of the Student User linking flow (AAM-FR-05.5):
/// when a Student User provides TeacherCode + StudentCode + HashedToken,
/// the system matches against this table to establish the link.
/// </summary>
public class TeacherStudent : BaseEntity
{
    /// <summary>
    /// Foreign key to the owning Teacher. All student data is scoped to this teacher.
    /// REQ-STU-NFR-003: No student record is visible to other teacher accounts.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    /// <summary>
    /// Student's full name. The only mandatory field during creation.
    /// REQ-STU-005: Student Name is the only mandatory field.
    /// REQ-STU-NFR-002: Supports Arabic and English input.
    /// </summary>
    public string StudentName { get; set; } = null!;

    /// <summary>
    /// Unique student code within this teacher's account.
    /// REQ-STU-007/008: Auto-generated (A1, A2 ... Z999) or manually entered.
    /// REQ-STU-010: Must be unique per teacher account.
    /// REQ-STU-CODE-003: Stored as uppercase, matched case-insensitive.
    /// AAM-FR-05.5: Used as one of three credentials for Student User linking.
    /// </summary>
    public string StudentCode { get; set; } = null!;

    /// <summary>
    /// Auto-generated hashed token for this student under this teacher.
    /// REQ-STU-004: "Student Hashed Password" — auto-generated.
    /// AAM-FR-05.5: Used as one of three credentials for Student User linking.
    /// AAM-NFR-03: Cryptographically unique and collision-resistant.
    /// </summary>
    public string HashedToken { get; set; } = null!;

    /// <summary>
    /// Student's phone number. Optional during creation.
    /// REQ-STU-004/005: Optional field.
    /// </summary>
    public string? StudentPhoneNumber { get; set; }

    /// <summary>
    /// Parent's phone number. Optional during creation.
    /// REQ-STU-004/005: Optional field.
    /// </summary>
    public string? ParentPhoneNumber { get; set; }

    /// <summary>
    /// Auto-generated barcode encoding the student code.
    /// REQ-STU-047: Generated at creation time, permanently associated.
    /// REQ-STU-048: Never changes even if other fields are modified.
    /// </summary>
    public string? Barcode { get; set; }

    /// <summary>
    /// Soft-delete flag for recycle bin functionality.
    /// REQ-STU-025: Deleted records move to recycle bin.
    /// REQ-STU-026: Retained for 10 days before permanent purge.
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Timestamp when the record was moved to the recycle bin.
    /// REQ-STU-027: 10-day retention window starts from this date.
    /// Null if the record is active.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public ICollection<StudentTeacherLink> StudentTeacherLinks { get; set; } = new List<StudentTeacherLink>();
}