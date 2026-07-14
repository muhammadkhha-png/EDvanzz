namespace Edvanz.Domain.Enums;

/// <summary>
/// Lifecycle status of an <see cref="Entities.OnlineExam"/>.
/// Draft → Published → Closed. No reverse transition from Closed (§6).
/// Numeric values are part of the persisted contract — never reorder.
/// </summary>
public enum OnlineExamStatus : byte
{
    Draft = 1,
    Published = 2,
    Closed = 3
}