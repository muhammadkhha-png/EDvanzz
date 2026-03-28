using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Represents a Student user account in the system. Links to a User record via UserId.
/// AAM-FR-05: Students register via Google, Apple, or username/password.
/// 
/// This is the student's OWN account on the platform — distinct from TeacherStudent,
/// which is the student data record a Teacher creates and manages.
/// 
/// The bridge between them is StudentTeacherLink: when the student provides
/// TeacherCode + StudentCode + HashedToken (AAM-FR-05.5), the system creates
/// a link from this account to the matching TeacherStudent record.
/// 
/// AAM-BR-06: A single Student account can be linked to multiple Teachers simultaneously.
/// </summary>
public class StudentUser : BaseEntity
{
    /// <summary>
    /// Foreign key to the User table. Establishes 1:1 identity link.
    /// The User record holds shared fields: FullName, Username, Email, PasswordHashed,
    /// PhoneNumber, SecurityStamp, UserType, IsActive.
    /// </summary>
    public long UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>
    /// Unique, immutable code auto-generated upon registration.
    /// AAM-FR-05.3: Auto-generated unique Student Code attached to the Student's account.
    /// AAM-NFR-03: Cryptographically unique and collision-resistant.
    /// 
    /// IMPORTANT: This is the ACCOUNT-level code — globally unique across the platform.
    /// It is NOT the teacher-assigned StudentCode from TeacherStudent (REQ-STU-004).
    /// Used by Parents to link to a child's account (AAM-FR-06.3 Method A).
    /// </summary>
    public string StudentAccountCode { get; set; } = null!;

    /// <summary>
    /// Student's preferred UI language. "en" or "ar".
    /// AAM-FR-02.1: Selected during registration.
    /// AAM-FR-02.3: Changeable from account settings at any time.
    /// </summary>
    public string? LanguagePreference { get; set; }

    /// <summary>
    /// Current account status: Active, Inactive, or Suspended.
    /// Reuses the existing AccountStatus enum shared with Teacher.
    /// </summary>
    public AccountStatus AccountStatus { get; set; } = AccountStatus.Active;

    /// <summary>
    /// Indicates whether this is the student's first login (no teachers linked yet).
    /// AAM-FR-05.4: Upon first login, the dashboard is empty with a prompt to add a Teacher.
    /// Set to false after the first teacher is successfully linked.
    /// </summary>
    public bool IsFirstLogin { get; set; } = true;

    /// <summary>
    /// Timestamp of account deactivation. Null if account is active.
    /// </summary>
    public DateTime? DeactivatedAt { get; set; }

    /// <summary>
    /// Soft-delete timestamp. Null if account is not deleted.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// Collection of teacher links for this student's dashboard.
    /// AAM-FR-05.7: A Student can add multiple Teachers.
    /// </summary>
    public ICollection<StudentTeacherLink> StudentTeacherLinks { get; set; } = new List<StudentTeacherLink>();
}