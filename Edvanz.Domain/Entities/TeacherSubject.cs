using Edvanz.Domain.Entities.ShareProp;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Junction table linking a Teacher to one or more Subjects.
/// AAM-FR-03.5: Teacher selects the subject they teach during registration.
/// Schema allows multiple subjects per teacher for future flexibility.
/// </summary>
public class TeacherSubject : BaseEntity
{
    /// <summary>
    /// Foreign key to the Teacher who teaches this subject.
    /// </summary>
    [ForeignKey(nameof(Teacher))]
    public long TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    /// <summary>
    /// Foreign key to the ministry-defined subject.
    /// </summary>
    [ForeignKey(nameof(Subject))]
    public long SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
}