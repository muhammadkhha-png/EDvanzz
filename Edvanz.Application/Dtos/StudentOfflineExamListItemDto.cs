using Edvanz.Domain.Enums;

/// <summary>
/// One row in the student's (read-only) offline-exam list — a paper exam the student sat under a
/// given teacher. One row per <see cref="Edvanz.Domain.Entities.AssignmentOccurrence"/> the student
/// has an obligation for. Enriched (audit F1/F2/F3) with the exam's max grade, the publishing
/// teacher's subject, and the student's leaderboard rank within the exam's cohort.
/// </summary>
public class StudentOfflineExamListItemDto
{
    public long ExamId { get; set; }            // = OccurrenceId, see flag #1
    public string ExamName { get; set; } = null!;
    public string? Description { get; set; }     // = Template.Notes, see flag #2
    public DateOnly Date { get; set; }
    public decimal? Score { get; set; }
    public decimal? ScorePercentage { get; set; } // null when not gradeable

    /// <summary>
    /// F2 — the exam's maximum grade (the occurrence's snapshotted MaxGrade, i.e. the denominator
    /// used for <see cref="ScorePercentage"/>). Lets the app render "Score: 40/50" — the percentage
    /// alone can't recover the denominator. Null for an exam with no max grade configured.
    /// </summary>
    public decimal? MaxGrade { get; set; }

    /// <summary>
    /// F3 — the publishing teacher's subject, resolved by the student's language (ministry subject
    /// name preferred, falling back to the teacher's custom subject). Null when the teacher has no
    /// subject on file. Same for every row in the list (one teacher per request).
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// F1 — the student's rank within this exam's cohort (the students sharing the same
    /// session/occurrence), by grade descending. Competition ranking: ties share a rank (same grade =
    /// same rank), so the position after a tie skips accordingly. Null when the student's exam is not
    /// graded (Pending / Missed / ungradeable) — rank is only meaningful for a graded exam.
    /// </summary>
    public int? Rank { get; set; }

    /// <summary>
    /// F1 — the size of the ranking cohort: the number of graded students in this exam's
    /// session/occurrence (includes the student). Null when the student's exam is not graded.
    /// Renders alongside <see cref="Rank"/> as "Rank 3 of 25".
    /// </summary>
    public int? GroupSize { get; set; }

    public ObligationStatus Status { get; set; }
}
