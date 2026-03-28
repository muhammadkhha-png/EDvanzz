using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Represents a child managed by a Parent account.
/// AAM-FR-06.3: Two methods to add a child:
///   Method A — Child has a Student User account: StudentUserId is set.
///   Method B — Child has no account: ChildName is set, teachers are linked via ParentChildTeacherLink.
/// 
/// AAM-FR-06.4: A Parent can manage multiple children (1:N from ParentUser).
/// AAM-FR-06.5: Each child profile can be linked to multiple Teachers.
/// AAM-BR-07: A single Parent account can manage multiple children,
///            and each child can be linked to multiple Teachers.
/// </summary>
public class ParentChild : BaseEntity
{
    /// <summary>
    /// Foreign key to the owning Parent account.
    /// </summary>
    [ForeignKey(nameof(ParentUser))]
    public long ParentUserId { get; set; }
    public ParentUser ParentUser { get; set; } = null!;

    /// <summary>
    /// How this child was linked to the parent.
    /// StudentAccount = Method A (linked via StudentAccountCode).
    /// ManualProfile = Method B (parent entered the child's name manually).
    /// </summary>
    public ChildLinkMethod LinkMethod { get; set; }

    /// <summary>
    /// Foreign key to the child's Student User account.
    /// Set ONLY for Method A (child has a Student User account).
    /// Null for Method B (manual profile — child has no account).
    /// 
    /// When set, the parent inherits visibility into all teachers
    /// already linked to this StudentUser (via StudentTeacherLink),
    /// governed by AAM-FR-04.9 parent visibility settings.
    /// </summary>
    [ForeignKey(nameof(StudentUser))]
    public long? StudentUserId { get; set; }
    public StudentUser? StudentUser { get; set; }

    /// <summary>
    /// Child's display name.
    /// Method A: copied from the StudentUser's User.FullName at link time for display.
    /// Method B: entered manually by the parent (AAM-FR-06.3 Method B).
    /// Always editable by the parent for their own reference.
    /// </summary>
    public string ChildName { get; set; } = null!;

    /// <summary>
    /// Whether this child link is still active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Navigation properties

    /// <summary>
    /// Teacher links for Method B children (manual profile, no student account).
    /// AAM-FR-06.5: Each child can be linked to multiple Teachers.
    /// For Method A children, teacher data is accessed via StudentUser.StudentTeacherLinks.
    /// </summary>
    public ICollection<ParentChildTeacherLink> TeacherLinks { get; set; } = new List<ParentChildTeacherLink>();
}