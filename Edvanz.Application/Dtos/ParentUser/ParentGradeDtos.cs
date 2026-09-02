namespace Edvanz.Application.Dtos.ParentUser;

/// <summary>
/// One exam result row as a parent sees it, normalized across the two exam channels (paper
/// "Offline" exams from Module 6 and "Online" exams from the online-exam module) so a single card
/// component renders both.
///
/// The percentage is NOT recomputed here: the offline list service already produces
/// <c>ScorePercentage</c> (null when ungradeable), and the online list carries
/// <c>StudentDegree</c>/<c>ExamDegree</c> from which the composer derives the same value with the
/// exact formula the parent dashboard has always used.
/// </summary>
public class ParentGradeRowDto
{
    /// <summary>Exam identifier within its channel. Not unique ACROSS channels — pair it with <see cref="ExamType"/>.</summary>
    public long ExamId { get; set; }

    public string ExamName { get; set; } = string.Empty;

    /// <summary>The exam's subject label, resolved in the reader's language. Null when the teacher has no subject on file.</summary>
    public string? Subject { get; set; }

    /// <summary>Exam date (date only — no time component on either channel's list row).</summary>
    public DateOnly Date { get; set; }

    /// <summary>"Offline" (paper) or "Online". Stable literal — clients branch on it.</summary>
    public string ExamType { get; set; } = string.Empty;

    /// <summary>The student's score, or null when not graded / not attempted.</summary>
    public decimal? Score { get; set; }

    /// <summary>The exam's maximum grade — the denominator behind <see cref="ScorePercentage"/>. Null when none is configured.</summary>
    public decimal? MaxGrade { get; set; }

    /// <summary>Score ÷ MaxGrade × 100, or null when the row cannot produce a valid percentage.</summary>
    public decimal? ScorePercentage { get; set; }

    /// <summary>Competition rank within the exam's cohort (offline exams only). Null when not graded.</summary>
    public int? Rank { get; set; }

    /// <summary>Size of the ranking cohort (offline exams only). Renders as "Rank 3 of 25".</summary>
    public int? GroupSize { get; set; }

    /// <summary>True when <see cref="ScorePercentage"/> is present — i.e. the row counts toward the averages.</summary>
    public bool IsGraded { get; set; }
}

/// <summary>
/// One exam channel's contribution to the parent grade view: the visibility verdict plus the rows
/// it produced (empty when hidden or when the module failed — see <c>IParentSectionComposer</c>'s
/// fail-soft contract).
/// </summary>
public class ParentGradeSectionDto
{
    /// <summary>Whether the teacher shares this channel with parents.</summary>
    public bool Visible { get; set; }

    /// <summary>Rows for this channel. Always empty when <see cref="Visible"/> is false.</summary>
    public List<ParentGradeRowDto> Rows { get; set; } = new();
}
