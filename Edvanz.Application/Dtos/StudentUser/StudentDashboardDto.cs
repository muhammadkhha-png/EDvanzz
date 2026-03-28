namespace Edvanz.Application.Dtos.StudentUser;

/// <summary>
/// Output DTO representing the Student's dashboard state.
/// AAM-FR-05.4: Empty with a prompt to add a Teacher on first login.
/// AAM-FR-05.7: Lists all linked Teachers distinctly.
/// </summary>
public class StudentDashboardDto
{
    /// <summary>
    /// Whether this is the student's first login with no teachers linked.
    /// AAM-FR-05.4: UI should show an empty state with "Add Teacher" prompt.
    /// </summary>
    public bool IsFirstLogin { get; set; }

    /// <summary>
    /// The student's unique account code, displayed on their dashboard for reference.
    /// AAM-FR-05.3: Students may need to share this with parents (AAM-FR-06.3).
    /// </summary>
    public string StudentAccountCode { get; set; } = null!;

    /// <summary>
    /// List of teachers currently linked to this student's dashboard.
    /// AAM-FR-05.7: Each Teacher is displayed distinctly.
    /// Empty list on first login (AAM-FR-05.4).
    /// Only active links are included (LinkStatus = Active).
    /// </summary>
    public List<StudentDashboardTeacherDto> LinkedTeachers { get; set; } = new();
}