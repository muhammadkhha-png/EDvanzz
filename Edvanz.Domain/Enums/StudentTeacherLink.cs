using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Junction table linking a Student User account to a Teacher.
/// Created when the student successfully provides all three credentials (AAM-FR-05.5):
///   1. Teacher's unique 8-digit Teacher Code
///   2. The student's unique code as assigned by the Teacher (TeacherStudent.StudentCode)
///   3. The hash/token generated for that student under that Teacher (TeacherStudent.HashedToken)
/// 
/// AAM-FR-05.6: Upon successful linking, the teacher appears on the student's dashboard.
/// AAM-FR-05.7: A student can link to multiple teachers; each is displayed distinctly.
/// AAM-BR-06: Each teacher's visibility settings apply independently.
/// AAM-FR-05.8: Content visibility is governed by TeacherConfiguration (AAM-FR-04.8).
/// 
/// This is the primary data-access join path for the student dashboard:
///   StudentUser → StudentTeacherLink → Teacher → TeacherConfiguration (visibility)
///   StudentUser → StudentTeacherLink → TeacherStudent (attendance, payments, grades)
/// </summary>
public class StudentTeacherLink : BaseEntity
{
    /// <summary>
    /// Foreign key to the Student User account performing the link.
    /// </summary>
    [ForeignKey(nameof(StudentUser))]
    public long StudentUserId { get; set; }
    public StudentUser StudentUser { get; set; } = null!;

    /// <summary>
    /// Foreign key to the Teacher being linked.
    /// Resolved from the TeacherCode provided during linking (AAM-FR-05.5 credential #1).
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    /// <summary>
    /// Foreign key to the specific TeacherStudent record under that teacher.
    /// Resolved from StudentCode + HashedToken (AAM-FR-05.5 credentials #2 and #3).
    /// 
    /// Nullable: set to null if the teacher deletes the student record.
    /// The link survives but the UI should show a "removed enrollment" state.
    /// 
    /// This FK is the performance-critical join path for all student data access:
    /// attendance, payments, homework, exams all route through this reference.
    /// </summary>
    [ForeignKey(nameof(TeacherStudent))]
    public long? TeacherStudentId { get; set; }
    public TeacherStudent? TeacherStudent { get; set; }

    /// <summary>
    /// Current status of the link.
    /// Active: student can see teacher data on their dashboard.
    /// Unlinked: student removed the teacher (soft-unlink, preserved for audit).
    /// </summary>
    public LinkStatus LinkStatus { get; set; } = LinkStatus.Active;

    /// <summary>
    /// Timestamp when the link was successfully established.
    /// AAM-FR-05.6: The moment the teacher entry appears on the student's dashboard.
    /// </summary>
    public DateTime LinkedAt { get; set; }

    /// <summary>
    /// Timestamp when the student removed the teacher from their dashboard.
    /// Null if the link is still active.
    /// </summary>
    public DateTime? UnlinkedAt { get; set; }
}