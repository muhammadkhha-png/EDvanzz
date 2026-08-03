namespace Edvanz.Application.Dtos.ParentUser;

/// <summary>
/// Output DTO representing a single child on the Parent's dashboard.
/// AAM-FR-06.4: Parent manages multiple children, each displayed distinctly.
/// </summary>
public class ParentChildDto
{
    public long ChildId { get; set; }
    public string ChildName { get; set; } = null!;

    /// <summary>
    /// How this child was linked: "StudentAccount" (Method A) or "ManualProfile" (Method B).
    /// </summary>
    public string LinkMethod { get; set; } = null!;

    /// <summary>
    /// The child's StudentAccountCode. Present only for Method A children.
    /// Null for Method B (manual profile).
    /// </summary>
    public string? StudentAccountCode { get; set; }

    /// <summary>
    /// Whether the child link is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Teachers linked to this child.
    /// Method A: sourced from StudentUser.StudentTeacherLinks (read-only for parent).
    /// Method B: sourced from ParentChildTeacherLink (managed by parent).
    /// </summary>
    public List<ParentChildTeacherDto> Teachers { get; set; } = new();
}

/// <summary>
/// Output DTO representing a single teacher linked to a child, as seen by the Parent.
/// AAM-FR-06.6: Visibility governed by TeacherConfiguration parent settings (AAM-FR-04.9).
/// AAM-BR-03: Parent cannot override or expand visibility.
/// </summary>
public class ParentChildTeacherDto
{
    /// <summary>
    /// The link Id (ParentChildTeacherLink.Id for Method B, StudentTeacherLink.Id for Method A).
    /// </summary>
    public long LinkId { get; set; }

    public string TeacherCode { get; set; } = null!;
    public string TeacherFullName { get; set; } = null!;
    public string SubjectName { get; set; } = null!;
    public DateTime LinkedAt { get; set; }
    public bool IsEnrollmentActive { get; set; }

    // ─── Visibility flags from TeacherConfiguration (AAM-FR-04.9) ───

    public bool VisibilityAttendance { get; set; }
    public bool VisibilityPayment { get; set; }
    public bool VisibilityHomework { get; set; }
    public bool VisibilityExamDefault { get; set; }

    /// <summary>Whether this teacher allows the parent to see the Videos module. Added Phase 2.</summary>
    public bool VisibilityVideo { get; set; }

    /// <summary>Default online-exam visibility for this teacher. Added Phase 2.</summary>
    public bool VisibilityOnlineExamDefault { get; set; }
}

/// <summary>
/// Output DTO for the parent's full dashboard.
/// </summary>
public class ParentDashboardDto
{
    /// <summary>
    /// List of children managed by this parent.
    /// AAM-FR-06.4: Each child displayed distinctly.
    /// </summary>
    public List<ParentChildDto> Children { get; set; } = new();
}