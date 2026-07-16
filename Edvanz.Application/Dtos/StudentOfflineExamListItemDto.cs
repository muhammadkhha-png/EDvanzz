using Edvanz.Domain.Enums;

public class StudentOfflineExamListItemDto
{
    public long ExamId { get; set; }            // = OccurrenceId, see flag #1
    public string ExamName { get; set; } = null!;
    public string? Description { get; set; }     // = Template.Notes, see flag #2
    public DateOnly Date { get; set; }
    public decimal? Score { get; set; }
    public decimal? ScorePercentage { get; set; } // null when not gradeable
    public ObligationStatus Status { get; set; }
}