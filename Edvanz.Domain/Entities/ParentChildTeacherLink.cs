using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Links a Method B child (manual profile, no Student User account) to a Teacher.
/// Used ONLY when the parent creates a child profile manually (AAM-FR-06.3 Method B).
/// 
/// For Method A children (linked to a StudentUser), the teacher data is accessed
/// via StudentUser → StudentTeacherLink — this table is NOT used.
/// 
/// The linking flow is identical to StudentTeacherLink (AAM-FR-05.5):
///   1. Teacher's unique 8-digit TeacherCode
///   2. The student code assigned by the Teacher (TeacherStudent.StudentCode)
///   3. The hash/token generated for that student (TeacherStudent.HashedToken)
/// 
/// AAM-FR-06.5: Each child profile can be linked to multiple Teachers.
/// AAM-FR-06.6: Visibility governed by TeacherConfiguration parent settings (AAM-FR-04.9).
/// AAM-BR-03: Parent cannot override or expand visibility — Teacher controls it entirely.
/// </summary>
public class ParentChildTeacherLink : BaseEntity
{
    /// <summary>
    /// Foreign key to the ParentChild record this link belongs to.
    /// </summary>
    [ForeignKey(nameof(ParentChild))]
    public long ParentChildId { get; set; }
    public ParentChild ParentChild { get; set; } = null!;

    /// <summary>
    /// Foreign key to the Teacher being linked.
    /// Resolved from the TeacherCode provided during linking.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    /// <summary>
    /// Foreign key to the specific TeacherStudent record under that teacher.
    /// Resolved from StudentCode + HashedToken during the linking flow.
    /// 
    /// Nullable: set to null if the teacher deletes the student record.
    /// </summary>
    [ForeignKey(nameof(TeacherStudent))]
    public long? TeacherStudentId { get; set; }
    public TeacherStudent? TeacherStudent { get; set; }

    /// <summary>
    /// Current status of the link.
    /// Reuses the existing LinkStatus enum (Active / Unlinked).
    /// </summary>
    public LinkStatus LinkStatus { get; set; } = LinkStatus.Active;

    /// <summary>
    /// Timestamp when the link was successfully established.
    /// </summary>
    public DateTime LinkedAt { get; set; }

    /// <summary>
    /// Timestamp when the parent removed the teacher from this child.
    /// Null if the link is still active.
    /// </summary>
    public DateTime? UnlinkedAt { get; set; }
}