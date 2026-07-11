using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// How an offline exam is delivered relative to the teaching session it is anchored to.
/// Exam-only — null for homework templates.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExamDeliveryType : byte
{
    /// <summary>
    /// The exam is taken during an existing scheduled session occurrence. The exam occurrence
    /// is linked to a concrete <see cref="SessionOccurrence"/> and its attendance is driven by
    /// (and kept in sync with) that session's attendance. Exam-screen attendance is read-only.
    /// </summary>
    DuringSession = 0,

    /// <summary>
    /// The exam is taken at a separate, dedicated date the tutor chooses. The exam occurrence is
    /// still assigned to a session (for grouping/context) but has its own <c>DueDate</c> and no
    /// linked session occurrence; attendance is taken inside the Exams module.
    /// </summary>
    SeparateTime = 1
}
